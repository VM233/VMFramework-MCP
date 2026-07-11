#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;
using VMFramework.Properties;
using Object = UnityEngine.Object;

namespace VMFramework.MCP.Editor
{
    [InitializeOnLoad]
    public static partial class VMFrameworkMcpTools
    {
        private const string GET_PROPERTY_TOOL_NAME = "vmframework/get-property";
        private const string SET_PROPERTY_TOOL_NAME = "vmframework/set-property";
        private const string START_PROPERTY_TRACE_TOOL_NAME = "vmframework/start-property-trace";
        private const string GET_PROPERTY_TRACE_TOOL_NAME = "vmframework/get-property-trace";
        private const string STOP_PROPERTY_TRACE_TOOL_NAME = "vmframework/stop-property-trace";

        private const string PROPERTY_SCHEMA =
            "{\"type\":\"object\",\"properties\":{" +
            "\"managerInstanceID\":{\"type\":\"integer\",\"description\":\"Exact PropertyManager instance id.\"}," +
            "\"gameObjectPath\":{\"type\":\"string\",\"description\":\"Scene GameObject path or name.\"}," +
            "\"managerIndex\":{\"type\":\"integer\",\"description\":\"PropertyManager index under the GameObject. Defaults to 0.\"}," +
            "\"propertyName\":{\"type\":\"string\",\"description\":\"Exact property name.\"}" +
            "},\"required\":[\"propertyName\"]}";

        private const string SET_PROPERTY_SCHEMA =
            "{\"type\":\"object\",\"properties\":{" +
            "\"managerInstanceID\":{\"type\":\"integer\"},\"gameObjectPath\":{\"type\":\"string\"}," +
            "\"managerIndex\":{\"type\":\"integer\"},\"propertyName\":{\"type\":\"string\"}," +
            "\"value\":{\"description\":\"Typed value. Unity Object values accept an asset path or {assetPath}.\"}," +
            "\"initial\":{\"type\":\"boolean\",\"description\":\"Pass initial=true to SetObjectValue. Defaults to false.\"}" +
            "},\"required\":[\"propertyName\",\"value\"]}";

        private const string TRACE_SCHEMA =
            "{\"type\":\"object\",\"properties\":{" +
            "\"managerInstanceID\":{\"type\":\"integer\"},\"gameObjectPath\":{\"type\":\"string\"}," +
            "\"managerIndex\":{\"type\":\"integer\"},\"propertyName\":{\"type\":\"string\",\"description\":\"Optional exact property filter.\"}," +
            "\"includeChildren\":{\"type\":\"boolean\",\"description\":\"Trace child PropertyManagers. Defaults to true.\"}," +
            "\"maxEvents\":{\"type\":\"integer\",\"description\":\"Maximum retained events, 1-10000. Defaults to 1000.\"}," +
            "\"clear\":{\"type\":\"boolean\",\"description\":\"Clear retained events before returning.\"}" +
            "}}";

        private static readonly Dictionary<IReadOnlyProperty, PropertyTraceTarget> propertyTraceTargets = new();
        private static readonly List<Dictionary<string, object>> propertyTraceEvents = new();
        private static int propertyTraceMaxEvents = 1000;
        private static bool propertyTraceActive;

        static VMFrameworkMcpTools()
        {
            EditorApplication.playModeStateChanged -= OnPropertyTracePlayModeChanged;
            EditorApplication.playModeStateChanged += OnPropertyTracePlayModeChanged;
        }

        [MCPProjectTool(GET_PROPERTY_TOOL_NAME,
            Description = "Read one VMFramework PropertyManager property with its concrete property type and value type.",
            InputSchemaJson = PROPERTY_SCHEMA,
            ReadOnly = true)]
        public static object GetProperty(Dictionary<string, object> args)
        {
            args ??= new();
            var manager = ResolvePropertyManager(args);
            var propertyName = GetRequiredString(args, "propertyName");
            if (!manager.Properties.TryGetValue(propertyName, out var property))
                throw new KeyNotFoundException($"Property '{propertyName}' was not found on '{GetGameObjectPath(manager.transform)}'.");
            return DescribeTypedProperty(manager, propertyName, property);
        }

        [MCPProjectTool(SET_PROPERTY_TOOL_NAME,
            Description = "Set one writable VMFramework property using its runtime value type and return the before/after values.",
            InputSchemaJson = SET_PROPERTY_SCHEMA)]
        public static object SetProperty(Dictionary<string, object> args)
        {
            args ??= new();
            var manager = ResolvePropertyManager(args);
            var propertyName = GetRequiredString(args, "propertyName");
            if (!manager.Properties.TryGetValue(propertyName, out var readOnlyProperty))
                throw new KeyNotFoundException($"Property '{propertyName}' was not found.");
            if (readOnlyProperty is not IProperty property)
                throw new InvalidOperationException($"Property '{propertyName}' is read-only ({readOnlyProperty.GetType().FullName}).");
            if (!args.TryGetValue("value", out var rawValue)) throw new ArgumentException("value is required.");

            var before = DescribeTypedProperty(manager, propertyName, readOnlyProperty);
            var valueType = GetPropertyValueType(readOnlyProperty);
            var converted = ConvertSerializedValue(rawValue, valueType, propertyName);
            property.SetObjectValue(converted, GetBool(args, "initial", false));
            return new Dictionary<string, object>
            {
                { "success", true }, { "before", before },
                { "after", DescribeTypedProperty(manager, propertyName, readOnlyProperty) }
            };
        }

        [MCPProjectTool(START_PROPERTY_TRACE_TOOL_NAME,
            Description = "Start tracing dirty events from selected VMFramework PropertyManager properties.",
            InputSchemaJson = TRACE_SCHEMA,
            ReadOnly = true)]
        public static object StartPropertyTrace(Dictionary<string, object> args)
        {
            args ??= new();
            StopPropertyTraceInternal();
            propertyTraceEvents.Clear();
            propertyTraceMaxEvents = Math.Max(1, Math.Min(10000, GetInt(args, "maxEvents", 1000)));
            var propertyName = GetString(args, "propertyName");
            foreach (var manager in ResolvePropertyManagers(args))
            {
                foreach (var pair in manager.Properties)
                {
                    if (!string.IsNullOrWhiteSpace(propertyName) && pair.Key != propertyName) continue;
                    propertyTraceTargets[pair.Value] = new PropertyTraceTarget(manager, pair.Key);
                    pair.Value.OnDirty += OnTracedPropertyDirty;
                }
            }

            propertyTraceActive = true;
            return DescribePropertyTrace("start");
        }

        [MCPProjectTool(GET_PROPERTY_TRACE_TOOL_NAME,
            Description = "Return retained VMFramework property-change trace events.",
            InputSchemaJson = TRACE_SCHEMA,
            ReadOnly = true)]
        public static object GetPropertyTrace(Dictionary<string, object> args)
        {
            args ??= new();
            var result = DescribePropertyTrace("get");
            if (GetBool(args, "clear", false)) propertyTraceEvents.Clear();
            return result;
        }

        [MCPProjectTool(STOP_PROPERTY_TRACE_TOOL_NAME,
            Description = "Stop VMFramework property-change tracing and return retained events.",
            InputSchemaJson = TRACE_SCHEMA,
            ReadOnly = true)]
        public static object StopPropertyTrace(Dictionary<string, object> args)
        {
            var result = DescribePropertyTrace("stop");
            StopPropertyTraceInternal();
            return result;
        }

        private static PropertyManager ResolvePropertyManager(Dictionary<string, object> args)
        {
            var managers = ResolvePropertyManagers(args);
            if (managers.Count == 0) throw new InvalidOperationException("No PropertyManager matched the request.");
            var index = GetInt(args, "managerIndex", 0);
            if (index < 0 || index >= managers.Count) throw new IndexOutOfRangeException($"managerIndex {index} is invalid.");
            return managers[index];
        }

        private static List<PropertyManager> ResolvePropertyManagers(Dictionary<string, object> args)
        {
            var instanceID = GetInt(args, "managerInstanceID", 0);
            if (instanceID != 0)
            {
                var manager = EditorUtility.InstanceIDToObject(instanceID) as PropertyManager;
                return manager == null ? new List<PropertyManager>() : new List<PropertyManager> { manager };
            }

            var gameObjectPath = GetString(args, "gameObjectPath");
            GameObject root = null;
            if (!string.IsNullOrWhiteSpace(gameObjectPath)) root = FindSceneGameObject(gameObjectPath);
            else if (Selection.activeGameObject != null) root = Selection.activeGameObject;
            if (root == null) return new List<PropertyManager>();

            var managers = new List<PropertyManager>();
            AddPropertyManagers(root, managers, GetBool(args, "includeChildren", true));
            return managers.Where(manager => manager != null).Distinct().ToList();
        }

        private static Dictionary<string, object> DescribeTypedProperty(PropertyManager manager, string name,
            IReadOnlyProperty property)
        {
            object value;
            string error = "";
            try { value = property.ObjectValue; }
            catch (Exception ex) { value = null; error = ex.Message; }
            return new Dictionary<string, object>
            {
                { "managerInstanceID", manager.GetInstanceID() },
                { "gameObjectPath", GetGameObjectPath(manager.transform) },
                { "propertyName", name }, { "propertyType", property.GetType().FullName },
                { "valueType", GetPropertyValueType(property).FullName },
                { "writable", property is IProperty }, { "value", DescribeValue(value) }, { "valueError", error }
            };
        }

        private static Type GetPropertyValueType(IReadOnlyProperty property)
        {
            var propertyInterface = property.GetType().GetInterfaces().FirstOrDefault(type =>
                type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyProperty<>));
            return propertyInterface?.GetGenericArguments()[0] ?? property.ObjectValue?.GetType() ?? typeof(object);
        }

        private static void OnTracedPropertyDirty(IReadOnlyProperty property, bool initial)
        {
            if (!propertyTraceTargets.TryGetValue(property, out var target)) return;
            if (propertyTraceEvents.Count >= propertyTraceMaxEvents) propertyTraceEvents.RemoveAt(0);
            propertyTraceEvents.Add(new Dictionary<string, object>
            {
                { "sequence", propertyTraceEvents.Count }, { "time", EditorApplication.timeSinceStartup },
                { "initial", initial }, { "managerInstanceID", target.Manager.GetInstanceID() },
                { "gameObjectPath", GetGameObjectPath(target.Manager.transform) },
                { "propertyName", target.PropertyName }, { "value", DescribeValue(property.ObjectValue) }
            });
        }

        private static Dictionary<string, object> DescribePropertyTrace(string action)
        {
            return new Dictionary<string, object>
            {
                { "success", true }, { "action", action }, { "active", propertyTraceActive },
                { "targetCount", propertyTraceTargets.Count }, { "eventCount", propertyTraceEvents.Count },
                { "events", new List<Dictionary<string, object>>(propertyTraceEvents) }
            };
        }

        private static void StopPropertyTraceInternal()
        {
            foreach (var property in propertyTraceTargets.Keys) property.OnDirty -= OnTracedPropertyDirty;
            propertyTraceTargets.Clear();
            propertyTraceActive = false;
        }

        private static void OnPropertyTracePlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                StopPropertyTraceInternal();
        }

        private readonly struct PropertyTraceTarget
        {
            public readonly PropertyManager Manager;
            public readonly string PropertyName;
            public PropertyTraceTarget(PropertyManager manager, string propertyName)
            {
                Manager = manager;
                PropertyName = propertyName;
            }
        }
    }
}
#endif
