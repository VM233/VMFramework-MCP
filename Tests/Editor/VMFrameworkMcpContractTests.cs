#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityMCP.Editor;

namespace VMFramework.MCP.Editor.Tests
{
    public class VMFrameworkMcpContractTests
    {
        private static readonly string[] ExpectedToolNames =
        {
            "vmframework/add-game-prefab",
            "vmframework/find-game-prefab",
            "vmframework/get-configuration",
            "vmframework/get-property",
            "vmframework/get-property-trace",
            "vmframework/inspect-bind-objects",
            "vmframework/inspect-container-panel",
            "vmframework/inspect-game-prefab",
            "vmframework/inspect-game-prefab-wrapper",
            "vmframework/inspect-property-manager",
            "vmframework/inspect-ui-panel",
            "vmframework/list-game-prefab-types",
            "vmframework/list-game-tags",
            "vmframework/list-general-settings",
            "vmframework/set-property",
            "vmframework/start-property-trace",
            "vmframework/stop-property-trace",
            "vmframework/update-game-prefab",
            "vmframework/upsert-game-tag",
            "vmframework/validate-game-tags",
            "vmframework/validate-visual-element-paths",
        };

        [Test]
        public void ProjectToolCatalog_IsCompleteStrictAndThreeStage()
        {
            var tools = MCPProjectToolCommands.GetToolDetails(false)
                .Where(tool => GetString(tool, "toolName").StartsWith(
                    "vmframework/", StringComparison.Ordinal))
                .OrderBy(tool => GetString(tool, "toolName"), StringComparer.Ordinal)
                .ToList();

            CollectionAssert.AreEqual(
                ExpectedToolNames.OrderBy(name => name, StringComparer.Ordinal),
                tools.Select(tool => GetString(tool, "toolName")));

            foreach (var tool in tools)
            {
                string toolName = GetString(tool, "toolName");
                Assert.That(tool["valid"], Is.EqualTo(true), toolName);
                Assert.That(tool["firstClass"], Is.EqualTo(false),
                    $"{toolName} must stay behind project-tools/list|get|execute.");

                int operationKinds =
                    (Convert.ToBoolean(tool["readOnly"]) ? 1 : 0) +
                    (Convert.ToBoolean(tool["mutatesAssets"]) ? 1 : 0) +
                    (Convert.ToBoolean(tool["mutatesRuntime"]) ? 1 : 0);
                Assert.That(operationKinds, Is.EqualTo(1), toolName);

                var schema = RequireDictionary(tool["inputSchema"]);
                Assert.That(schema["additionalProperties"], Is.EqualTo(false),
                    $"{toolName} must reject unknown business arguments.");
            }
        }

        [Test]
        public void RuntimePropertyTools_DeclareAccurateOperationMetadataAndSchemas()
        {
            var details = MCPProjectToolCommands.GetToolDetails(false)
                .Where(tool => GetString(tool, "toolName").StartsWith(
                    "vmframework/", StringComparison.Ordinal))
                .ToDictionary(tool => GetString(tool, "toolName"));

            var setProperty = details["vmframework/set-property"];
            Assert.That(setProperty["mutatesRuntime"], Is.EqualTo(true));
            Assert.That(setProperty["requiresPlayMode"], Is.EqualTo(true));

            var startTrace = details["vmframework/start-property-trace"];
            Assert.That(startTrace["mutatesRuntime"], Is.EqualTo(true));
            var startProperties = RequireDictionary(
                RequireDictionary(startTrace["inputSchema"])["properties"]);
            Assert.That(startProperties.ContainsKey("maxEvents"), Is.True);
            Assert.That(startProperties.ContainsKey("clear"), Is.False);

            var getTrace = details["vmframework/get-property-trace"];
            Assert.That(getTrace["readOnly"], Is.EqualTo(true));
            var readProperties = RequireDictionary(
                RequireDictionary(getTrace["inputSchema"])["properties"]);
            Assert.That(readProperties.Keys, Is.EquivalentTo(new[] { "offset", "limit" }));

            var stopTrace = details["vmframework/stop-property-trace"];
            Assert.That(stopTrace["mutatesRuntime"], Is.EqualTo(true));
            Assert.That(
                RequireDictionary(RequireDictionary(stopTrace["inputSchema"])["properties"]).Keys,
                Is.EquivalentTo(new[] { "offset", "limit" }));
        }

        [Test]
        public void PanelTools_RequireAnUnambiguousSelector_AndValidationSupportsAllPanels()
        {
            var details = MCPProjectToolCommands.GetToolDetails(false)
                .Where(tool => GetString(tool, "toolName").StartsWith(
                    "vmframework/", StringComparison.Ordinal))
                .ToDictionary(tool => GetString(tool, "toolName"));

            foreach (string toolName in new[]
                     {
                         "vmframework/inspect-ui-panel",
                         "vmframework/inspect-bind-objects",
                         "vmframework/inspect-container-panel"
                     })
            {
                var schema = RequireDictionary(details[toolName]["inputSchema"]);
                Assert.That(schema["oneOf"], Is.InstanceOf<IList>(), toolName);
                Assert.That(((IList)schema["oneOf"]).Count, Is.EqualTo(2), toolName);
            }

            var validationSchema = RequireDictionary(
                details["vmframework/validate-visual-element-paths"]["inputSchema"]);
            var validationProperties = RequireDictionary(validationSchema["properties"]);
            Assert.That(validationProperties.ContainsKey("allPanels"), Is.True);
            Assert.That(validationSchema["oneOf"], Is.InstanceOf<IList>());
            Assert.That(((IList)validationSchema["oneOf"]).Count, Is.EqualTo(3));

            Assert.Throws<ArgumentException>(() =>
                VMFrameworkMcpTools.InspectUIPanel(new Dictionary<string, object>()));
            Assert.Throws<ArgumentException>(() =>
                VMFrameworkMcpTools.InspectBindObjects(new Dictionary<string, object>()));
            Assert.Throws<ArgumentException>(() =>
                VMFrameworkMcpTools.InspectContainerPanel(new Dictionary<string, object>()));
            Assert.Throws<ArgumentException>(() =>
                VMFrameworkMcpTools.ValidateVisualElementPaths(new Dictionary<string, object>()));
            Assert.Throws<ArgumentException>(() =>
                VMFrameworkMcpTools.InspectUIPanel(new Dictionary<string, object>
                {
                    { "panelID", "panel" },
                    { "prefabPath", "Assets/Panel.prefab" }
                }));
            Assert.Throws<ArgumentException>(() =>
                VMFrameworkMcpTools.ValidateVisualElementPaths(new Dictionary<string, object>
                {
                    { "allPanels", true },
                    { "panelID", "panel" }
                }));
            Assert.Throws<ArgumentException>(() =>
                VMFrameworkMcpTools.ValidateVisualElementPaths(new Dictionary<string, object>
                {
                    { "allPanels", false },
                    { "panelID", "panel" }
                }));
        }

        [Test]
        public void ValidateVisualElementPaths_AllPanels_ReturnsBoundedAggregate()
        {
            var result = RequireDictionary(
                VMFrameworkMcpTools.ValidateVisualElementPaths(new Dictionary<string, object>
                {
                    { "allPanels", true },
                    { "limit", 1 }
                }));

            Assert.That(result["mode"], Is.EqualTo("allPanels"));
            Assert.That(Convert.ToInt32(result["panelCount"]), Is.GreaterThanOrEqualTo(0));
            Assert.That(Convert.ToInt32(result["count"]), Is.LessThanOrEqualTo(1));
            Assert.That(result.ContainsKey("missingPrefabCount"), Is.True);
            Assert.That(result.ContainsKey("missingVisualTreeCount"), Is.True);
            Assert.That(result.ContainsKey("invalidPathCount"), Is.True);
            Assert.That(result["paths"], Is.InstanceOf<IList>());
            Assert.That(((IList)result["paths"]).Count, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void ProjectConfiguration_RoundTripsTeamOwnedValidationCoverage()
        {
            string path = Path.GetFullPath(
                "ProjectSettings/VMFrameworkMCPSettings.json");
            bool existed = File.Exists(path);
            string original = existed ? File.ReadAllText(path) : null;

            Type manager = typeof(VMFrameworkMcpTools).Assembly.GetType(
                "VMFramework.MCP.Editor.VMFrameworkMcpSettingsManager", true);
            PropertyInfo missingTranslations = manager.GetProperty(
                "IncludeMissingGameTagTranslations",
                BindingFlags.Static | BindingFlags.NonPublic);
            PropertyInfo prefabReferences = manager.GetProperty(
                "IncludeGamePrefabTagReferences",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo reload = manager.GetMethod(
                "ReloadProjectConfiguration",
                BindingFlags.Static | BindingFlags.NonPublic);

            try
            {
                File.WriteAllText(path,
                    "{\n" +
                    "  \"schemaVersion\": 1,\n" +
                    "  \"gameTagValidation\": {\n" +
                    "    \"includeMissingTranslations\": false,\n" +
                    "    \"includeGamePrefabReferences\": true\n" +
                    "  }\n" +
                    "}\n");
                reload.Invoke(null, null);

                Assert.That(missingTranslations.GetValue(null), Is.EqualTo(false));
                Assert.That(prefabReferences.GetValue(null), Is.EqualTo(true));
                var snapshot = RequireDictionary(
                    VMFrameworkMcpTools.GetConfiguration(
                        new Dictionary<string, object>()));
                var projectSettings = RequireDictionary(snapshot["projectSettings"]);
                Assert.That(projectSettings.ContainsKey("error"), Is.False);

                prefabReferences.SetValue(null, false);
                reload.Invoke(null, null);
                Assert.That(prefabReferences.GetValue(null), Is.EqualTo(false));
                Assert.That(File.ReadAllText(path),
                    Does.Contain("\"includeGamePrefabReferences\": false"));
            }
            finally
            {
                if (existed)
                    File.WriteAllText(path, original);
                else if (File.Exists(path))
                    File.Delete(path);
                reload.Invoke(null, null);
            }
        }

        private static string GetString(Dictionary<string, object> dictionary, string key)
        {
            return dictionary.TryGetValue(key, out object value)
                ? value?.ToString() ?? ""
                : "";
        }

        private static Dictionary<string, object> RequireDictionary(object value)
        {
            Assert.That(value, Is.InstanceOf<Dictionary<string, object>>());
            return (Dictionary<string, object>)value;
        }
    }
}
#endif
