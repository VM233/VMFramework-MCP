#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityMCP.Editor;
using VMFramework.Configuration;
using VMFramework.Containers;
using VMFramework.GameLogicArchitecture;
using VMFramework.GameLogicArchitecture.Editor;
using VMFramework.OdinExtensions;
using VMFramework.Properties;
using VMFramework.UI;
using Object = UnityEngine.Object;

namespace VMFramework.MCP.Editor
{
    public static partial class VMFrameworkMcpTools
    {
        private const string LIST_GAME_PREFAB_TYPES_TOOL_NAME = "vmframework/list-game-prefab-types";
        private const string ADD_GAME_PREFAB_TOOL_NAME = "vmframework/add-game-prefab";
        private const string FIND_GAME_PREFAB_TOOL_NAME = "vmframework/find-game-prefab";
        private const string INSPECT_GAME_PREFAB_WRAPPER_TOOL_NAME = "vmframework/inspect-game-prefab-wrapper";
        private const string LIST_GENERAL_SETTINGS_TOOL_NAME = "vmframework/list-general-settings";
        private const string INSPECT_UI_PANEL_TOOL_NAME = "vmframework/inspect-ui-panel";
        private const string INSPECT_BIND_OBJECTS_TOOL_NAME = "vmframework/inspect-bind-objects";
        private const string VALIDATE_VISUAL_ELEMENT_PATHS_TOOL_NAME = "vmframework/validate-visual-element-paths";
        private const string INSPECT_CONTAINER_PANEL_TOOL_NAME = "vmframework/inspect-container-panel";
        private const string INSPECT_PROPERTY_MANAGER_TOOL_NAME = "vmframework/inspect-property-manager";

        private const string LIST_GAME_PREFAB_TYPES_INPUT_SCHEMA_JSON =
            "{\"type\":\"object\",\"properties\":{" +
            "\"filter\":{\"type\":\"string\",\"description\":\"Optional case-insensitive type name filter.\"}," +
            "\"includeAbstract\":{\"type\":\"boolean\",\"description\":\"Include abstract and interface GamePrefab types. Defaults to false.\"}" +
            "}}";

        private const string ADD_GAME_PREFAB_INPUT_SCHEMA_JSON =
            "{\"type\":\"object\",\"properties\":{" +
            "\"id\":{\"type\":\"string\",\"description\":\"GamePrefab id to create or replace.\"}," +
            "\"gamePrefabType\":{\"type\":\"string\",\"description\":\"Instantiable GamePrefab type name, full name, or assembly-qualified name.\"}," +
            "\"overwrite\":{\"type\":\"boolean\",\"description\":\"Replace an existing single-wrapper GamePrefab with the same id. Defaults to false.\"}," +
            "\"assetName\":{\"type\":\"string\",\"description\":\"Optional wrapper asset file name when creating a new GamePrefab.\"}," +
            "\"serializedValues\":{\"type\":\"object\",\"description\":\"Optional serialized field or property values applied to the created GamePrefab.\"}" +
            "},\"required\":[\"id\",\"gamePrefabType\"]}";

        private const string FIND_GAME_PREFAB_INPUT_SCHEMA_JSON =
            "{\"type\":\"object\",\"properties\":{" +
            "\"id\":{\"type\":\"string\",\"description\":\"Exact GamePrefab id to find.\"}," +
            "\"filter\":{\"type\":\"string\",\"description\":\"Case-insensitive id, wrapper path, or type filter.\"}," +
            "\"gamePrefabType\":{\"type\":\"string\",\"description\":\"Optional GamePrefab type name, full name, or assembly-qualified name filter.\"}," +
            "\"limit\":{\"type\":\"integer\",\"description\":\"Maximum result count. Defaults to 100.\"}" +
            "}}";

        private const string INSPECT_GAME_PREFAB_WRAPPER_INPUT_SCHEMA_JSON =
            "{\"type\":\"object\",\"properties\":{" +
            "\"id\":{\"type\":\"string\",\"description\":\"GamePrefab id contained by the wrapper.\"}," +
            "\"wrapperPath\":{\"type\":\"string\",\"description\":\"Asset path of a GamePrefabWrapper.\"}," +
            "\"filter\":{\"type\":\"string\",\"description\":\"Optional wrapper path, wrapper name, GamePrefab id, or type filter.\"}," +
            "\"limit\":{\"type\":\"integer\",\"description\":\"Maximum wrapper count when id and wrapperPath are omitted. Defaults to 50.\"}" +
            "}}";

        private const string LIST_GENERAL_SETTINGS_INPUT_SCHEMA_JSON =
            "{\"type\":\"object\",\"properties\":{" +
            "\"filter\":{\"type\":\"string\",\"description\":\"Case-insensitive type, asset name, or path filter.\"}," +
            "\"includeGamePrefabDetails\":{\"type\":\"boolean\",\"description\":\"Include GamePrefabGeneralSetting provider details. Defaults to true.\"}" +
            "}}";

        private const string PANEL_SOURCE_INPUT_SCHEMA_JSON =
            "{\"type\":\"object\",\"properties\":{" +
            "\"panelID\":{\"type\":\"string\",\"description\":\"UIPanelConfig id to inspect.\"}," +
            "\"prefabPath\":{\"type\":\"string\",\"description\":\"Panel prefab asset path to inspect.\"}," +
            "\"includeRuntime\":{\"type\":\"boolean\",\"description\":\"Include runtime unique panel state when Play Mode is running. Defaults to true.\"}" +
            "}}";

        private const string VALIDATE_VISUAL_ELEMENT_PATHS_INPUT_SCHEMA_JSON =
            "{\"type\":\"object\",\"properties\":{" +
            "\"panelID\":{\"type\":\"string\",\"description\":\"UIPanelConfig id to validate.\"}," +
            "\"prefabPath\":{\"type\":\"string\",\"description\":\"Panel prefab asset path to validate.\"}," +
            "\"includeValid\":{\"type\":\"boolean\",\"description\":\"Include valid paths in the result. Defaults to false.\"}" +
            "}}";

        private const string INSPECT_PROPERTY_MANAGER_INPUT_SCHEMA_JSON =
            "{\"type\":\"object\",\"properties\":{" +
            "\"prefabPath\":{\"type\":\"string\",\"description\":\"Prefab asset path whose PropertyManagers should be inspected.\"}," +
            "\"gameObjectPath\":{\"type\":\"string\",\"description\":\"Slash-separated scene GameObject path or GameObject name.\"}," +
            "\"propertyName\":{\"type\":\"string\",\"description\":\"Optional exact property name filter.\"}," +
            "\"includeChildren\":{\"type\":\"boolean\",\"description\":\"Inspect child PropertyManagers. Defaults to true.\"}," +
            "\"useSelection\":{\"type\":\"boolean\",\"description\":\"Use selected GameObjects when prefabPath and gameObjectPath are omitted. Defaults to true.\"}," +
            "\"limit\":{\"type\":\"integer\",\"description\":\"Maximum manager count when scanning loaded scenes. Defaults to 50.\"}" +
            "}}";

        [MCPProjectTool(LIST_GAME_PREFAB_TYPES_TOOL_NAME,
            Description = "List VMFramework GamePrefab types and their matching GamePrefabGeneralSetting.",
            InputSchemaJson = LIST_GAME_PREFAB_TYPES_INPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object ListGamePrefabTypes(Dictionary<string, object> args)
        {
            args ??= new();
            string filter = GetString(args, "filter");
            bool includeAbstract = GetBool(args, "includeAbstract", false);

            var types = GetGamePrefabTypes(includeAbstract)
                .Where(type => MatchesFilter(type.Name, filter) ||
                               MatchesFilter(type.FullName, filter) ||
                               MatchesFilter(type.AssemblyQualifiedName, filter))
                .Select(type =>
                {
                    var setting = GetGamePrefabGeneralSetting(type);
                    return new Dictionary<string, object>
                    {
                        { "name", type.Name },
                        { "fullName", type.FullName },
                        { "assemblyQualifiedName", type.AssemblyQualifiedName },
                        { "isAbstract", type.IsAbstract },
                        { "isInterface", type.IsInterface },
                        { "hasDefaultConstructor", type.GetConstructor(Type.EmptyTypes) != null },
                        { "generalSetting", DescribeGeneralSetting(setting, includeGamePrefabDetails: true) }
                    };
                })
                .OrderBy(info => info["fullName"])
                .ToList();

            return new Dictionary<string, object>
            {
                { "types", types },
                { "totalTypes", types.Count }
            };
        }

        [MCPProjectTool(ADD_GAME_PREFAB_TOOL_NAME,
            Description = "Create or replace a VMFramework GamePrefab wrapper by id and register it to the matching GamePrefabGeneralSetting.",
            InputSchemaJson = ADD_GAME_PREFAB_INPUT_SCHEMA_JSON,
            MutatesAssets = true)]
        public static object AddGamePrefab(Dictionary<string, object> args)
        {
            args ??= new();

            string id = GetRequiredString(args, "id");
            Type gamePrefabType = ResolveGamePrefabType(GetRequiredString(args, "gamePrefabType"));
            bool overwrite = GetBool(args, "overwrite", false);
            string assetName = GetString(args, "assetName");
            var serializedValues = GetDictionary(args, "serializedValues");
            var warnings = new List<string>();

            RefreshGamePrefabRegistry();

            var existingInfos = FindGamePrefabInfos(id: id, filter: null, gamePrefabType: null, limit: int.MaxValue);
            if (existingInfos.Count > 0 && overwrite == false)
            {
                throw new InvalidOperationException(
                    $"GamePrefab id '{id}' already exists in: {string.Join(", ", existingInfos.Select(info => info.wrapperPath))}");
            }

            IGamePrefab gamePrefab = CreateGamePrefab(id, gamePrefabType, serializedValues, warnings);
            GamePrefabGeneralSetting gamePrefabGeneralSetting = ResolveGamePrefabGeneralSetting(gamePrefab);

            GamePrefabWrapper wrapper;
            bool created;
            bool replaced;

            if (existingInfos.Count > 0)
            {
                if (existingInfos.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"GamePrefab id '{id}' exists in multiple wrappers. Refusing to overwrite.");
                }

                var existingInfo = existingInfos[0];
                if (existingInfo.wrapper is not GamePrefabSingleWrapper singleWrapper)
                {
                    throw new InvalidOperationException(
                        $"Existing wrapper '{existingInfo.wrapperPath}' is not a GamePrefabSingleWrapper.");
                }

                if (string.IsNullOrWhiteSpace(assetName) == false)
                {
                    warnings.Add("assetName is ignored when overwriting an existing GamePrefab.");
                }

                singleWrapper.InitGamePrefabs(new[] { gamePrefab });
                wrapper = singleWrapper;
                created = false;
                replaced = true;
            }
            else
            {
                wrapper = CreateWrapper(gamePrefab, gamePrefabGeneralSetting, assetName);
                created = true;
                replaced = false;
            }

            RegisterWrapper(gamePrefabGeneralSetting, wrapper);
            wrapper = SaveAndRefresh(wrapper, gamePrefabGeneralSetting);
            ValidateWrapperContainsGamePrefab(wrapper, id);

            return new Dictionary<string, object>
            {
                { "id", id },
                { "gamePrefab", DescribeGamePrefab(gamePrefab) },
                { "wrapper", DescribeWrapper(wrapper, includeGamePrefabs: true) },
                { "generalSetting", DescribeGeneralSetting(gamePrefabGeneralSetting, includeGamePrefabDetails: true) },
                { "created", created },
                { "replaced", replaced },
                { "registered", gamePrefabGeneralSetting.initialGamePrefabProviders.Contains(wrapper) },
                { "warnings", warnings }
            };
        }

        [MCPProjectTool(FIND_GAME_PREFAB_TOOL_NAME,
            Description = "Find VMFramework GamePrefabs by id, type, wrapper path, or filter.",
            InputSchemaJson = FIND_GAME_PREFAB_INPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object FindGamePrefab(Dictionary<string, object> args)
        {
            args ??= new();
            string id = GetString(args, "id");
            string filter = GetString(args, "filter");
            string typeName = GetString(args, "gamePrefabType");
            int limit = Math.Max(1, GetInt(args, "limit", 100));
            Type typeFilter = string.IsNullOrWhiteSpace(typeName) ? null : ResolveGamePrefabType(typeName, allowAbstract: true);

            var infos = FindGamePrefabInfos(id, filter, typeFilter, limit);
            return new Dictionary<string, object>
            {
                { "gamePrefabs", infos.Select(DescribeGamePrefabInfo).ToList() },
                { "count", infos.Count },
                { "limit", limit }
            };
        }

        [MCPProjectTool(INSPECT_GAME_PREFAB_WRAPPER_TOOL_NAME,
            Description = "Inspect VMFramework GamePrefabWrapper assets and the GamePrefabs they contain.",
            InputSchemaJson = INSPECT_GAME_PREFAB_WRAPPER_INPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object InspectGamePrefabWrapper(Dictionary<string, object> args)
        {
            args ??= new();
            string id = GetString(args, "id");
            string wrapperPath = GetString(args, "wrapperPath");
            string filter = GetString(args, "filter");
            int limit = Math.Max(1, GetInt(args, "limit", 50));

            var wrappers = new List<GamePrefabWrapper>();
            if (string.IsNullOrWhiteSpace(wrapperPath) == false)
            {
                var wrapper = AssetDatabase.LoadAssetAtPath<GamePrefabWrapper>(wrapperPath);
                if (wrapper != null)
                {
                    wrappers.Add(wrapper);
                }
            }
            else if (string.IsNullOrWhiteSpace(id) == false)
            {
                wrappers.AddRange(FindGamePrefabInfos(id, null, null, int.MaxValue)
                    .Select(info => info.wrapper)
                    .Where(wrapper => wrapper != null)
                    .Distinct());
            }
            else
            {
                wrappers.AddRange(GetAllGamePrefabWrappers()
                    .Where(wrapper => WrapperMatches(wrapper, filter))
                    .Take(limit));
            }

            return new Dictionary<string, object>
            {
                { "wrappers", wrappers.Select(wrapper => DescribeWrapper(wrapper, includeGamePrefabs: true)).ToList() },
                { "count", wrappers.Count },
                { "limit", limit }
            };
        }

        [MCPProjectTool(LIST_GENERAL_SETTINGS_TOOL_NAME,
            Description = "List VMFramework GeneralSetting assets currently discoverable from global settings and the general settings asset folder.",
            InputSchemaJson = LIST_GENERAL_SETTINGS_INPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object ListGeneralSettings(Dictionary<string, object> args)
        {
            args ??= new();
            string filter = GetString(args, "filter");
            bool includeDetails = GetBool(args, "includeGamePrefabDetails", true);

            var settings = GetAllGeneralSettings()
                .Where(setting =>
                {
                    if (setting is not Object obj)
                    {
                        return MatchesFilter(setting?.GetType().Name, filter) ||
                               MatchesFilter(setting?.GetType().FullName, filter);
                    }

                    return MatchesFilter(obj.name, filter) ||
                           MatchesFilter(AssetDatabase.GetAssetPath(obj), filter) ||
                           MatchesFilter(setting.GetType().Name, filter) ||
                           MatchesFilter(setting.GetType().FullName, filter);
                })
                .Select(setting => DescribeGeneralSetting(setting, includeDetails))
                .OrderBy(info => info["type"])
                .ToList();

            return new Dictionary<string, object>
            {
                { "generalSettingsFolderPath", SafeGet(() => EditorSetting.GeneralSettingsAssetFolderPath) ?? ConfigurationPath.DEFAULT_GENERAL_SETTINGS_PATH },
                { "settings", settings },
                { "count", settings.Count }
            };
        }

        [MCPProjectTool(INSPECT_UI_PANEL_TOOL_NAME,
            Description = "Inspect a VMFramework UI panel prefab/config, UIDocument, VisualTreeAsset, modifiers, bind object names, and optional runtime state.",
            InputSchemaJson = PANEL_SOURCE_INPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object InspectUIPanel(Dictionary<string, object> args)
        {
            args ??= new();
            var source = ResolvePanelSource(args);

            return new Dictionary<string, object>
            {
                { "panelID", source.panelID ?? "" },
                { "config", DescribePanelConfig(source.config) },
                { "prefab", DescribePanelPrefab(source.prefab) },
                { "bindObjects", InspectBindObjects(source, includeRuntime: GetBool(args, "includeRuntime", true)) },
                { "runtime", InspectRuntimePanel(source.panelID, GetBool(args, "includeRuntime", true)) }
            };
        }

        [MCPProjectTool(INSPECT_BIND_OBJECTS_TOOL_NAME,
            Description = "Inspect VMFramework BindObjectsManager names, single-mode names, providers, and optional runtime bound object counts for a UI panel.",
            InputSchemaJson = PANEL_SOURCE_INPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object InspectBindObjects(Dictionary<string, object> args)
        {
            args ??= new();
            var source = ResolvePanelSource(args);
            return InspectBindObjects(source, includeRuntime: GetBool(args, "includeRuntime", true));
        }

        [MCPProjectTool(VALIDATE_VISUAL_ELEMENT_PATHS_TOOL_NAME,
            Description = "Validate VisualElementPath fields on a VMFramework UI panel prefab against its UIDocument VisualTreeAsset.",
            InputSchemaJson = VALIDATE_VISUAL_ELEMENT_PATHS_INPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object ValidateVisualElementPaths(Dictionary<string, object> args)
        {
            args ??= new();
            var source = ResolvePanelSource(args);
            bool includeValid = GetBool(args, "includeValid", false);

            var visualTree = GetVisualTreeAsset(source.prefab);
            if (visualTree == null)
            {
                return new Dictionary<string, object>
                {
                    { "valid", false },
                    { "error", "Panel prefab has no UIDocument VisualTreeAsset." },
                    { "panelID", source.panelID ?? "" },
                    { "prefabPath", GetAssetPath(source.prefab) }
                };
            }

            var root = visualTree.CloneTree();
            var records = new List<VisualElementPathRecord>();
            foreach (var component in source.prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    continue;
                }

                ScanVisualElementPaths(component, GetGameObjectPath(component.transform) + "/" + component.GetType().Name,
                    records, new HashSet<object>(ReferenceEqualityComparer.Instance), 0, null);
            }

            var results = new List<Dictionary<string, object>>();
            int invalidCount = 0;

            foreach (var record in records)
            {
                var result = ValidateVisualElementPath(root, record);
                bool isValid = (bool)result["valid"];
                if (isValid == false)
                {
                    invalidCount++;
                }

                if (includeValid || isValid == false)
                {
                    results.Add(result);
                }
            }

            return new Dictionary<string, object>
            {
                { "valid", invalidCount == 0 },
                { "invalidCount", invalidCount },
                { "checkedCount", records.Count },
                { "reportedCount", results.Count },
                { "panelID", source.panelID ?? "" },
                { "prefabPath", GetAssetPath(source.prefab) },
                { "visualTreeAssetPath", GetAssetPath(visualTree) },
                { "paths", results }
            };
        }

        [MCPProjectTool(INSPECT_CONTAINER_PANEL_TOOL_NAME,
            Description = "Inspect VMFramework UIToolkitContainerModifierBase components, bind object names, slot distributor configs, and optional runtime slot/container state.",
            InputSchemaJson = PANEL_SOURCE_INPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object InspectContainerPanel(Dictionary<string, object> args)
        {
            args ??= new();
            var source = ResolvePanelSource(args);
            bool includeRuntime = GetBool(args, "includeRuntime", true);

            var modifiers = source.prefab.GetComponentsInChildren<UIToolkitContainerModifierBase>(true)
                .Select(modifier => DescribeContainerModifier(modifier, includeRuntime))
                .ToList();

            return new Dictionary<string, object>
            {
                { "panelID", source.panelID ?? "" },
                { "prefabPath", GetAssetPath(source.prefab) },
                { "containerPanelModifiers", modifiers },
                { "count", modifiers.Count }
            };
        }

        [MCPProjectTool(INSPECT_PROPERTY_MANAGER_TOOL_NAME,
            Description = "Inspect VMFramework PropertyManager components on a prefab, selected GameObjects, a scene GameObject path, or loaded scenes.",
            InputSchemaJson = INSPECT_PROPERTY_MANAGER_INPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object InspectPropertyManager(Dictionary<string, object> args)
        {
            args ??= new();
            string prefabPath = GetString(args, "prefabPath");
            string gameObjectPath = GetString(args, "gameObjectPath");
            string propertyName = GetString(args, "propertyName");
            bool includeChildren = GetBool(args, "includeChildren", true);
            bool useSelection = GetBool(args, "useSelection", true);
            int limit = Math.Max(1, GetInt(args, "limit", 50));

            var managers = new List<PropertyManager>();
            string sourceType;

            if (string.IsNullOrWhiteSpace(prefabPath) == false)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    throw new ArgumentException($"Could not load prefab at '{prefabPath}'.");
                }

                sourceType = "prefab";
                AddPropertyManagers(prefab, managers, includeChildren);
            }
            else if (string.IsNullOrWhiteSpace(gameObjectPath) == false)
            {
                var gameObject = FindSceneGameObject(gameObjectPath);
                if (gameObject == null)
                {
                    throw new ArgumentException($"Could not find scene GameObject '{gameObjectPath}'.");
                }

                sourceType = "sceneGameObject";
                AddPropertyManagers(gameObject, managers, includeChildren);
            }
            else if (useSelection && Selection.gameObjects.Length > 0)
            {
                sourceType = "selection";
                foreach (var gameObject in Selection.gameObjects)
                {
                    AddPropertyManagers(gameObject, managers, includeChildren);
                }
            }
            else
            {
                sourceType = "loadedScenes";
                managers.AddRange(Object.FindObjectsByType<PropertyManager>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Take(limit));
            }

            var distinctManagers = managers
                .Where(manager => manager != null)
                .Distinct()
                .Take(limit)
                .Select(manager => DescribePropertyManager(manager, propertyName))
                .ToList();

            return new Dictionary<string, object>
            {
                { "sourceType", sourceType },
                { "propertyName", propertyName ?? "" },
                { "includeChildren", includeChildren },
                { "managers", distinctManagers },
                { "count", distinctManagers.Count },
                { "limit", limit }
            };
        }

        private static IGamePrefab CreateGamePrefab(string id, Type gamePrefabType,
            Dictionary<string, object> serializedValues, List<string> warnings)
        {
            var gamePrefab = GamePrefabWrapperCreator.CreateDefaultGamePrefab(id, gamePrefabType);
            if (gamePrefab == null)
            {
                throw new InvalidOperationException(
                    $"Could not create GamePrefab of type '{gamePrefabType.FullName}'.");
            }

            if (serializedValues != null)
            {
                ApplySerializedValues(gamePrefab, serializedValues);
            }

            if (gamePrefab is GamePrefab typedPrefab)
            {
                if (typedPrefab.IsIDStartsWithPrefix == false)
                {
                    warnings.Add($"ID '{id}' does not start with expected prefix '{typedPrefab.IDPrefix}'.");
                }

                if (typedPrefab.IsIDEndsWithSuffix == false)
                {
                    warnings.Add($"ID '{id}' does not end with expected suffix '{typedPrefab.IDSuffix}'.");
                }
            }

            return gamePrefab;
        }

        private static GamePrefabWrapper CreateWrapper(IGamePrefab gamePrefab,
            GamePrefabGeneralSetting gamePrefabGeneralSetting, string assetName)
        {
            string path = string.IsNullOrWhiteSpace(assetName)
                ? CombineAssetPath(gamePrefabGeneralSetting.GamePrefabFolderPath, ToPascalAssetName(gamePrefab.id))
                : CombineAssetPath(gamePrefabGeneralSetting.GamePrefabFolderPath, assetName);

            var wrapper = GamePrefabWrapperCreator.CreateGamePrefabWrapper(path, GamePrefabWrapperType.Single,
                gamePrefab);
            if (wrapper == null)
            {
                throw new InvalidOperationException(
                    $"Could not create GamePrefab wrapper for id '{gamePrefab.id}' at '{path}'.");
            }

            return wrapper;
        }

        private static void RegisterWrapper(GamePrefabGeneralSetting targetSetting, GamePrefabWrapper wrapper)
        {
            foreach (var setting in GetAllGamePrefabGeneralSettings())
            {
                if (setting == targetSetting)
                {
                    continue;
                }

                if (setting.initialGamePrefabProviders.Contains(wrapper))
                {
                    setting.RemoveFromInitialGamePrefabProviders(wrapper);
                }
            }

            targetSetting.AddToInitialGamePrefabProviders(wrapper);
        }

        private static GamePrefabWrapper SaveAndRefresh(GamePrefabWrapper wrapper,
            GamePrefabGeneralSetting gamePrefabGeneralSetting)
        {
            string wrapperPath = GetAssetPath(wrapper);

            EditorUtility.SetDirty(wrapper);
            EditorUtility.SetDirty(gamePrefabGeneralSetting);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (string.IsNullOrWhiteSpace(wrapperPath) == false)
            {
                wrapper = AssetDatabase.LoadAssetAtPath<GamePrefabWrapper>(wrapperPath) ?? wrapper;
            }

            GamePrefabWrapperInitializeUtility.Refresh();
            return wrapper;
        }

        private static void ValidateWrapperContainsGamePrefab(GamePrefabWrapper wrapper, string id)
        {
            var gamePrefabs = GetGamePrefabs(wrapper);
            if (gamePrefabs.Any(gamePrefab => gamePrefab != null &&
                                              string.Equals(gamePrefab.id, id, StringComparison.Ordinal)))
            {
                return;
            }

            throw new InvalidOperationException(
                $"GamePrefab wrapper '{GetAssetPath(wrapper)}' was saved but does not contain GamePrefab id '{id}'.");
        }

        private static GamePrefabGeneralSetting ResolveGamePrefabGeneralSetting(IGamePrefab gamePrefab)
        {
            var gamePrefabGeneralSetting = GetGamePrefabGeneralSetting(gamePrefab.GetType());
            if (gamePrefabGeneralSetting == null)
            {
                throw new InvalidOperationException(
                    $"Could not find GamePrefabGeneralSetting for '{gamePrefab.GetType().FullName}'.");
            }

            return gamePrefabGeneralSetting;
        }

        private static Type ResolveGamePrefabType(string typeName, bool allowAbstract = false)
        {
            var matches = GetGamePrefabTypes(includeAbstract: allowAbstract)
                .Where(type => string.Equals(type.AssemblyQualifiedName, typeName, StringComparison.Ordinal) ||
                               string.Equals(type.FullName, typeName, StringComparison.Ordinal) ||
                               string.Equals(type.Name, typeName, StringComparison.Ordinal))
                .ToList();

            if (matches.Count == 0)
            {
                throw new ArgumentException($"Could not find GamePrefab type '{typeName}'.");
            }

            if (matches.Count > 1)
            {
                throw new ArgumentException(
                    $"GamePrefab type '{typeName}' is ambiguous: {string.Join(", ", matches.Select(type => type.FullName))}");
            }

            return matches[0];
        }

        private static List<Type> GetGamePrefabTypes(bool includeAbstract)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(type => typeof(IGamePrefab).IsAssignableFrom(type))
                .Where(type => includeAbstract || (type.IsAbstract == false &&
                                                   type.IsInterface == false &&
                                                   type.GetConstructor(Type.EmptyTypes) != null))
                .OrderBy(type => type.FullName)
                .ToList();
        }

        private static GamePrefabGeneralSetting GetGamePrefabGeneralSetting(Type gamePrefabType)
        {
            foreach (var setting in GetAllGamePrefabGeneralSettings())
            {
                if (setting.BaseGamePrefabType.IsAssignableFrom(gamePrefabType))
                {
                    return setting;
                }
            }

            return null;
        }

        private static List<GamePrefabGeneralSetting> GetAllGamePrefabGeneralSettings()
        {
            return GetAllGeneralSettings()
                .OfType<GamePrefabGeneralSetting>()
                .Where(setting => setting != null)
                .Distinct()
                .ToList();
        }

        private static IEnumerable<IGeneralSetting> GetAllGeneralSettings()
        {
            var seen = new HashSet<Object>();

            foreach (var setting in SafeEnumerable(GlobalSettingCollector.GetAllGeneralSettings))
            {
                if (setting is Object obj)
                {
                    if (seen.Add(obj))
                    {
                        yield return setting;
                    }
                }
                else if (setting != null)
                {
                    yield return setting;
                }
            }

            foreach (string folder in GetGeneralSettingsSearchFolders())
            {
                if (AssetDatabase.IsValidFolder(folder) == false)
                {
                    continue;
                }

                foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (asset is IGeneralSetting setting && seen.Add(asset))
                    {
                        yield return setting;
                    }
                }
            }
        }

        private static IEnumerable<string> GetGeneralSettingsSearchFolders()
        {
            var folders = new[]
            {
                SafeGet(() => EditorSetting.GeneralSettingsAssetFolderPath),
                ConfigurationPath.DEFAULT_GENERAL_SETTINGS_PATH
            };

            return folders.Where(folder => string.IsNullOrWhiteSpace(folder) == false).Distinct();
        }

        private static IEnumerable<GamePrefabWrapper> GetAllGamePrefabWrappers()
        {
            return SafeEnumerable(GamePrefabWrapperQueryTools.GetAllGamePrefabWrappers)
                .Where(wrapper => wrapper != null);
        }

        private static List<GamePrefabInfo> FindGamePrefabInfos(string id, string filter, Type gamePrefabType, int limit)
        {
            var infos = new List<GamePrefabInfo>();
            foreach (var wrapper in GetAllGamePrefabWrappers())
            {
                var gamePrefabs = GetGamePrefabs(wrapper);
                foreach (var gamePrefab in gamePrefabs)
                {
                    if (gamePrefab == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(id) == false &&
                        string.Equals(gamePrefab.id, id, StringComparison.Ordinal) == false)
                    {
                        continue;
                    }

                    if (gamePrefabType != null && gamePrefabType.IsAssignableFrom(gamePrefab.GetType()) == false)
                    {
                        continue;
                    }

                    string wrapperPath = AssetDatabase.GetAssetPath(wrapper);
                    if (MatchesGamePrefabFilter(gamePrefab, wrapper, wrapperPath, filter) == false)
                    {
                        continue;
                    }

                    infos.Add(new GamePrefabInfo
                    {
                        wrapper = wrapper,
                        wrapperPath = wrapperPath,
                        gamePrefab = gamePrefab
                    });

                    if (infos.Count >= limit)
                    {
                        return infos;
                    }
                }
            }

            return infos;
        }

        private static bool MatchesGamePrefabFilter(IGamePrefab gamePrefab, GamePrefabWrapper wrapper,
            string wrapperPath, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return true;
            }

            return MatchesFilter(gamePrefab.id, filter) ||
                   MatchesFilter(gamePrefab.GetType().Name, filter) ||
                   MatchesFilter(gamePrefab.GetType().FullName, filter) ||
                   MatchesFilter(wrapper.name, filter) ||
                   MatchesFilter(wrapperPath, filter);
        }

        private static bool WrapperMatches(GamePrefabWrapper wrapper, string filter)
        {
            if (wrapper == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(filter))
            {
                return true;
            }

            string path = AssetDatabase.GetAssetPath(wrapper);
            if (MatchesFilter(wrapper.name, filter) || MatchesFilter(path, filter))
            {
                return true;
            }

            return GetGamePrefabs(wrapper).Any(gamePrefab => MatchesGamePrefabFilter(gamePrefab, wrapper, path, filter));
        }

        private static List<IGamePrefab> GetGamePrefabs(GamePrefabWrapper wrapper)
        {
            var gamePrefabs = new List<IGamePrefab>();
            if (wrapper == null)
            {
                return gamePrefabs;
            }

            try
            {
                wrapper.GetGamePrefabs(gamePrefabs);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to get GamePrefabs from wrapper '{wrapper.name}': {ex.Message}", wrapper);
            }

            return gamePrefabs;
        }

        private static PanelSource ResolvePanelSource(Dictionary<string, object> args)
        {
            string panelID = GetString(args, "panelID") ?? GetString(args, "id");
            string prefabPath = GetString(args, "prefabPath");

            if (string.IsNullOrWhiteSpace(prefabPath) == false)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    throw new ArgumentException($"Could not load prefab at '{prefabPath}'.");
                }

                return new PanelSource
                {
                    panelID = panelID,
                    prefab = prefab,
                    config = FindPanelConfigByPrefab(prefab)
                };
            }

            if (string.IsNullOrWhiteSpace(panelID))
            {
                throw new ArgumentException("panelID or prefabPath is required.");
            }

            var info = FindGamePrefabInfos(panelID, null, typeof(UIPanelConfig), 1).FirstOrDefault();
            if (info?.gamePrefab is not UIPanelConfig config)
            {
                throw new ArgumentException($"Could not find UIPanelConfig with id '{panelID}'.");
            }

            if (config.prefab == null)
            {
                throw new InvalidOperationException($"UIPanelConfig '{panelID}' has no prefab.");
            }

            return new PanelSource
            {
                panelID = panelID,
                config = config,
                prefab = config.prefab,
                wrapper = info.wrapper
            };
        }

        private static UIPanelConfig FindPanelConfigByPrefab(GameObject prefab)
        {
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            foreach (var info in FindGamePrefabInfos(null, null, typeof(UIPanelConfig), int.MaxValue))
            {
                if (info.gamePrefab is UIPanelConfig config &&
                    config.prefab != null &&
                    AssetDatabase.GetAssetPath(config.prefab) == prefabPath)
                {
                    return config;
                }
            }

            return null;
        }

        private static Dictionary<string, object> DescribePanelConfig(UIPanelConfig config)
        {
            if (config == null)
            {
                return null;
            }

            var result = DescribeGamePrefab(config);
            result["sortingOrder"] = config.sortingOrder;
            result["isUnique"] = config.isUnique;
            result["prefabPath"] = GetAssetPath(config.prefab);

            if (config is UIToolkitPanelConfig toolkitConfig)
            {
                result["useDefaultPanelSettings"] = toolkitConfig.useDefaultPanelSettings;
                result["customPanelSettingsPath"] = GetAssetPath(toolkitConfig.customPanelSettings);
                result["panelSettingsPath"] = GetSafePanelSettingsPath(toolkitConfig);
                result["ignoreMouseEvents"] = toolkitConfig.ignoreMouseEvents;
                result["closeMode"] = toolkitConfig.closeMode.ToString();
            }

            return result;
        }

        private static string GetSafePanelSettingsPath(UIToolkitPanelConfig config)
        {
            try
            {
                return GetAssetPath(config.PanelSettings);
            }
            catch (Exception ex)
            {
                if (config.useDefaultPanelSettings &&
                    GetGamePrefabGeneralSetting(typeof(UIPanelConfig)) is UIPanelGeneralSetting setting)
                {
                    string defaultPanelSettingsPath = GetAssetPath(setting.panelSettings);
                    if (string.IsNullOrWhiteSpace(defaultPanelSettingsPath) == false)
                    {
                        return defaultPanelSettingsPath;
                    }
                }

                return $"<unavailable: {ex.GetType().Name}>";
            }
        }

        private static Dictionary<string, object> DescribePanelPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            var uiDocument = prefab.GetComponentInChildren<UIDocument>(true);
            var modifiers = prefab.GetComponentsInChildren<IPanelModifier>(true)
                .OfType<Component>()
                .Select(DescribeComponent)
                .ToList();

            return new Dictionary<string, object>
            {
                { "name", prefab.name },
                { "path", GetAssetPath(prefab) },
                { "hasUIPanel", prefab.GetComponentInChildren<UIPanel>(true) != null },
                { "uiDocumentPath", uiDocument == null ? "" : GetGameObjectPath(uiDocument.transform) },
                { "visualTreeAssetPath", uiDocument?.visualTreeAsset == null ? "" : GetAssetPath(uiDocument.visualTreeAsset) },
                { "panelSettingsPath", uiDocument?.panelSettings == null ? "" : GetAssetPath(uiDocument.panelSettings) },
                { "bindObjectsManagerCount", prefab.GetComponentsInChildren<BindObjectsManager>(true).Length },
                { "panelModifierCount", modifiers.Count },
                { "panelModifiers", modifiers }
            };
        }

        private static Dictionary<string, object> InspectBindObjects(PanelSource source, bool includeRuntime)
        {
            var managerInfos = source.prefab.GetComponentsInChildren<BindObjectsManager>(true)
                .Select(manager => DescribeBindObjectsManager(manager, includeRuntime))
                .ToList();

            return new Dictionary<string, object>
            {
                { "panelID", source.panelID ?? "" },
                { "prefabPath", GetAssetPath(source.prefab) },
                { "managers", managerInfos },
                { "managerCount", managerInfos.Count },
                { "runtime", InspectRuntimeBindObjects(source.panelID, includeRuntime) }
            };
        }

        private static Dictionary<string, object> DescribeBindObjectsManager(BindObjectsManager manager,
            bool includeRuntime)
        {
            var names = new HashSet<string> { BindObjectsManager.GLOBAL_BIND_NAME };
            var singleModeNames = new HashSet<string>();
            var providers = new List<Dictionary<string, object>>();

            foreach (var provider in manager.GetComponentsInChildren<IBindObjectsNamesProvider>(true))
            {
                var beforeNames = names.Count;
                var beforeSingle = singleModeNames.Count;
                try
                {
                    provider.GetBindObjectsNames(names, singleModeNames);
                }
                catch (Exception ex)
                {
                    providers.Add(new Dictionary<string, object>
                    {
                        { "type", provider.GetType().FullName },
                        { "error", ex.Message }
                    });
                    continue;
                }

                providers.Add(new Dictionary<string, object>
                {
                    { "type", provider.GetType().FullName },
                    { "gameObjectPath", provider is Component component ? GetGameObjectPath(component.transform) : "" },
                    { "addedNameCount", names.Count - beforeNames },
                    { "addedSingleModeNameCount", singleModeNames.Count - beforeSingle },
                    { "details", DescribeBindObjectsNamesProvider(provider) }
                });
            }

            return new Dictionary<string, object>
            {
                { "gameObjectPath", GetGameObjectPath(manager.transform) },
                { "type", manager.GetType().FullName },
                { "names", names.OrderBy(name => name).ToList() },
                { "singleModeNames", singleModeNames.OrderBy(name => name).ToList() },
                { "providers", providers },
                { "isInitialized", manager.IsInitialized },
                { "runtimeObjectCounts", includeRuntime && manager.IsInitialized ? DescribeBindObjectCounts(manager, names) : null }
            };
        }

        private static Dictionary<string, object> DescribeBindObjectsNamesProvider(IBindObjectsNamesProvider provider)
        {
            if (provider is PreDefinedBindObjectsNames predefined)
            {
                return new Dictionary<string, object>
                {
                    { "names", predefined.names.ToArray() },
                    { "singleModeNames", predefined.singleModeNames.ToArray() },
                    { "useGameObjectNames", predefined.useGameObjectNames }
                };
            }

            return new Dictionary<string, object>();
        }

        private static Dictionary<string, object> InspectRuntimePanel(string panelID, bool includeRuntime)
        {
            if (includeRuntime == false || Application.isPlaying == false || string.IsNullOrWhiteSpace(panelID))
            {
                return null;
            }

            var panel = GetRuntimePanel(panelID);
            if (panel == null)
            {
                return new Dictionary<string, object>
                {
                    { "found", false },
                    { "isPlaying", Application.isPlaying }
                };
            }

            return new Dictionary<string, object>
            {
                { "found", true },
                { "id", panel.id },
                { "type", panel.GetType().FullName },
                { "isOpened", panel.IsOpened },
                { "isClosing", panel.IsClosing },
                { "uiEnabled", panel.UIEnabled },
                { "modifierCount", panel.Modifiers.Count },
                { "bindObjects", panel.BindObjectsManager == null ? null : DescribeBindObjectsManager(panel.BindObjectsManager, includeRuntime: true) }
            };
        }

        private static Dictionary<string, object> InspectRuntimeBindObjects(string panelID, bool includeRuntime)
        {
            if (includeRuntime == false || Application.isPlaying == false || string.IsNullOrWhiteSpace(panelID))
            {
                return null;
            }

            var panel = GetRuntimePanel(panelID);
            if (panel?.BindObjectsManager == null)
            {
                return new Dictionary<string, object> { { "found", false } };
            }

            return DescribeBindObjectsManager(panel.BindObjectsManager, includeRuntime: true);
        }

        private static IUIPanel GetRuntimePanel(string panelID)
        {
            return SafeGet(() =>
            {
                var manager = UIPanelManager.Instance;
                return manager != null && manager.TryGetUniquePanel(panelID, out var panel) ? panel : null;
            });
        }

        private static Dictionary<string, object> DescribeBindObjectCounts(BindObjectsManager manager,
            IEnumerable<string> names)
        {
            var counts = new Dictionary<string, object>();
            foreach (string name in names)
            {
                var objects = manager.GetObjects(name);
                counts[name] = new Dictionary<string, object>
                {
                    { "count", objects.Count },
                    { "objects", objects.Take(20).Select(DescribeRuntimeObject).ToList() }
                };
            }

            return counts;
        }

        private static Dictionary<string, object> DescribeContainerModifier(UIToolkitContainerModifierBase modifier,
            bool includeRuntime)
        {
            var configs = modifier.slotDistributorConfigs
                .Select(DescribeContainerSlotDistributorConfig)
                .ToList();

            return new Dictionary<string, object>
            {
                { "type", modifier.GetType().FullName },
                { "gameObjectPath", GetGameObjectPath(modifier.transform) },
                { "bindObjectsName", modifier.bindObjectsName ?? "" },
                { "slotDistributorConfigs", configs },
                { "slotDistributorConfigCount", configs.Count },
                { "isInitialized", modifier.IsInitialized },
                { "runtime", includeRuntime && modifier.IsInitialized ? DescribeRuntimeContainerModifier(modifier) : null }
            };
        }

        private static Dictionary<string, object> DescribeContainerSlotDistributorConfig(
            ContainerSlotDistributorConfig config)
        {
            var result = new Dictionary<string, object>
            {
                { "parentName", config.parentName ?? "" },
                { "findMethod", config.findMethod.ToString() },
                { "slotName", config.slotName ?? "" },
                { "removeExtraSlots", config.removeExtraSlots },
                { "isFinite", config.isFinite },
                { "startSlotIndex", config.startSlotIndex },
                { "slotIndexRange", DescribeRange(config.slotIndexRange) },
                { "autoFill", config.autoFill },
                { "hasCustomContainer", config.hasCustomContainer },
                { "customContainerName", config.customContainerName ?? "" },
                { "containerName", config.ContainerName ?? "" },
                { "startIndex", config.StartIndex },
                { "count", config.Count == int.MaxValue ? "int.MaxValue" : config.Count.ToString(CultureInfo.InvariantCulture) }
            };

            result["slotNameBindings"] = config.slotNameBindings
                .Select(binding => new Dictionary<string, object>
                {
                    { "slotName", binding.slotName ?? "" },
                    { "slotIndex", binding.slotIndex }
                })
                .ToList();

            return result;
        }

        private static Dictionary<string, object> DescribeRuntimeContainerModifier(UIToolkitContainerModifierBase modifier)
        {
            IContainer container = null;
            try
            {
                container = modifier.Panel?.BindObjectsManager?.GetObject(modifier.bindObjectsName) as IContainer;
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    { "error", ex.Message },
                    { "slotCount", modifier.Slots.Count }
                };
            }

            return new Dictionary<string, object>
            {
                { "slotCount", modifier.Slots.Count },
                { "container", DescribeContainer(container) }
            };
        }

        private static Dictionary<string, object> DescribePropertyManager(PropertyManager manager,
            string propertyName)
        {
            var properties = manager.Properties
                .Where(pair => string.IsNullOrWhiteSpace(propertyName) || pair.Key == propertyName)
                .Select(pair => DescribeProperty(pair.Key, pair.Value))
                .ToList();

            return new Dictionary<string, object>
            {
                { "gameObjectPath", GetGameObjectPath(manager.transform) },
                { "type", manager.GetType().FullName },
                { "propertyCount", manager.Properties.Count },
                { "reportedPropertyCount", properties.Count },
                { "properties", properties }
            };
        }

        private static Dictionary<string, object> DescribeProperty(string name, IReadOnlyProperty property)
        {
            object value = null;
            string valueError = "";
            try
            {
                value = property.ObjectValue;
            }
            catch (Exception ex)
            {
                valueError = ex.Message;
            }

            return new Dictionary<string, object>
            {
                { "name", name },
                { "type", property.GetType().FullName },
                { "owner", DescribeRuntimeObject(property.Owner) },
                { "value", DescribeValue(value) },
                { "valueError", valueError }
            };
        }

        private static Dictionary<string, object> ValidateVisualElementPath(VisualElement root,
            VisualElementPathRecord record)
        {
            var names = record.path?.names ?? new List<string>();
            string joinedPath = string.Join("/", names);
            var result = new Dictionary<string, object>
            {
                { "owner", record.owner },
                { "member", record.member },
                { "path", joinedPath },
                { "required", record.required },
                { "expectedTypes", record.allowedTypes.Select(type => type.Name).ToArray() }
            };

            if (record.path == null || names.Count == 0)
            {
                result["valid"] = record.required == false;
                if (record.required)
                {
                    result["error"] = "Required VisualElementPath is empty.";
                }
                else
                {
                    result["skipped"] = true;
                    result["reason"] = "Optional VisualElementPath is empty.";
                }

                return result;
            }

            var element = record.path.Query(root);
            if (element == null)
            {
                result["valid"] = false;
                result["error"] = "VisualElementPath was not found.";
                return result;
            }

            if (record.allowedTypes.Count > 0 && record.allowedTypes.Any(type => type.IsInstanceOfType(element)) == false)
            {
                result["valid"] = false;
                result["error"] = $"VisualElement type '{element.GetType().Name}' does not match expected type.";
                result["actualType"] = element.GetType().Name;
                result["actualName"] = element.name;
                return result;
            }

            result["valid"] = true;
            result["actualType"] = element.GetType().Name;
            result["actualName"] = element.name;
            result["classList"] = element.GetClasses().ToArray();
            return result;
        }

        private static void ScanVisualElementPaths(object target, string owner,
            List<VisualElementPathRecord> records, HashSet<object> visited, int depth,
            VisualElementPathSettingsAttribute inheritedSettings)
        {
            if (target == null || depth > 5)
            {
                return;
            }

            Type targetType = target.GetType();
            if (ShouldRecurseInto(targetType, allowUnityObjectRoot: depth == 0) == false)
            {
                return;
            }

            if (target is not ValueType && visited.Add(target) == false)
            {
                return;
            }

            foreach (var field in GetSerializableFields(targetType))
            {
                object value;
                try
                {
                    value = field.GetValue(target);
                }
                catch
                {
                    continue;
                }

                var settings = field.GetCustomAttribute<VisualElementPathSettingsAttribute>() ?? inheritedSettings;
                string member = field.Name;

                if (value is VisualElementPath path)
                {
                    records.Add(new VisualElementPathRecord
                    {
                        owner = owner,
                        member = member,
                        path = path,
                        required = IsVisualElementPathRequired(field),
                        allowedTypes = GetAllowedTypes(settings)
                    });
                    continue;
                }

                // UnityEngine.Object instances can implement IEnumerable (Transform is the
                // common case), but their serialized object graphs are not nested config data.
                // Enumerating a missing/destroyed reference throws before ShouldRecurseInto can
                // reject it, so stop at every non-root Unity object boundary.
                if (value is Object)
                {
                    continue;
                }

                if (value is IEnumerable enumerable && value is not string)
                {
                    int index = 0;
                    foreach (object item in enumerable)
                    {
                        if (item is VisualElementPath itemPath)
                        {
                            records.Add(new VisualElementPathRecord
                            {
                                owner = owner,
                                member = $"{member}[{index}]",
                                path = itemPath,
                                required = IsVisualElementPathRequired(field),
                                allowedTypes = GetAllowedTypes(settings)
                            });
                        }
                        else
                        {
                            ScanVisualElementPaths(item, owner, records, visited, depth + 1, settings);
                        }

                        index++;
                        if (index > 500)
                        {
                            break;
                        }
                    }

                    continue;
                }

                ScanVisualElementPaths(value, owner, records, visited, depth + 1, settings);
            }
        }

        private static bool IsVisualElementPathRequired(FieldInfo field)
        {
            return field.IsDefined(typeof(IsNotNullOrEmptyAttribute), true);
        }

        private static IEnumerable<FieldInfo> GetSerializableFields(Type type)
        {
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                if (current == typeof(MonoBehaviour) ||
                    current == typeof(Behaviour) ||
                    current == typeof(Component) ||
                    current == typeof(Object))
                {
                    yield break;
                }

                foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsStatic)
                    {
                        continue;
                    }

                    if (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
                    {
                        yield return field;
                    }
                }
            }
        }

        private static bool ShouldRecurseInto(Type type, bool allowUnityObjectRoot = false)
        {
            if (type == null || type.IsPrimitive || type.IsEnum || type == typeof(string) ||
                type == typeof(decimal))
            {
                return false;
            }

            if (typeof(Object).IsAssignableFrom(type))
            {
                return allowUnityObjectRoot && typeof(Component).IsAssignableFrom(type);
            }

            if (type == typeof(VisualElementPath))
            {
                return true;
            }

            string ns = type.Namespace ?? "";
            if (ns.StartsWith("System", StringComparison.Ordinal) ||
                ns.StartsWith("Unity", StringComparison.Ordinal) ||
                ns.StartsWith("Microsoft", StringComparison.Ordinal) ||
                ns.StartsWith("Sirenix", StringComparison.Ordinal) ||
                ns.StartsWith("Newtonsoft", StringComparison.Ordinal))
            {
                return false;
            }

            return ns.Length > 0 || type.GetCustomAttribute<SerializableAttribute>() != null;
        }

        private static List<Type> GetAllowedTypes(VisualElementPathSettingsAttribute settings)
        {
            if (settings?.AllowedTypes == null)
            {
                return new List<Type> { typeof(VisualElement) };
            }

            return settings.AllowedTypes.Where(type => type != null).ToList();
        }

        private static VisualTreeAsset GetVisualTreeAsset(GameObject prefab)
        {
            var uiDocument = prefab == null ? null : prefab.GetComponentInChildren<UIDocument>(true);
            return uiDocument == null ? null : uiDocument.visualTreeAsset;
        }

        private static void AddPropertyManagers(GameObject gameObject, ICollection<PropertyManager> managers,
            bool includeChildren)
        {
            if (includeChildren)
            {
                foreach (var manager in gameObject.GetComponentsInChildren<PropertyManager>(true))
                {
                    managers.Add(manager);
                }
            }
            else if (gameObject.TryGetComponent(out PropertyManager manager))
            {
                managers.Add(manager);
            }
        }

        private static GameObject FindSceneGameObject(string pathOrName)
        {
            foreach (var root in GetSceneRoots())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    string path = GetGameObjectPath(transform);
                    if (string.Equals(path, pathOrName, StringComparison.Ordinal) ||
                        string.Equals(path.TrimStart('/'), pathOrName.TrimStart('/'), StringComparison.Ordinal) ||
                        string.Equals(transform.name, pathOrName, StringComparison.Ordinal))
                    {
                        return transform.gameObject;
                    }
                }
            }

            return null;
        }

        private static IEnumerable<GameObject> GetSceneRoots()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded == false)
                {
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    yield return root;
                }
            }
        }

        private static Dictionary<string, object> DescribeGamePrefabInfo(GamePrefabInfo info)
        {
            return new Dictionary<string, object>
            {
                { "gamePrefab", DescribeGamePrefab(info.gamePrefab) },
                { "wrapperPath", info.wrapperPath },
                { "wrapperName", info.wrapper == null ? "" : info.wrapper.name },
                { "generalSetting", DescribeGeneralSetting(GetGamePrefabGeneralSetting(info.gamePrefab.GetType()), includeGamePrefabDetails: false) }
            };
        }

        private static Dictionary<string, object> DescribeGamePrefab(IGamePrefab gamePrefab)
        {
            if (gamePrefab == null)
            {
                return null;
            }

            return new Dictionary<string, object>
            {
                { "id", gamePrefab.id ?? "" },
                { "name", gamePrefab.Name ?? "" },
                { "type", gamePrefab.GetType().FullName },
                { "gameItemType", gamePrefab.GameItemType == null ? "" : gamePrefab.GameItemType.FullName },
                { "isActive", gamePrefab.IsActive },
                { "isDebugging", gamePrefab.IsDebugging },
                { "gameItemPrewarmCount", gamePrefab.GameItemPrewarmCount },
                { "idPrefix", gamePrefab.IDPrefix ?? "" },
                { "idSuffix", gamePrefab.IDSuffix ?? "" }
            };
        }

        private static Dictionary<string, object> DescribeWrapper(GamePrefabWrapper wrapper,
            bool includeGamePrefabs)
        {
            if (wrapper == null)
            {
                return null;
            }

            var result = new Dictionary<string, object>
            {
                { "name", wrapper.name },
                { "id", wrapper.id ?? "" },
                { "type", wrapper.GetType().FullName },
                { "path", GetAssetPath(wrapper) }
            };

            if (includeGamePrefabs)
            {
                var gamePrefabs = GetGamePrefabs(wrapper)
                    .Where(gamePrefab => gamePrefab != null)
                    .Select(DescribeGamePrefab)
                    .ToList();
                result["gamePrefabs"] = gamePrefabs;
                result["gamePrefabCount"] = gamePrefabs.Count;
            }

            return result;
        }

        private static Dictionary<string, object> DescribeGeneralSetting(IGeneralSetting setting,
            bool includeGamePrefabDetails)
        {
            if (setting == null)
            {
                return null;
            }

            var obj = setting as Object;
            var result = new Dictionary<string, object>
            {
                { "name", obj == null ? setting.GetType().Name : obj.name },
                { "type", setting.GetType().FullName },
                { "path", obj == null ? "" : GetAssetPath(obj) },
                { "isGamePrefabGeneralSetting", setting is GamePrefabGeneralSetting }
            };

            if (includeGamePrefabDetails && setting is GamePrefabGeneralSetting gamePrefabSetting)
            {
                var providers = DescribeGamePrefabProviders(gamePrefabSetting.initialGamePrefabProviders,
                    out int providerSlotCount, out int missingProviderCount);

                result["gamePrefabName"] = gamePrefabSetting.GamePrefabName;
                result["baseGamePrefabType"] = gamePrefabSetting.BaseGamePrefabType.FullName;
                result["gamePrefabFolderPath"] = gamePrefabSetting.GamePrefabFolderPath;
                result["initialGamePrefabProviderSlotCount"] = providerSlotCount;
                result["initialGamePrefabProviderCount"] = providers.Count;
                result["missingInitialGamePrefabProviderCount"] = missingProviderCount;
                result["initialGamePrefabProviders"] = providers;
            }

            return result;
        }

        private static List<Dictionary<string, object>> DescribeGamePrefabProviders(
            IEnumerable<IGamePrefabsProvider> rawProviders, out int providerSlotCount, out int missingProviderCount)
        {
            var providers = new List<Dictionary<string, object>>();
            providerSlotCount = 0;
            missingProviderCount = 0;

            if (rawProviders == null)
            {
                return providers;
            }

            foreach (var rawProvider in rawProviders)
            {
                providerSlotCount++;
                if (rawProvider is not Object provider || provider == null)
                {
                    missingProviderCount++;
                    continue;
                }

                providers.Add(new Dictionary<string, object>
                {
                    { "name", provider.name },
                    { "type", provider.GetType().FullName },
                    { "path", GetAssetPath(provider) }
                });
            }

            return providers;
        }

        private static Dictionary<string, object> DescribeComponent(Component component)
        {
            return new Dictionary<string, object>
            {
                { "type", component.GetType().FullName },
                { "gameObjectPath", GetGameObjectPath(component.transform) },
                { "enabled", component is Behaviour behaviour ? (object)behaviour.enabled : null }
            };
        }

        private static Dictionary<string, object> DescribeRuntimeObject(object obj)
        {
            if (obj == null)
            {
                return null;
            }

            if (obj is Object unityObject)
            {
                return new Dictionary<string, object>
                {
                    { "type", unityObject.GetType().FullName },
                    { "name", unityObject.name },
                    { "path", unityObject is Component component ? GetGameObjectPath(component.transform) : GetAssetPath(unityObject) },
                    { "instanceID", unityObject.GetInstanceID() }
                };
            }

            return new Dictionary<string, object>
            {
                { "type", obj.GetType().FullName },
                { "text", obj.ToString() }
            };
        }

        private static object DescribeValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is string || value.GetType().IsPrimitive || value is decimal)
            {
                return value;
            }

            if (value is Object unityObject)
            {
                return DescribeRuntimeObject(unityObject);
            }

            if (value is IContainer container)
            {
                return DescribeContainer(container);
            }

            if (value is IEnumerable enumerable)
            {
                var items = new List<object>();
                int count = 0;
                foreach (object item in enumerable)
                {
                    count++;
                    if (items.Count < 20)
                    {
                        items.Add(DescribeValue(item));
                    }
                }

                return new Dictionary<string, object>
                {
                    { "type", value.GetType().FullName },
                    { "count", count },
                    { "items", items }
                };
            }

            return new Dictionary<string, object>
            {
                { "type", value.GetType().FullName },
                { "text", value.ToString() }
            };
        }

        private static Dictionary<string, object> DescribeContainer(IContainer container)
        {
            if (container == null)
            {
                return null;
            }

            return new Dictionary<string, object>
            {
                { "type", container.GetType().FullName },
                { "id", container.id ?? "" },
                { "capacity", container.Capacity.HasValue ? (object)container.Capacity.Value : null },
                { "count", container.Count },
                { "validCount", container.ValidCount },
                { "isFull", container.IsFull },
                { "validSlotIndices", container.ValidSlotIndices.Take(100).ToArray() },
                { "validItems", container.ValidItems.Take(20).Select(DescribeRuntimeObject).ToList() }
            };
        }

        private static Dictionary<string, object> DescribeRange(object range)
        {
            if (range == null)
            {
                return null;
            }

            var type = range.GetType();
            return new Dictionary<string, object>
            {
                { "type", type.FullName },
                { "min", ReadFieldOrProperty(range, "min") },
                { "max", ReadFieldOrProperty(range, "max") },
                { "count", ReadFieldOrProperty(range, "Count") }
            };
        }

        private static object ReadFieldOrProperty(object target, string memberName)
        {
            var type = target.GetType();
            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return field.GetValue(target);
            }

            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property == null ? null : property.GetValue(target);
        }

        private static void ApplySerializedValues(object target, Dictionary<string, object> values)
        {
            foreach (var pair in values)
            {
                string memberName = pair.Key;
                object rawValue = pair.Value;
                if (memberName == nameof(GamePrefab.id) || memberName == "_id")
                {
                    throw new InvalidOperationException("Use the root id argument instead of serializedValues.id.");
                }

                if (TrySetProperty(target, memberName, rawValue))
                {
                    continue;
                }

                if (TrySetField(target, memberName, rawValue))
                {
                    continue;
                }

                throw new MissingMemberException(target.GetType().FullName, memberName);
            }
        }

        private static bool TrySetProperty(object target, string memberName, object rawValue)
        {
            var property = target.GetType().GetProperty(memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (property == null)
            {
                return false;
            }

            if (property.CanWrite == false || property.GetIndexParameters().Length != 0)
            {
                throw new InvalidOperationException(
                    $"Property '{memberName}' on '{target.GetType().FullName}' is not writable.");
            }

            property.SetValue(target, ConvertValue(rawValue, property.PropertyType, memberName));
            return true;
        }

        private static bool TrySetField(object target, string memberName, object rawValue)
        {
            var field = target.GetType().GetField(memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field == null)
            {
                return false;
            }

            field.SetValue(target, ConvertValue(rawValue, field.FieldType, memberName));
            return true;
        }

        private static object ConvertValue(object value, Type targetType, string memberName)
        {
            if (value == null)
            {
                return null;
            }

            Type nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
            {
                targetType = nullableType;
            }

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            if (targetType == typeof(string))
            {
                return value.ToString();
            }

            if (targetType == typeof(bool))
            {
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(int))
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(long))
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(float))
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(double))
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }

            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, value.ToString(), true);
            }

            if (typeof(Object).IsAssignableFrom(targetType))
            {
                return ConvertUnityObject(value, targetType, memberName);
            }

            if (TryConvertStringCollection(value, targetType, out var stringCollection))
            {
                return stringCollection;
            }

            throw new InvalidOperationException(
                $"Cannot convert value for '{memberName}' to '{targetType.FullName}'.");
        }

        private static object ConvertUnityObject(object value, Type targetType, string memberName)
        {
            if (value is not string assetPath || string.IsNullOrWhiteSpace(assetPath))
            {
                throw new InvalidOperationException(
                    $"Unity object field '{memberName}' must be set with an asset path string.");
            }

            var asset = AssetDatabase.LoadAssetAtPath(assetPath, targetType);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Could not load asset '{assetPath}' as '{targetType.FullName}' for '{memberName}'.");
            }

            return asset;
        }

        private static bool TryConvertStringCollection(object value, Type targetType, out object collection)
        {
            collection = null;

            if (targetType == typeof(HashSet<string>))
            {
                collection = new HashSet<string>(GetStringValues(value));
                return true;
            }

            if (targetType == typeof(List<string>))
            {
                collection = GetStringValues(value).ToList();
                return true;
            }

            return false;
        }

        private static IEnumerable<string> GetStringValues(object value)
        {
            if (value is string str)
            {
                yield return str;
                yield break;
            }

            if (value is not IEnumerable enumerable)
            {
                throw new InvalidOperationException("Expected a string or string array.");
            }

            foreach (object item in enumerable)
            {
                if (item != null)
                {
                    yield return item.ToString();
                }
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null);
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static IEnumerable<T> SafeEnumerable<T>(Func<IEnumerable<T>> getter)
        {
            IEnumerable<T> values;
            try
            {
                values = getter();
            }
            catch
            {
                yield break;
            }

            foreach (var value in values)
            {
                yield return value;
            }
        }

        private static T SafeGet<T>(Func<T> getter)
        {
            try
            {
                return getter();
            }
            catch
            {
                return default;
            }
        }

        private static void RefreshGamePrefabRegistry()
        {
            AssetDatabase.Refresh();
            GamePrefabWrapperInitializeUtility.Refresh();
        }

        private static bool MatchesFilter(string value, string filter)
        {
            return string.IsNullOrWhiteSpace(filter) ||
                   (value != null && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string CombineAssetPath(string folderPath, string assetName)
        {
            folderPath = (folderPath ?? "").Replace("\\", "/").TrimEnd('/');
            assetName = (assetName ?? "").Replace("\\", "/").TrimStart('/');
            return $"{folderPath}/{assetName}";
        }

        private static string ToPascalAssetName(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return "New GamePrefab";
            }

            var parts = id.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join("", parts.Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }

        private static string GetAssetPath(Object obj)
        {
            return obj == null ? "" : AssetDatabase.GetAssetPath(obj);
        }

        private static string GetGameObjectPath(Transform transform)
        {
            if (transform == null)
            {
                return "";
            }

            var names = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private static string GetRequiredString(Dictionary<string, object> args, string key)
        {
            string value = GetString(args, key);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{key} is required.");
            }

            return value;
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            if (args.TryGetValue(key, out object value) == false || value == null)
            {
                return null;
            }

            return value.ToString();
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args.TryGetValue(key, out object value) == false || value == null)
            {
                return defaultValue;
            }

            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args.TryGetValue(key, out object value) == false || value == null)
            {
                return defaultValue;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> args, string key)
        {
            if (args.TryGetValue(key, out object value) == false || value == null)
            {
                return null;
            }

            return value as Dictionary<string, object>;
        }

        private sealed class PanelSource
        {
            public string panelID;
            public UIPanelConfig config;
            public GameObject prefab;
            public GamePrefabWrapper wrapper;
        }

        private sealed class GamePrefabInfo
        {
            public GamePrefabWrapper wrapper;
            public string wrapperPath;
            public IGamePrefab gamePrefab;
        }

        private sealed class VisualElementPathRecord
        {
            public string owner;
            public string member;
            public VisualElementPath path;
            public bool required;
            public List<Type> allowedTypes;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
#endif
