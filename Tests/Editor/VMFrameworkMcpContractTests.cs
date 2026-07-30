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
            "vmframework/inspect-runtime-game-item",
            "vmframework/inspect-ui-panel",
            "vmframework/list-game-prefab-types",
            "vmframework/list-game-tags",
            "vmframework/list-general-settings",
            "vmframework/logic-tick-control",
            "vmframework/procedure-state",
            "vmframework/reference-trace",
            "vmframework/runtime-game-item-session",
            "vmframework/runtime-ui-panel",
            "vmframework/set-property",
            "vmframework/start-property-trace",
            "vmframework/stop-property-trace",
            "vmframework/update-game-prefab",
            "vmframework/upsert-game-tag",
            "vmframework/validate-game-tags",
            "vmframework/validate-visual-element-paths",
        };

        private static readonly HashSet<string> ExpectedFirstClassToolNames =
            new(StringComparer.Ordinal)
            {
                "vmframework/inspect-runtime-game-item",
                "vmframework/logic-tick-control",
                "vmframework/procedure-state",
                "vmframework/reference-trace",
                "vmframework/runtime-game-item-session",
                "vmframework/runtime-ui-panel",
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
                foreach (string retiredKey in new[]
                         {
                             "readOnly", "mutatesAssets", "mutatesRuntime", "dangerous",
                             "longRunning", "mayReloadDomain", "requiresPlayMode",
                             "firstClass", "cleanupAvailable", "incrementalJob",
                             "hasOutputSchema", "enforcesInputSchema",
                             "enforcesOutputSchema", "valid",
                         })
                {
                    Assert.That(tool.ContainsKey(retiredKey), Is.False,
                        $"{toolName} still exposes legacy boolean metadata '{retiredKey}'.");
                }
                Assert.That(HasTag(tool, "invalid"), Is.False, toolName);
                Assert.That(HasTag(tool, "firstClass"),
                    Is.EqualTo(ExpectedFirstClassToolNames.Contains(toolName)),
                    $"{toolName} has the wrong direct-exposure contract.");

                int operationKinds =
                    (HasTag(tool, "readOnly") ? 1 : 0) +
                    (HasSideEffect(tool, "writesAssets") ||
                     HasSideEffect(tool, "writesScene") ? 1 : 0) +
                    (HasSideEffect(tool, "changesRuntimeState") ? 1 : 0);
                Assert.That(operationKinds, Is.EqualTo(1), toolName);

                var schema = RequireDictionary(tool["inputSchema"]);
                Assert.That(schema["additionalProperties"], Is.EqualTo(false),
                    $"{toolName} must reject unknown business arguments.");

                if (ExpectedFirstClassToolNames.Contains(toolName))
                {
                    Assert.That(HasTag(tool, "outputSchema"), Is.True,
                        $"{toolName} must provide and enforce outputSchema.");
                    Assert.That(RequireDictionary(tool["outputSchema"])["type"],
                        Is.EqualTo("object"), toolName);
                    Assert.That(tool["errorCodes"], Is.InstanceOf<IList>(), toolName);
                    Assert.That(((IList)tool["errorCodes"]).Count,
                        Is.GreaterThan(0), toolName);
                    Assert.That(tool["sideEffects"], Is.InstanceOf<IList>(), toolName);
                    Assert.That(((IList)tool["sideEffects"]).Count,
                        Is.GreaterThan(0), toolName);
                }
            }
        }

        [Test]
        public void NewRuntimeAndWaitTools_ExposeLifecycleAndIncrementalContracts()
        {
            var details = MCPProjectToolCommands.GetToolDetails(false)
                .Where(tool => GetString(tool, "toolName").StartsWith(
                    "vmframework/", StringComparison.Ordinal))
                .ToDictionary(tool => GetString(tool, "toolName"));

            var session = details["vmframework/runtime-game-item-session"];
            Assert.That(HasTag(session, "cleanup"), Is.True);
            Assert.That(session["cleanupToolName"],
                Is.EqualTo("vmframework/runtime-game-item-session"));
            CollectionAssert.Contains((IList)session["sideEffects"],
                "createsTemporaryObjects");
            CollectionAssert.Contains((IList)session["sideEffects"],
                "changesRuntimeState");

            foreach (string toolName in new[]
                     {
                         "vmframework/logic-tick-control",
                         "vmframework/procedure-state",
                         "vmframework/reference-trace",
                         "vmframework/runtime-ui-panel",
                     })
            {
                Assert.That(HasTag(details[toolName], "incrementalJob"),
                    Is.True, toolName);
            }

            CollectionAssert.Contains(
                (IList)details["vmframework/logic-tick-control"]["sideEffects"],
                "advancesLogicTicks");
            CollectionAssert.Contains(
                (IList)details["vmframework/reference-trace"]["sideEffects"],
                "readsProjectState");
        }

        [Test]
        public void RuntimePropertyTools_DeclareAccurateOperationMetadataAndSchemas()
        {
            var details = MCPProjectToolCommands.GetToolDetails(false)
                .Where(tool => GetString(tool, "toolName").StartsWith(
                    "vmframework/", StringComparison.Ordinal))
                .ToDictionary(tool => GetString(tool, "toolName"));

            var setProperty = details["vmframework/set-property"];
            Assert.That(HasSideEffect(setProperty, "changesRuntimeState"), Is.True);
            Assert.That(HasTag(setProperty, "requiresPlayMode"), Is.True);

            var startTrace = details["vmframework/start-property-trace"];
            Assert.That(HasSideEffect(startTrace, "changesRuntimeState"), Is.True);
            var startProperties = RequireDictionary(
                RequireDictionary(startTrace["inputSchema"])["properties"]);
            Assert.That(startProperties.ContainsKey("maxEvents"), Is.True);
            Assert.That(startProperties.ContainsKey("clear"), Is.False);

            var getTrace = details["vmframework/get-property-trace"];
            Assert.That(HasTag(getTrace, "readOnly"), Is.True);
            var readProperties = RequireDictionary(
                RequireDictionary(getTrace["inputSchema"])["properties"]);
            Assert.That(readProperties.Keys, Is.EquivalentTo(new[] { "offset", "limit" }));

            var stopTrace = details["vmframework/stop-property-trace"];
            Assert.That(HasSideEffect(stopTrace, "changesRuntimeState"), Is.True);
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

        private static bool HasTag(Dictionary<string, object> metadata, string tag)
        {
            return HasString(metadata, "tags", tag);
        }

        private static bool HasSideEffect(Dictionary<string, object> metadata, string sideEffect)
        {
            return HasString(metadata, "sideEffects", sideEffect);
        }

        private static bool HasString(Dictionary<string, object> metadata,
            string key, string expected)
        {
            return metadata.TryGetValue(key, out object value) &&
                   value is IEnumerable values &&
                   values.Cast<object>().Any(item =>
                       string.Equals(item?.ToString(), expected, StringComparison.Ordinal));
        }

        private static Dictionary<string, object> RequireDictionary(object value)
        {
            Assert.That(value, Is.InstanceOf<Dictionary<string, object>>());
            return (Dictionary<string, object>)value;
        }
    }
}
#endif
