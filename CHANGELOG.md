# Changelog

## 3.0.0

- Migrate all 27 VMFramework tools to VM Unity MCP 6's canonical direct-route
  catalog. Remove first-class flags and the three-stage project-tool contract;
  each tool is now searched, exactly activated, and called as a typed tool.
- Split the former monolithic tool class into configuration, GamePrefab,
  GameTag, property, and UI-panel owners. Extract UI-panel inspection and
  validation into `VMFrameworkUIPanelMcpTools`.
- Add `VMFrameworkGamePrefabAuthoring` and its typed request as the reusable
  asset-authoring authority. Project consumers can compose it directly instead
  of invoking one MCP tool through a string-based generic executor.
- Keep the public extension surface semantically neutral: project tools declare
  module/capability/operation/search metadata through VM Unity MCP, while
  project-specific gameplay meanings remain in consumer adapters.
- Update catalog/schema tests for direct routes, normalized operation metadata,
  strict descriptions, package-level regression categories, and the new class
  ownership boundaries.

## 2.3.3

- Fix `vmframework/update-game-prefab` transactions that rename the root ID:
  post-save verification now resolves the new identity instead of looking up
  the obsolete selector and rolling the transaction back.
- Return the verified new `id` and the prior selector as `previousId`, reject
  empty IDs, and cover the complete asset save/import/readback path with an
  EditMode regression test.
- Declare the regression assembly's direct Odin serialization dependency so
  package tests compile in consumer projects.

## 2.3.2

- Require VM Unity MCP 5.5.2 so compact Editor-state snapshots always retain
  an authoritative process-state tag while preserving the schema-v5
  presence-only response contract.

## 2.3.1

- Require VM Unity MCP 5.5.1 so `project-tools/get` preserves the exact
  `reference-trace` output schema instead of interpreting its business `tags`
  property as transport capability metadata.
- Extend the configuration and ownership audit to the six runtime,
  Procedure/Logic Tick, inspection, and reference-trace tools introduced in
  2.2.0.

## 2.3.0

- Adopt VM Unity MCP 5.5 schema-v5 tool metadata: positive capabilities use
  `tags`, mutations stay in `sideEffects`, and false/empty descriptor aliases
  are omitted.
- Keep runtime booleans such as visibility, wait matching, and tick outcomes
  explicit because they are dynamic domain facts rather than capability tags.
- Add contract coverage preventing VMFramework tools from reintroducing legacy
  boolean metadata.

## 2.2.0

- Add `vmframework/runtime-game-item-session` as the single owner for borrowing,
  placement, property/faction setup, optional UI binding, idempotent session-key
  reuse, and token-based cleanup of temporary runtime GameItems.
- Add `vmframework/runtime-ui-panel` for open/close, bind/clear, actual
  visibility inspection, and persistent OnOpen/OnPostClose waits.
- Add `vmframework/procedure-state` and
  `vmframework/logic-tick-control` for explicit state contracts, bounded
  persistent waits, and controlled Logic Tick advancement.
- Add `vmframework/inspect-runtime-game-item` for one-shot identity, GameTags,
  Properties, Containers, project-domain Abilities/Faction, lifecycle, and
  pool-state inspection.
- Move semantic wrapper/GamePrefab/Prefab/component/GameTag/localization and
  reverse-reference tracing into generic `vmframework/reference-trace`; large
  reverse scans yield through persistent Jobs and support cancellation.
- Add a priority-based project-domain adapter contract for authoritative facts
  such as faction and abilities without introducing tag, name, hierarchy, or
  UI-state inference.
- Publish and enforce output schemas, stable domain error codes, precise side
  effects, and cleanup metadata for the new runtime tools. Require VM Unity MCP
  5.4.0 for persistent project-tool Jobs and schema-v4 metadata.

## 2.1.6

- Keep the conditional VisualElementPath regression fixture independent of
  optional Odin Editor assemblies by iterating reflected records directly.

## 2.1.5

- Exclude VisualElementPath fields disabled by resolvable Odin `ShowIf` or
  `HideIf` conditions from validation, so inactive alternate configuration
  branches do not produce required-path false positives.
- Support boolean fields, enum/value comparisons, properties, and parameterless
  condition methods while keeping unsupported expressions conservatively active.
- Add regression coverage for both alternate `PairEntryAdder` path branches and
  enum-gated custom overflow containers.

## 2.1.4

- Require VM Unity MCP 5.3.4 so the `not` and `const` constraints used by panel
  selector schemas are enforced at the shared project-tool boundary before
  invocation.

## 2.1.3

- Require exactly one `panelID` or `prefabPath` for every single-panel
  inspection tool so runtime resolution and published schemas have the same
  contract.
- Add `allPanels: true` to VisualElementPath validation, covering registered
  panel configs and standalone `UIPanel` prefabs with one globally bounded,
  paginated report.
- Distinguish missing prefabs, missing VisualTreeAssets, invalid panels, and
  invalid paths in aggregate results.
- Replace the README's hand-maintained complete tool table with live
  `project-tools/list -> get -> execute` discovery guidance.

## 2.1.2

- Omit an empty project-settings error from configuration replies and report
  the effective GameTag validation coverage in validation results.

## 2.1.1

- Restore the explicit integer parser used by VMFramework-specific depth,
  collection, and trace preferences after introducing the shared Unity MCP
  result-limit resolver.

## 2.1.0

- Add team-owned GameTag validation settings under Project Settings and local
  GamePrefab/trace response preferences under Preferences.
- Reuse VM Unity MCP's shared primary-result preference for paginated VMFramework
  reads instead of duplicating a result-limit setting.
- Add `vmframework/get-configuration` and audit the exact 21-tool catalog.
- Make large runtime, localization, provider, update-snapshot, and post-upsert
  validation details opt-in.
- Correct runtime-property and trace operation metadata, split trace schemas,
  paginate retained events, and keep trace sequence numbers monotonic.
- Reject unknown tool arguments and document the three-stage project-tool
  contract, configuration ownership, and per-tool decisions.

## 2.0.0

- Require VM Unity MCP 5.0.0 and its compact list, detailed get, and execute project-tool contract.

## 1.0.6

- Migrate the Unity MCP dependency and assembly reference to the independent VM Unity MCP package (`com.vm233.unity-mcp`, `VMUnityMCP.Editor`).

## 1.0.5

- Add first-class GameTag listing, upsert, localization maintenance, and validation tools.
- Stop VisualElementPath scanning at nested Unity object references so missing Transforms and other enumerable Unity objects cannot abort validation.

## 1.0.4

- Inspect LocalizedString values as structured localized references instead of empty enumerable collections.

## 1.0.3

- Added HashSet and generic ICollection support to GamePrefab collection conversion, append, remove, clear, and indexed replacement operations.

## 1.0.2

- Convert structured object dictionaries before enumerable values so LocalizedString and other serialized objects that implement IEnumerable are updated correctly.

## 1.0.1

- Treat empty VisualElementPath fields as valid when they are optional, while preserving errors for fields marked with IsNotNullOrEmpty.

## 1.0.0

- Added VMFramework MCP project tools for GamePrefab creation/inspection, general settings inspection, UI panel inspection, bind object inspection, VisualElementPath validation, container panel inspection, and property manager inspection.
