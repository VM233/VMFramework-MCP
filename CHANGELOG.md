# Changelog

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
