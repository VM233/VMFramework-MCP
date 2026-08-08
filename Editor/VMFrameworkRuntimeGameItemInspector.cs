#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMCP.Editor;
using VMFramework.Containers;
using VMFramework.GameLogicArchitecture;
using VMFramework.Properties;

namespace VMFramework.MCP.Editor
{
    internal static class VMFrameworkRuntimeGameItemInspector
    {
        internal static Dictionary<string, object> Describe(IGameItem gameItem)
        {
            if (gameItem == null)
                return null;

            var domainSections = new Dictionary<string, object>();
            if (VMFrameworkMcpRuntimeGameItemDomain.TryGetAdapter(gameItem,
                    out IVMFrameworkMcpRuntimeGameItemDomainAdapter adapter))
            {
                adapter.AddInspectionSections(gameItem, domainSections);
            }

            var result = new Dictionary<string, object>
            {
                { "identity", DescribeIdentity(gameItem) },
                { "gameTags", gameItem.GameTags?
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(tag => tag, StringComparer.Ordinal)
                    .ToList() ?? new List<string>() },
                { "properties", DescribeProperties(gameItem) },
                { "containers", DescribeContainers(gameItem) },
                { "abilities", TakeDomainSection(domainSections, "abilities",
                    new Dictionary<string, object> { { "available", false } }) },
                { "faction", TakeDomainSection(domainSections, "faction",
                    new Dictionary<string, object> { { "available", false } }) },
                { "lifecycle", DescribeLifecycle(gameItem) },
                { "domain", domainSections },
            };
            return result;
        }

        internal static IGameItem Resolve(Dictionary<string, object> args)
        {
            string cleanupToken = GetString(args, "cleanupToken");
            if (!string.IsNullOrWhiteSpace(cleanupToken))
            {
                if (!VMFrameworkRuntimeGameItemSessions.TryGetGameItem(cleanupToken,
                        out IGameItem sessionGameItem))
                {
                    throw new MCPProjectToolException(
                        "runtime_game_item_session_not_found",
                        $"Runtime GameItem session '{cleanupToken}' was not found.");
                }
                return sessionGameItem;
            }

            string objectID = GetString(args, "objectID");
            if (!string.IsNullOrWhiteSpace(objectID))
            {
                UnityEngine.Object unityObject = MCPObjectId.ToObject(objectID);
                IGameItem gameItem = GetGameItem(unityObject);
                if (gameItem == null)
                {
                    throw new MCPProjectToolException("runtime_game_item_not_found",
                        $"Object '{objectID}' is not a runtime GameItem.");
                }
                return gameItem;
            }

            string path = GetString(args, "gameObjectPath");
            if (!string.IsNullOrWhiteSpace(path))
            {
                GameObject gameObject = FindSceneGameObject(path);
                IGameItem gameItem = GetGameItem(gameObject);
                if (gameItem == null)
                {
                    throw new MCPProjectToolException("runtime_game_item_not_found",
                        $"GameObject '{path}' has no runtime GameItem component.");
                }
                return gameItem;
            }

            throw new MCPProjectToolException("invalid_arguments",
                "Exactly one of cleanupToken, objectID, or gameObjectPath is required.");
        }

        private static Dictionary<string, object> DescribeIdentity(IGameItem gameItem)
        {
            IGamePrefab gamePrefab = GamePrefabManager.GetGamePrefab(gameItem.id);
            var identity = new Dictionary<string, object>
            {
                { "id", gameItem.id ?? "" },
                { "name", gameItem.Name ?? "" },
                { "type", gameItem.GetType().FullName },
                { "gamePrefabType", gamePrefab?.GetType().FullName ?? "" },
                { "isController", gameItem is IControllerGameItem },
            };
            if (gameItem is Component component)
            {
                identity["objectID"] = MCPObjectId.Get(component);
                identity["gameObjectPath"] =
                    VMFrameworkMcpTools.GetGameObjectPath(component.transform);
                identity["scene"] = component.gameObject.scene.IsValid()
                    ? component.gameObject.scene.path
                    : "";
                identity["position"] = new Dictionary<string, object>
                {
                    { "x", component.transform.position.x },
                    { "y", component.transform.position.y },
                    { "z", component.transform.position.z },
                };
            }
            return identity;
        }

        private static List<Dictionary<string, object>> DescribeProperties(
            IGameItem gameItem)
        {
            if (gameItem is not IPropertyManagerOwner owner ||
                owner.PropertyManager == null)
            {
                return new List<Dictionary<string, object>>();
            }

            return owner.PropertyManager.Properties
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair =>
                {
                    object value = null;
                    string valueError = "";
                    try
                    {
                        value = VMFrameworkMcpTools.DescribeValue(pair.Value.ObjectValue);
                    }
                    catch (Exception exception)
                    {
                        valueError = exception.Message;
                    }
                    return new Dictionary<string, object>
                    {
                        { "name", pair.Key },
                        { "propertyType", pair.Value.GetType().FullName },
                        { "valueType", GetPropertyValueType(pair.Value).FullName },
                        { "writable", pair.Value is IProperty },
                        { "value", value },
                        { "valueError", valueError },
                    };
                })
                .ToList();
        }

        private static List<Dictionary<string, object>> DescribeContainers(
            IGameItem gameItem)
        {
            var containers = new List<(string source, IContainer container)>();
            if (gameItem is IContainer selfContainer)
                containers.Add(("self", selfContainer));
            if (gameItem is IPropertyManagerOwner propertyOwner &&
                propertyOwner.PropertyManager != null)
            {
                foreach (KeyValuePair<string, IReadOnlyProperty> pair in
                         propertyOwner.PropertyManager.Properties)
                {
                    try
                    {
                        if (pair.Value.ObjectValue is IContainer propertyContainer)
                            containers.Add(("property:" + pair.Key, propertyContainer));
                    }
                    catch
                    {
                    }
                }
            }
            if (gameItem is Component component)
            {
                foreach (IContainer componentContainer in component
                             .GetComponentsInChildren<MonoBehaviour>(true)
                             .OfType<IContainer>())
                {
                    containers.Add(("component", componentContainer));
                }
            }

            var seen = new HashSet<IContainer>(ReferenceComparer<IContainer>.Instance);
            return containers
                .Where(pair => pair.container != null && seen.Add(pair.container))
                .Select(pair => new Dictionary<string, object>
                {
                    { "source", pair.source },
                    { "value", VMFrameworkMcpTools.DescribeContainer(pair.container) },
                })
                .ToList();
        }

        private static Dictionary<string, object> DescribeLifecycle(IGameItem gameItem)
        {
            bool sessionOwned = VMFrameworkRuntimeGameItemSessions.TryGetToken(
                gameItem, out _);
            var result = new Dictionary<string, object>
            {
                { "isDestroyed", gameItem.IsDestroyed },
                { "poolState", gameItem.IsDestroyed ? "returned" : "borrowed" },
                { "sessionOwned", sessionOwned },
            };
            if (gameItem is Component component)
            {
                result["activeSelf"] = component.gameObject.activeSelf;
                result["activeInHierarchy"] = component.gameObject.activeInHierarchy;
                result["enabled"] = component is Behaviour behaviour
                    ? (object)behaviour.enabled
                    : null;
            }
            return result;
        }

        private static object TakeDomainSection(IDictionary<string, object> sections,
            string key, object fallback)
        {
            if (!sections.TryGetValue(key, out object value))
                return fallback;
            sections.Remove(key);
            return value;
        }

        private static Type GetPropertyValueType(IReadOnlyProperty property)
        {
            Type propertyInterface = property.GetType().GetInterfaces().FirstOrDefault(type =>
                type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(IReadOnlyProperty<>));
            return propertyInterface?.GetGenericArguments()[0] ??
                   property.ObjectValue?.GetType() ??
                   typeof(object);
        }

        private static IGameItem GetGameItem(UnityEngine.Object unityObject)
        {
            if (unityObject is IGameItem direct)
                return direct;
            GameObject gameObject = unityObject switch
            {
                GameObject value => value,
                Component component => component.gameObject,
                _ => null,
            };
            return gameObject?
                .GetComponentsInChildren<MonoBehaviour>(true)
                .OfType<IGameItem>()
                .FirstOrDefault();
        }

        private static GameObject FindSceneGameObject(string pathOrName)
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!scene.isLoaded)
                    continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform transform in root
                                 .GetComponentsInChildren<Transform>(true))
                    {
                        if (string.Equals(transform.name, pathOrName,
                                StringComparison.Ordinal) ||
                            string.Equals(VMFrameworkMcpTools.GetGameObjectPath(transform),
                                pathOrName, StringComparison.Ordinal))
                        {
                            return transform.gameObject;
                        }
                    }
                }
            }
            return null;
        }

        private static string GetString(IReadOnlyDictionary<string, object> args,
            string key)
        {
            return args != null && args.TryGetValue(key, out object value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : "";
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T>
            where T : class
        {
            internal static readonly ReferenceComparer<T> Instance = new();

            public bool Equals(T x, T y) => ReferenceEquals(x, y);

            public int GetHashCode(T obj) =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }

    public static class VMFrameworkInspectRuntimeGameItemTool
    {
        private const string ToolName = "vmframework/inspect-runtime-game-item";
        private const string InputSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"cleanupToken\":{\"type\":\"string\",\"description\":\"Runtime GameItem session token.\"}," +
            "\"objectID\":{\"type\":\"string\",\"description\":\"Unity object id of a controller GameItem component or GameObject.\"}," +
            "\"gameObjectPath\":{\"type\":\"string\",\"description\":\"Loaded-scene GameObject path or name.\"}" +
            "},\"oneOf\":[{\"required\":[\"cleanupToken\"]},{\"required\":[\"objectID\"]},{\"required\":[\"gameObjectPath\"]}],\"additionalProperties\":false}";
        private const string OutputSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"identity\":{\"type\":\"object\",\"additionalProperties\":true}," +
            "\"gameTags\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}," +
            "\"properties\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":true}}," +
            "\"containers\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":true}}," +
            "\"abilities\":{\"type\":\"object\",\"additionalProperties\":true}," +
            "\"faction\":{\"type\":\"object\",\"additionalProperties\":true}," +
            "\"lifecycle\":{\"type\":\"object\",\"additionalProperties\":true}," +
            "\"domain\":{\"type\":\"object\",\"additionalProperties\":true}" +
            "},\"required\":[\"identity\",\"gameTags\",\"properties\",\"containers\",\"abilities\",\"faction\",\"lifecycle\",\"domain\"],\"additionalProperties\":false}";

        [MCPProjectTool(ToolName,
            ShortName = "vmf/inspect-runtime-item",
            Description = "Inspect one live VMFramework GameItem in a single response: identity, GameTags, Properties, Containers, project-domain Abilities and Faction, lifecycle, and pool state.",
            InputSchemaJson = InputSchema,
            OutputSchemaJson = OutputSchema,
            SideEffects = MCPProjectToolSideEffect.ReadsProjectState,
            ErrorCodes = new[]
            {
                "requires_play_mode",
                "runtime_game_item_not_found",
                "runtime_game_item_session_not_found",
                "runtime_game_item_domain_adapter_ambiguous",
            },
            ReadOnly = true,
            RequiresPlayMode = true)]
        public static object Execute(Dictionary<string, object> args)
        {
            if (!Application.isPlaying)
            {
                throw new MCPProjectToolException("requires_play_mode",
                    "Runtime GameItem inspection requires Play Mode.");
            }
            return VMFrameworkRuntimeGameItemInspector.Describe(
                VMFrameworkRuntimeGameItemInspector.Resolve(
                    args ?? new Dictionary<string, object>()));
        }
    }
}
#endif
