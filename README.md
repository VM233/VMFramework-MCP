# VMFramework MCP

VMFramework MCP is an Editor-only Unity package that adds VMFramework-aware
project tools to [VM Unity MCP](https://github.com/VM233/VMUnityMCP). It works
with VMFramework's public concepts—GamePrefabs, GeneralSettings, GameTags,
UI panels, containers, and properties—instead of asking callers to edit
internal serialized fields.

The tools use Unity MCP's bounded three-stage project-tool contract:

1. `project-tools/list` returns compact summaries.
2. `project-tools/get` returns the selected tool's complete schema.
3. `project-tools/execute` validates and executes it.

The companion Node server deliberately does not publish project-specific tools
as permanent concrete tools because the active Unity project can change.

## Requirements and installation

- Unity 6000.4 or newer.
- `com.vm233.unity-mcp` 5.3.1 or newer.
- The VMFramework, VMCore, VM Odin Extensions, and Unity Localization
  dependencies declared by `package.json`.

For reproducible projects, pin a commit in `Packages/manifest.json`:

```json
"com.vm233.vmframework-mcp": "https://github.com/VM233/VMFramework-MCP.git#<commit-hash>"
```

## Project tools

| Area | Tool | Behavior |
|---|---|---|
| Configuration | `vmframework/get-configuration` | Read effective VMFramework MCP settings and the shared Unity MCP result budget. |
| GamePrefab | `vmframework/list-game-prefab-types` | List bounded GamePrefab types and matching GeneralSettings. |
| GamePrefab | `vmframework/add-game-prefab` | Create or replace a single wrapper and register it. |
| GamePrefab | `vmframework/find-game-prefab` | Search GamePrefabs with pagination. |
| GamePrefab | `vmframework/inspect-game-prefab-wrapper` | Inspect exact or paginated wrapper assets. |
| GamePrefab | `vmframework/inspect-game-prefab` | Inspect one serialized GamePrefab with configurable depth budgets. |
| GamePrefab | `vmframework/update-game-prefab` | Apply ordered atomic edits with rollback and a semantic diff. |
| General settings | `vmframework/list-general-settings` | List discoverable GeneralSettings; provider detail is opt-in. |
| UI | `vmframework/inspect-ui-panel` | Inspect panel config, prefab, bind objects, and optional runtime state. |
| UI | `vmframework/inspect-bind-objects` | Inspect bind-object contracts and optional runtime counts. |
| UI | `vmframework/validate-visual-element-paths` | Validate bounded VisualElementPath results against the panel UXML. |
| UI | `vmframework/inspect-container-panel` | Inspect container modifiers and optional runtime state. |
| Properties | `vmframework/inspect-property-manager` | Inspect PropertyManagers from an explicit target or loaded scenes. |
| Properties | `vmframework/get-property` | Read one typed property. |
| Properties | `vmframework/set-property` | Set one runtime property in Play Mode. |
| Properties | `vmframework/start-property-trace` | Start a bounded property dirty-event trace. |
| Properties | `vmframework/get-property-trace` | Read a paginated trace without changing it. |
| Properties | `vmframework/stop-property-trace` | Stop tracing and return a paginated retained page. |
| GameTag | `vmframework/list-game-tags` | List registered tags; locale values are opt-in. |
| GameTag | `vmframework/upsert-game-tag` | Upsert a tag and localized values with dry-run support. |
| GameTag | `vmframework/validate-game-tags` | Validate IDs, localization, and GamePrefab references. |

All schemas reject unknown business arguments. Selectors, paths, IDs,
transaction operations, `dryRun`, overwrite choices, and registration choices
remain explicit per request.

## Configuration

Effective defaults use this order:

1. explicit tool argument;
2. `Project Settings > VMFramework MCP`;
3. `Preferences > VMFramework MCP` or the shared
   `Preferences > Unity MCP` result budget;
4. package default.

`ProjectSettings/VMFrameworkMCPSettings.json` is team-owned and contains only
the GameTag validation coverage contract:

```json
{
  "schemaVersion": 1,
  "gameTagValidation": {
    "includeMissingTranslations": true,
    "includeGamePrefabReferences": true
  }
}
```

`Preferences > VMFramework MCP` contains operator response choices:
GamePrefab inspection depth, per-collection item budget, optional update
snapshots, and the retained property-trace capacity.

Single-primary-collection tools reuse the optional result-limit override under
`Preferences > Unity MCP > Tool Responses`. VMFramework MCP does not duplicate
that preference.

Large or nondeterministic details stay request-owned and default off:

- `includeGamePrefabDetails`;
- `includeRuntime`;
- `includeValid`;
- `includeLocalizations`;
- `includeSnapshots`;
- `includeValidation` on `upsert-game-tag`.

See [Documentation~/configuration.md](Documentation~/configuration.md) for the
per-tool ownership audit and response rules.

## Development

Run the package's EditMode tests through Unity MCP's
`testing/run-package-tests` workflow. The regression suite verifies the exact
VMFramework tool catalog, operation metadata, strict schemas, settings
round-tripping, and GamePrefab/GameTag conversion behavior.

This package contains no runtime assembly.
