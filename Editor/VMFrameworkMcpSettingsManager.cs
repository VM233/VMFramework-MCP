#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityMCP.Editor;

namespace VMFramework.MCP.Editor
{
    internal static class VMFrameworkMcpSettingsManager
    {
        private const string UserPrefix = "VMFrameworkMCP_user_v1_";

        private static VMFrameworkMcpProjectConfiguration projectConfiguration;
        private static bool projectConfigurationFileExists;
        private static long projectConfigurationWriteTicks;

        internal static int GamePrefabInspectionMaxDepth
        {
            get => Clamp(EditorPrefs.GetInt(UserPrefix + "GamePrefabInspectionMaxDepth", 8), 1, 16);
            set => EditorPrefs.SetInt(UserPrefix + "GamePrefabInspectionMaxDepth", Clamp(value, 1, 16));
        }

        internal static int GamePrefabCollectionItemLimit
        {
            get => Clamp(EditorPrefs.GetInt(UserPrefix + "GamePrefabCollectionItemLimit", 100), 1, 1000);
            set => EditorPrefs.SetInt(UserPrefix + "GamePrefabCollectionItemLimit", Clamp(value, 1, 1000));
        }

        internal static bool IncludeGamePrefabUpdateSnapshots
        {
            get => EditorPrefs.GetBool(UserPrefix + "IncludeGamePrefabUpdateSnapshots", false);
            set => EditorPrefs.SetBool(UserPrefix + "IncludeGamePrefabUpdateSnapshots", value);
        }

        internal static int PropertyTraceMaxEvents
        {
            get => Clamp(EditorPrefs.GetInt(UserPrefix + "PropertyTraceMaxEvents", 1000), 1, 10000);
            set => EditorPrefs.SetInt(UserPrefix + "PropertyTraceMaxEvents", Clamp(value, 1, 10000));
        }

        internal static bool IncludeMissingGameTagTranslations
        {
            get
            {
                var configuration = GetProjectConfiguration();
                return configuration.Found && configuration.Valid
                    ? configuration.IncludeMissingGameTagTranslations
                    : true;
            }
            set => UpdateProjectConfiguration(configuration =>
                configuration.IncludeMissingGameTagTranslations = value);
        }

        internal static bool IncludeGamePrefabTagReferences
        {
            get
            {
                var configuration = GetProjectConfiguration();
                return configuration.Found && configuration.Valid
                    ? configuration.IncludeGamePrefabTagReferences
                    : true;
            }
            set => UpdateProjectConfiguration(configuration =>
                configuration.IncludeGamePrefabTagReferences = value);
        }

        internal static int ResolveResultLimit(Dictionary<string, object> args, string argumentName,
            int builtInDefault, int maximum)
        {
            return MCPSettingsManager.ResolvePrimaryResultLimit(
                args, argumentName, builtInDefault, 1, maximum);
        }

        internal static int ResolvePreferenceInt(Dictionary<string, object> args, string argumentName,
            int preferenceValue, int minimum, int maximum)
        {
            return TryGetInt(args, argumentName, out int value)
                ? Clamp(value, minimum, maximum)
                : Clamp(preferenceValue, minimum, maximum);
        }

        internal static bool ResolvePreferenceBool(Dictionary<string, object> args, string argumentName,
            bool preferenceValue)
        {
            if (args != null && args.TryGetValue(argumentName, out object value) && value != null)
                return Convert.ToBoolean(value);
            return preferenceValue;
        }

        internal static bool ResolveProjectBool(Dictionary<string, object> args, string argumentName,
            bool projectValue)
        {
            if (args != null && args.TryGetValue(argumentName, out object value) && value != null)
                return Convert.ToBoolean(value);
            return projectValue;
        }

        internal static VMFrameworkMcpProjectConfiguration GetProjectConfiguration()
        {
            string path = VMFrameworkMcpProjectConfiguration.GetFullPath();
            bool exists = File.Exists(path);
            long ticks = exists ? File.GetLastWriteTimeUtc(path).Ticks : 0;
            if (projectConfiguration == null ||
                exists != projectConfigurationFileExists ||
                ticks != projectConfigurationWriteTicks)
            {
                projectConfiguration = VMFrameworkMcpProjectConfiguration.Load();
                projectConfigurationFileExists = exists;
                projectConfigurationWriteTicks = ticks;
            }

            return projectConfiguration;
        }

        internal static void ReloadProjectConfiguration()
        {
            projectConfiguration = null;
            GetProjectConfiguration();
        }

        internal static void ResetUserPreferencesToDefaults()
        {
            EditorPrefs.DeleteKey(UserPrefix + "GamePrefabInspectionMaxDepth");
            EditorPrefs.DeleteKey(UserPrefix + "GamePrefabCollectionItemLimit");
            EditorPrefs.DeleteKey(UserPrefix + "IncludeGamePrefabUpdateSnapshots");
            EditorPrefs.DeleteKey(UserPrefix + "PropertyTraceMaxEvents");
        }

        internal static void ResetProjectSettingsToDefaults()
        {
            var configuration = new VMFrameworkMcpProjectConfiguration();
            configuration.Save();
            CacheProjectConfiguration(configuration);
        }

        internal static Dictionary<string, object> GetConfigurationSnapshot()
        {
            var project = GetProjectConfiguration();
            return new Dictionary<string, object>
            {
                {
                    "precedence",
                    new[]
                    {
                        "explicit tool argument",
                        "VMFramework MCP Project Settings",
                        "VMFramework MCP or shared Unity MCP preference",
                        "built-in default",
                    }
                },
                {
                    "projectSettings",
                    new Dictionary<string, object>
                    {
                        { "path", VMFrameworkMcpProjectConfiguration.ConfigPath },
                        { "found", project.Found },
                        { "valid", project.Valid },
                        { "error", project.Error },
                        {
                            "gameTagValidation",
                            new Dictionary<string, object>
                            {
                                { "includeMissingTranslations", IncludeMissingGameTagTranslations },
                                { "includeGamePrefabReferences", IncludeGamePrefabTagReferences },
                            }
                        },
                    }
                },
                {
                    "preferences",
                    new Dictionary<string, object>
                    {
                        { "path", "Preferences > VMFramework MCP" },
                        { "gamePrefabInspectionMaxDepth", GamePrefabInspectionMaxDepth },
                        { "gamePrefabCollectionItemLimit", GamePrefabCollectionItemLimit },
                        { "includeGamePrefabUpdateSnapshots", IncludeGamePrefabUpdateSnapshots },
                        { "propertyTraceMaxEvents", PropertyTraceMaxEvents },
                    }
                },
                {
                    "sharedUnityMcpPreferences",
                    new Dictionary<string, object>
                    {
                        { "path", "Preferences > Unity MCP > Tool Responses" },
                        { "overrideResultDefaults", MCPSettingsManager.OverrideDefaultResultLimit },
                        { "defaultResultLimit", MCPSettingsManager.DefaultResultLimit },
                    }
                },
            };
        }

        private static void UpdateProjectConfiguration(Action<VMFrameworkMcpProjectConfiguration> update)
        {
            var configuration = GetProjectConfiguration();
            if (configuration.Found && !configuration.Valid)
            {
                throw new InvalidOperationException(
                    $"{VMFrameworkMcpProjectConfiguration.ConfigPath}: {configuration.Error}");
            }

            update(configuration);
            configuration.Save();
            CacheProjectConfiguration(configuration);
        }

        private static void CacheProjectConfiguration(VMFrameworkMcpProjectConfiguration configuration)
        {
            projectConfiguration = configuration;
            string path = VMFrameworkMcpProjectConfiguration.GetFullPath();
            projectConfigurationFileExists = File.Exists(path);
            projectConfigurationWriteTicks = projectConfigurationFileExists
                ? File.GetLastWriteTimeUtc(path).Ticks
                : 0;
        }

        private static bool TryGetInt(Dictionary<string, object> args, string key, out int value)
        {
            value = 0;
            if (args == null || !args.TryGetValue(key, out object rawValue) || rawValue == null)
                return false;

            value = Convert.ToInt32(rawValue);
            return true;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
#endif
