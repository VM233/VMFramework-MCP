#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor;
using VMFramework.GameLogicArchitecture;
using VMFramework.UI;

namespace VMFramework.MCP.Editor
{
    internal static class VMFrameworkRuntimeUIPanelEvents
    {
        private static readonly Dictionary<string, long> Sequences =
            new(StringComparer.Ordinal);
        private static readonly HashSet<IUIPanel> TrackedPanels =
            new(ReferenceComparer.Instance);
        private static UIPanelManager subscribedManager;
        private static long sequence;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        internal static void EnsureManagerSubscription()
        {
            UIPanelManager manager = UIPanelManager.Instance;
            if (ReferenceEquals(manager, subscribedManager))
                return;
            if (subscribedManager != null)
                subscribedManager.OnPanelCreatedEvent -= Track;
            subscribedManager = manager;
            if (subscribedManager != null)
                subscribedManager.OnPanelCreatedEvent += Track;
        }

        internal static void Track(IUIPanel panel)
        {
            if (panel == null || !TrackedPanels.Add(panel))
                return;
            panel.OnOpen += OnOpen;
            panel.OnPostClose += OnPostClose;
            panel.OnDestruct += OnDestruct;
        }

        internal static long GetSequence(string panelID, string objectID,
            string eventName)
        {
            long byPanel = Sequences.TryGetValue(
                BuildPanelKey(panelID, eventName), out long panelSequence)
                ? panelSequence
                : 0;
            long byObject = !string.IsNullOrWhiteSpace(objectID) &&
                            Sequences.TryGetValue(
                                BuildObjectKey(objectID, eventName),
                                out long objectSequence)
                ? objectSequence
                : 0;
            return Math.Max(byPanel, byObject);
        }

        private static void OnOpen(IUIPanel panel)
        {
            Record(panel, "open");
        }

        private static void OnPostClose(IUIPanel panel)
        {
            Record(panel, "post-close");
        }

        private static void Record(IUIPanel panel, string eventName)
        {
            long current = ++sequence;
            Sequences[BuildPanelKey(panel.id, eventName)] = current;
            string objectID = GetObjectID(panel);
            if (!string.IsNullOrWhiteSpace(objectID))
                Sequences[BuildObjectKey(objectID, eventName)] = current;
        }

        private static void OnDestruct(IUIPanel panel)
        {
            panel.OnOpen -= OnOpen;
            panel.OnPostClose -= OnPostClose;
            panel.OnDestruct -= OnDestruct;
            TrackedPanels.Remove(panel);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode &&
                state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            if (subscribedManager != null)
                subscribedManager.OnPanelCreatedEvent -= Track;
            subscribedManager = null;
            foreach (IUIPanel panel in TrackedPanels.ToList())
                OnDestruct(panel);
            TrackedPanels.Clear();
            Sequences.Clear();
            sequence = 0;
        }

        private static string BuildPanelKey(string panelID, string eventName) =>
            $"panel:{panelID}:{eventName}";

        private static string BuildObjectKey(string objectID, string eventName) =>
            $"object:{objectID}:{eventName}";

        private static string GetObjectID(IUIPanel panel) =>
            panel is UnityEngine.Object unityObject ? MCPObjectId.Get(unityObject) : "";

        private sealed class ReferenceComparer : IEqualityComparer<IUIPanel>
        {
            internal static readonly ReferenceComparer Instance = new();

            public bool Equals(IUIPanel x, IUIPanel y) => ReferenceEquals(x, y);

            public int GetHashCode(IUIPanel obj) =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }

    [MCPProjectTool(ToolName,
        ShortName = "vmf/runtime-ui-panel",
        Description = "Open, close, bind, clear, inspect actual visibility, or persistently wait for OnOpen/OnPostClose on a VMFramework runtime UIPanel.",
        InputSchemaJson = InputSchema,
        OutputSchemaJson = OutputSchema,
        SideEffects = MCPProjectToolSideEffect.ChangesRuntimeState,
        ErrorCodes = new[]
        {
            "requires_play_mode",
            "ui_panel_manager_unavailable",
            "ui_panel_not_found",
            "ui_panel_ambiguous",
            "ui_panel_has_no_bind_objects_manager",
            "runtime_game_item_session_not_found",
            "runtime_game_item_session_not_live",
            "persistent_job_required",
            "ui_panel_wait_timeout",
        },
        MutatesRuntime = true,
        RequiresPlayMode = true,
        FirstClass = true)]
    public sealed class VMFrameworkRuntimeUIPanelTool : IMCPPersistentProjectTool
    {
        private const string ToolName = "vmframework/runtime-ui-panel";
        private const string InputSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"action\":{\"type\":\"string\",\"enum\":[\"state\",\"open\",\"close\",\"bind\",\"clear\",\"wait-open\",\"wait-post-close\"],\"description\":\"Panel operation.\"}," +
            "\"panelID\":{\"type\":\"string\",\"minLength\":1,\"description\":\"UIPanel GamePrefab id.\"}," +
            "\"panelObjectID\":{\"type\":\"string\",\"description\":\"Optional exact runtime panel component object id.\"}," +
            "\"all\":{\"type\":\"boolean\",\"description\":\"For close, operate on every opened panel with panelID. Defaults to false.\"}," +
            "\"cleanupToken\":{\"type\":\"string\",\"description\":\"Runtime GameItem session token used by bind.\"}," +
            "\"bindName\":{\"type\":\"string\",\"description\":\"BindObjectsManager name. Defaults to the global bind name.\"}," +
            "\"timeoutMs\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":600000,\"description\":\"Wait timeout. Defaults to 30000 ms.\"}," +
            "\"acceptCurrentState\":{\"type\":\"boolean\",\"description\":\"Let wait actions succeed immediately when the target state already holds. Defaults to true.\"}" +
            "},\"required\":[\"action\",\"panelID\"],\"additionalProperties\":false}";
        private const string OutputSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"action\":{\"type\":\"string\"}," +
            "\"panel\":{\"oneOf\":[{\"type\":\"object\",\"additionalProperties\":true},{\"type\":\"null\"}]}," +
            "\"panels\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":true}}," +
            "\"affectedCount\":{\"type\":\"integer\"}," +
            "\"waited\":{\"type\":\"boolean\"}," +
            "\"eventSequence\":{\"type\":\"integer\"}" +
            "},\"required\":[\"action\"],\"additionalProperties\":false}";

        public object Execute(Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            RequirePlayMode();
            string action = GetString(args, "action");
            if (action is "wait-open" or "wait-post-close")
            {
                throw new MCPProjectToolException("persistent_job_required",
                    $"{action} must be called with runAsJob=true so Unity can advance between observations.");
            }
            return ExecuteImmediate(args, action);
        }

        public MCPProjectToolJobStep ExecuteJobStep(Dictionary<string, object> args,
            Dictionary<string, object> state)
        {
            args ??= new Dictionary<string, object>();
            state ??= new Dictionary<string, object>();
            RequirePlayMode();
            string action = GetString(args, "action");
            if (action is not ("wait-open" or "wait-post-close"))
                return MCPProjectToolJobStep.Complete(ExecuteImmediate(args, action));
            return WaitForPanelEvent(args, state, action);
        }

        private static object ExecuteImmediate(Dictionary<string, object> args,
            string action)
        {
            VMFrameworkRuntimeUIPanelEvents.EnsureManagerSubscription();
            UIPanelManager manager = GetManager();
            string panelID = GetString(args, "panelID");
            string objectID = GetString(args, "panelObjectID");
            switch (action)
            {
                case "state":
                {
                    IUIPanel panel = ResolvePanel(manager, panelID, objectID,
                        requireOpened: false, allowMissing: true);
                    return new Dictionary<string, object>
                    {
                        { "action", action },
                        { "panel", panel == null ? null : DescribePanel(manager, panel) },
                        { "panels", GetOpenedPanels(manager, panelID)
                            .Select(panelValue => DescribePanel(manager, panelValue))
                            .ToList() },
                    };
                }
                case "open":
                {
                    IUIPanel existing = ResolvePanel(manager, panelID, objectID,
                        requireOpened: false, allowMissing: true);
                    if (existing != null)
                        VMFrameworkRuntimeUIPanelEvents.Track(existing);
                    IUIPanel panel = existing != null
                        ? OpenExisting(manager, existing)
                        : manager.GetAndOpen(panelID);
                    if (panel == null)
                    {
                        throw new MCPProjectToolException("ui_panel_not_found",
                            $"UIPanel '{panelID}' could not be opened.");
                    }
                    VMFrameworkRuntimeUIPanelEvents.Track(panel);
                    return new Dictionary<string, object>
                    {
                        { "action", action },
                        { "affectedCount", 1 },
                        { "panel", DescribePanel(manager, panel) },
                    };
                }
                case "close":
                {
                    List<IUIPanel> targets = GetTargets(manager, panelID, objectID,
                        GetBool(args, "all", false));
                    int affected = targets.Count(manager.TryClose);
                    return new Dictionary<string, object>
                    {
                        { "action", action },
                        { "affectedCount", affected },
                        { "panels", targets.Select(panel => DescribePanel(manager, panel))
                            .ToList() },
                    };
                }
                case "bind":
                {
                    IUIPanel panel = ResolvePanel(manager, panelID, objectID,
                        requireOpened: true, allowMissing: false);
                    string token = GetRequiredString(args, "cleanupToken");
                    VMFrameworkRuntimeGameItemSessions.Session session =
                        VMFrameworkRuntimeGameItemSessions.GetRequired(token);
                    VMFrameworkRuntimeGameItemSessions.BindToExistingPanel(
                        session, panel, GetString(args, "bindName",
                            BindObjectsManager.GLOBAL_BIND_NAME));
                    return new Dictionary<string, object>
                    {
                        { "action", action },
                        { "affectedCount", 1 },
                        { "panel", DescribePanel(manager, panel) },
                    };
                }
                case "clear":
                {
                    IUIPanel panel = ResolvePanel(manager, panelID, objectID,
                        requireOpened: false, allowMissing: false);
                    if (panel.BindObjectsManager == null)
                    {
                        throw new MCPProjectToolException(
                            "ui_panel_has_no_bind_objects_manager",
                            $"UIPanel '{panelID}' has no BindObjectsManager.");
                    }
                    string bindName = GetString(args, "bindName");
                    if (string.IsNullOrWhiteSpace(bindName))
                        panel.BindObjectsManager.ClearAllObjects();
                    else
                        panel.BindObjectsManager.ClearObjects(bindName);
                    return new Dictionary<string, object>
                    {
                        { "action", action },
                        { "affectedCount", 1 },
                        { "panel", DescribePanel(manager, panel) },
                    };
                }
                default:
                    throw new MCPProjectToolException("invalid_arguments",
                        "Unsupported runtime UIPanel action.");
            }
        }

        private static MCPProjectToolJobStep WaitForPanelEvent(
            Dictionary<string, object> args, Dictionary<string, object> state,
            string action)
        {
            VMFrameworkRuntimeUIPanelEvents.EnsureManagerSubscription();
            UIPanelManager manager = GetManager();
            string panelID = GetString(args, "panelID");
            string objectID = GetString(args, "panelObjectID");
            IUIPanel panel = ResolvePanel(manager, panelID, objectID,
                requireOpened: false, allowMissing: true);
            if (panel != null)
            {
                VMFrameworkRuntimeUIPanelEvents.Track(panel);
                if (string.IsNullOrWhiteSpace(objectID))
                    objectID = GetObjectID(panel);
            }

            string eventName = action == "wait-open" ? "open" : "post-close";
            long sequence = VMFrameworkRuntimeUIPanelEvents.GetSequence(
                panelID, objectID, eventName);
            bool firstStep = !state.TryGetValue("startedAtTicks", out object startedValue);
            long startedAtTicks = firstStep
                ? DateTime.UtcNow.Ticks
                : Convert.ToInt64(startedValue, CultureInfo.InvariantCulture);
            long baselineSequence = firstStep
                ? sequence
                : Convert.ToInt64(state["baselineSequence"],
                    CultureInfo.InvariantCulture);
            int timeoutMs = GetInt(args, "timeoutMs", 30_000);
            bool currentStateAccepted = firstStep &&
                                        GetBool(args, "acceptCurrentState", true) &&
                                        IsTargetState(manager, panelID, objectID, action);
            bool eventObserved = sequence > baselineSequence;
            if (currentStateAccepted || eventObserved)
            {
                IUIPanel currentPanel = ResolvePanel(manager, panelID, objectID,
                    requireOpened: false, allowMissing: true);
                return MCPProjectToolJobStep.Complete(
                    new Dictionary<string, object>
                    {
                        { "action", action },
                        { "waited", !currentStateAccepted },
                        { "eventSequence", sequence },
                        { "panel", currentPanel == null
                            ? null
                            : DescribePanel(manager, currentPanel) },
                    });
            }

            double elapsedMs = TimeSpan.FromTicks(
                DateTime.UtcNow.Ticks - startedAtTicks).TotalMilliseconds;
            if (elapsedMs >= timeoutMs)
            {
                throw new MCPProjectToolException("ui_panel_wait_timeout",
                    $"Timed out after {timeoutMs} ms waiting for {eventName} on UIPanel '{panelID}'.");
            }

            return MCPProjectToolJobStep.Pending(
                new Dictionary<string, object>
                {
                    { "startedAtTicks", startedAtTicks },
                    { "baselineSequence", baselineSequence },
                    { "panelObjectID", objectID },
                },
                elapsedMs / timeoutMs,
                $"Waiting for {eventName} on UIPanel '{panelID}'.",
                delayMilliseconds: 16);
        }

        private static bool IsTargetState(UIPanelManager manager, string panelID,
            string objectID, string action)
        {
            IUIPanel panel = ResolvePanel(manager, panelID, objectID,
                requireOpened: false, allowMissing: true);
            if (action == "wait-open")
                return panel != null && panel.IsOpened;
            if (panel != null)
                return !panel.IsOpened && !manager.IsClosing(panel);
            return GetOpenedPanels(manager, panelID).Count == 0;
        }

        private static List<IUIPanel> GetTargets(UIPanelManager manager,
            string panelID, string objectID, bool all)
        {
            if (!string.IsNullOrWhiteSpace(objectID))
            {
                return new List<IUIPanel>
                {
                    ResolvePanel(manager, panelID, objectID, false, false),
                };
            }
            List<IUIPanel> opened = GetOpenedPanels(manager, panelID);
            if (opened.Count == 0 && manager.TryGetUniquePanel(panelID,
                    out IUIPanel uniquePanel))
            {
                opened.Add(uniquePanel);
            }
            if (opened.Count == 0)
            {
                throw new MCPProjectToolException("ui_panel_not_found",
                    $"UIPanel '{panelID}' was not found.");
            }
            if (!all && opened.Count > 1)
            {
                throw new MCPProjectToolException("ui_panel_ambiguous",
                    $"UIPanel '{panelID}' has {opened.Count} opened instances. " +
                    "Supply panelObjectID or all=true.");
            }
            return all ? opened : new List<IUIPanel> { opened[0] };
        }

        private static IUIPanel ResolvePanel(UIPanelManager manager, string panelID,
            string objectID, bool requireOpened, bool allowMissing)
        {
            if (!string.IsNullOrWhiteSpace(objectID))
            {
                UnityEngine.Object target = MCPObjectId.ToObject(objectID);
                IUIPanel exact = target switch
                {
                    IUIPanel direct => direct,
                    GameObject gameObject => gameObject
                        .GetComponentsInChildren<MonoBehaviour>(true)
                        .OfType<IUIPanel>()
                        .FirstOrDefault(),
                    Component component => component.gameObject
                        .GetComponentsInChildren<MonoBehaviour>(true)
                        .OfType<IUIPanel>()
                        .FirstOrDefault(),
                    _ => null,
                };
                if (exact != null && exact.id == panelID &&
                    (!requireOpened || exact.IsOpened))
                {
                    return exact;
                }
                if (!allowMissing)
                {
                    throw new MCPProjectToolException("ui_panel_not_found",
                        $"Object '{objectID}' is not the requested UIPanel '{panelID}'.");
                }
                return null;
            }

            List<IUIPanel> opened = GetOpenedPanels(manager, panelID);
            if (opened.Count == 1)
                return opened[0];
            if (opened.Count > 1)
            {
                throw new MCPProjectToolException("ui_panel_ambiguous",
                    $"UIPanel '{panelID}' has {opened.Count} opened instances. Supply panelObjectID.");
            }
            if (!requireOpened && manager.TryGetUniquePanel(panelID,
                    out IUIPanel uniquePanel))
            {
                return uniquePanel;
            }
            if (!allowMissing)
            {
                throw new MCPProjectToolException("ui_panel_not_found",
                    $"UIPanel '{panelID}' was not found.");
            }
            return null;
        }

        private static List<IUIPanel> GetOpenedPanels(UIPanelManager manager,
            string panelID)
        {
            return manager.TryGetOpenedPanels(panelID,
                    out IReadOnlyCollection<IUIPanel> opened)
                ? opened.Where(panel => panel != null).ToList()
                : new List<IUIPanel>();
        }

        private static IUIPanel OpenExisting(UIPanelManager manager,
            IUIPanel panel)
        {
            manager.TryOpen(panel, null);
            return panel;
        }

        private static Dictionary<string, object> DescribePanel(
            UIPanelManager manager, IUIPanel panel)
        {
            bool activeInHierarchy = panel is Component component &&
                                     component.gameObject.activeInHierarchy;
            var visibility = new Dictionary<string, object>
            {
                { "isOpened", panel.IsOpened },
                { "isClosing", panel.IsClosing || manager.IsClosing(panel) },
                { "uiEnabled", panel.UIEnabled },
                { "activeInHierarchy", activeInHierarchy },
            };
            bool toolkitVisible = true;
            if (panel is IUIToolkitPanel toolkitPanel)
            {
                VisualElement root = toolkitPanel.RootVisualElement;
                toolkitVisible = root != null &&
                                 root.panel != null &&
                                 root.resolvedStyle.display != DisplayStyle.None &&
                                 root.resolvedStyle.visibility == Visibility.Visible &&
                                 root.resolvedStyle.opacity > 0;
                visibility["rootAttached"] = root?.panel != null;
                visibility["display"] = root?.resolvedStyle.display.ToString() ?? "";
                visibility["visibility"] =
                    root?.resolvedStyle.visibility.ToString() ?? "";
                visibility["opacity"] = root?.resolvedStyle.opacity ?? 0;
            }
            visibility["actuallyVisible"] = panel.IsOpened && panel.UIEnabled &&
                                             activeInHierarchy && toolkitVisible;

            return new Dictionary<string, object>
            {
                { "id", panel.id },
                { "type", panel.GetType().FullName },
                { "objectID", GetObjectID(panel) },
                { "gameObjectPath", panel is Component panelComponent
                    ? VMFrameworkMcpTools.GetGameObjectPath(panelComponent.transform)
                    : "" },
                { "isUnique", panel.IsUnique },
                { "visibility", visibility },
                { "bindObjects", panel.BindObjectsManager == null
                    ? null
                    : new Dictionary<string, object>
                    {
                        { "bindNames", panel.BindObjectsManager.BindNames
                            .OrderBy(name => name, StringComparer.Ordinal).ToList() },
                        { "counts", panel.BindObjectsManager.BindNames
                            .OrderBy(name => name, StringComparer.Ordinal)
                            .ToDictionary(name => name,
                                name => (object)panel.BindObjectsManager
                                    .GetObjects(name).Count) },
                    } },
            };
        }

        private static UIPanelManager GetManager()
        {
            UIPanelManager manager = UIPanelManager.Instance;
            if (manager == null)
            {
                throw new MCPProjectToolException("ui_panel_manager_unavailable",
                    "UIPanelManager is unavailable in the current Play Mode lifecycle.");
            }
            return manager;
        }

        private static void RequirePlayMode()
        {
            if (!Application.isPlaying)
            {
                throw new MCPProjectToolException("requires_play_mode",
                    "Runtime UIPanel operations require Play Mode.");
            }
        }

        private static string GetObjectID(IUIPanel panel) =>
            panel is UnityEngine.Object unityObject ? MCPObjectId.Get(unityObject) : "";

        private static string GetRequiredString(
            IReadOnlyDictionary<string, object> args, string key)
        {
            string value = GetString(args, key);
            if (string.IsNullOrWhiteSpace(value))
                throw new MCPProjectToolException("invalid_arguments", $"{key} is required.");
            return value;
        }

        private static string GetString(IReadOnlyDictionary<string, object> args,
            string key, string fallback = "")
        {
            return args != null && args.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : fallback;
        }

        private static bool GetBool(IReadOnlyDictionary<string, object> args,
            string key, bool fallback)
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null
                ? Convert.ToBoolean(value, CultureInfo.InvariantCulture)
                : fallback;
        }

        private static int GetInt(IReadOnlyDictionary<string, object> args,
            string key, int fallback)
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture)
                : fallback;
        }
    }
}
#endif
