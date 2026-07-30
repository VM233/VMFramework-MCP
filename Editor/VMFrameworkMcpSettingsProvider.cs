#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;

namespace VMFramework.MCP.Editor
{
    internal static class VMFrameworkMcpSettingsProvider
    {
        private const string UserPreferencesPath = "Preferences/VMFramework MCP";
        private const string ProjectSettingsPath = "Project/VMFramework MCP";

        private static string projectWriteError = "";

        [SettingsProvider]
        public static SettingsProvider CreateUserPreferencesProvider()
        {
            return new SettingsProvider(UserPreferencesPath, SettingsScope.User)
            {
                label = "VMFramework MCP",
                guiHandler = _ => DrawUserPreferences(),
                keywords = new HashSet<string>
                {
                    "VMFramework", "MCP", "GamePrefab", "inspection", "depth",
                    "collection", "snapshot", "property trace", "result limit",
                },
            };
        }

        [SettingsProvider]
        public static SettingsProvider CreateProjectSettingsProvider()
        {
            return new SettingsProvider(ProjectSettingsPath, SettingsScope.Project)
            {
                label = "VMFramework MCP",
                guiHandler = _ => DrawProjectSettings(),
                keywords = new HashSet<string>
                {
                    "VMFramework", "MCP", "GameTag", "validation", "translation",
                    "GamePrefab", "references", "ProjectSettings",
                },
            };
        }

        private static void DrawUserPreferences()
        {
            EditorGUILayout.LabelField("GamePrefab Inspection", EditorStyles.boldLabel);

            int depth = EditorGUILayout.IntSlider(
                new GUIContent(
                    "Default Max Depth",
                    "Default nested GamePrefab inspection depth when maxDepth is omitted. Explicit tool arguments win."),
                VMFrameworkMcpSettingsManager.GamePrefabInspectionMaxDepth, 1, 16);
            if (depth != VMFrameworkMcpSettingsManager.GamePrefabInspectionMaxDepth)
                VMFrameworkMcpSettingsManager.GamePrefabInspectionMaxDepth = depth;

            int items = EditorGUILayout.IntField(
                new GUIContent(
                    "Collection Item Limit",
                    "Default items retained per inspected collection when maxCollectionItems is omitted. Explicit tool arguments win."),
                VMFrameworkMcpSettingsManager.GamePrefabCollectionItemLimit);
            items = Mathf.Clamp(items, 1, 1000);
            if (items != VMFrameworkMcpSettingsManager.GamePrefabCollectionItemLimit)
                VMFrameworkMcpSettingsManager.GamePrefabCollectionItemLimit = items;

            bool snapshots = EditorGUILayout.Toggle(
                new GUIContent(
                    "Include Update Snapshots",
                    "Include complete before and after GamePrefab snapshots when includeSnapshots is omitted. Disabled by default; operation summaries and the semantic diff are still returned."),
                VMFrameworkMcpSettingsManager.IncludeGamePrefabUpdateSnapshots);
            if (snapshots != VMFrameworkMcpSettingsManager.IncludeGamePrefabUpdateSnapshots)
                VMFrameworkMcpSettingsManager.IncludeGamePrefabUpdateSnapshots = snapshots;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Property Trace", EditorStyles.boldLabel);

            int traceEvents = EditorGUILayout.IntField(
                new GUIContent(
                    "Retained Event Limit",
                    "Default trace ring-buffer capacity when maxEvents is omitted. Explicit tool arguments win."),
                VMFrameworkMcpSettingsManager.PropertyTraceMaxEvents);
            traceEvents = Mathf.Clamp(traceEvents, 1, 10000);
            if (traceEvents != VMFrameworkMcpSettingsManager.PropertyTraceMaxEvents)
                VMFrameworkMcpSettingsManager.PropertyTraceMaxEvents = traceEvents;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Shared Result Budget", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                MCPSettingsManager.OverrideDefaultResultLimit
                    ? $"Unity MCP currently overrides single-collection defaults with {MCPSettingsManager.DefaultResultLimit} results."
                    : "Single-collection tools use their package defaults. Enable the shared override in Unity MCP preferences to use one personal result budget.",
                MessageType.None);
            if (GUILayout.Button("Open Unity MCP Tool Response Preferences"))
                SettingsService.OpenUserPreferences("Preferences/Unity MCP");

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Reset User Preferences to Defaults") &&
                EditorUtility.DisplayDialog(
                    "Reset User Preferences",
                    "Reset VMFramework MCP user preferences to defaults?",
                    "Reset",
                    "Cancel"))
            {
                VMFrameworkMcpSettingsManager.ResetUserPreferencesToDefaults();
            }
        }

        private static void DrawProjectSettings()
        {
            var configuration = VMFrameworkMcpSettingsManager.GetProjectConfiguration();
            if (!configuration.Valid)
            {
                EditorGUILayout.HelpBox(
                    $"{VMFrameworkMcpProjectConfiguration.ConfigPath}: {configuration.Error}",
                    MessageType.Error);
                if (GUILayout.Button("Replace Invalid Project Settings with Defaults") &&
                    EditorUtility.DisplayDialog(
                        "Replace Invalid Project Settings",
                        $"Replace {VMFrameworkMcpProjectConfiguration.ConfigPath} with VMFramework MCP defaults?",
                        "Replace",
                        "Cancel"))
                {
                    TryWrite(VMFrameworkMcpSettingsManager.ResetProjectSettingsToDefaults);
                }
                return;
            }

            EditorGUILayout.HelpBox(
                configuration.Found
                    ? $"Team settings are stored in {VMFrameworkMcpProjectConfiguration.ConfigPath}."
                    : $"Changing a team setting will create {VMFrameworkMcpProjectConfiguration.ConfigPath}.",
                MessageType.None);

            EditorGUILayout.LabelField("GameTag Validation", EditorStyles.boldLabel);
            bool missingTranslations = EditorGUILayout.Toggle(
                new GUIContent(
                    "Missing Translations",
                    "Default validation coverage for missing or empty locale values. Explicit tool arguments win."),
                VMFrameworkMcpSettingsManager.IncludeMissingGameTagTranslations);
            if (missingTranslations != VMFrameworkMcpSettingsManager.IncludeMissingGameTagTranslations)
            {
                TryWrite(() =>
                    VMFrameworkMcpSettingsManager.IncludeMissingGameTagTranslations = missingTranslations);
            }

            bool prefabReferences = EditorGUILayout.Toggle(
                new GUIContent(
                    "GamePrefab References",
                    "Default validation coverage for GamePrefab tags that are not registered. Explicit tool arguments win."),
                VMFrameworkMcpSettingsManager.IncludeGamePrefabTagReferences);
            if (prefabReferences != VMFrameworkMcpSettingsManager.IncludeGamePrefabTagReferences)
            {
                TryWrite(() =>
                    VMFrameworkMcpSettingsManager.IncludeGamePrefabTagReferences = prefabReferences);
            }

            if (!string.IsNullOrEmpty(projectWriteError))
                EditorGUILayout.HelpBox(projectWriteError, MessageType.Error);

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Reset Project Settings to Defaults") &&
                EditorUtility.DisplayDialog(
                    "Reset Project Settings",
                    "Reset VMFramework MCP project settings to defaults?",
                    "Reset",
                    "Cancel"))
            {
                TryWrite(VMFrameworkMcpSettingsManager.ResetProjectSettingsToDefaults);
            }
        }

        private static void TryWrite(Action write)
        {
            try
            {
                write();
                projectWriteError = "";
            }
            catch (Exception ex)
            {
                projectWriteError = ex.Message;
            }
        }
    }
}
#endif
