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
- `com.vm233.unity-mcp` 5.3.4 or newer. This version enforces the `not` and
  `const` constraints used by VMFramework MCP selector schemas before tool
  invocation.
- The VMFramework, VMCore, VM Odin Extensions, and Unity Localization
  dependencies declared by `package.json`.

For reproducible projects, pin a commit in `Packages/manifest.json`:

```json
"com.vm233.vmframework-mcp": "https://github.com/VM233/VMFramework-MCP.git#<commit-hash>"
```

## Project tools

Use `project-tools/list` as the authoritative catalog, `project-tools/get` for
the selected schema, and `project-tools/execute` only after validating the
requested arguments. The package currently publishes these capability
families:

- effective configuration and GeneralSettings discovery;
- GamePrefab type discovery, search, inspection, creation, and atomic update;
- UI panel, bind-object, container-panel, and VisualElementPath inspection;
- PropertyManager reads, runtime writes, and bounded traces;
- GameTag listing, localized upsert, and validation.

Single-panel UI tools require exactly one `panelID` or `prefabPath`.
`vmframework/validate-visual-element-paths` additionally accepts
`allPanels: true` to audit every registered panel and standalone `UIPanel`
prefab. Aggregate results are globally paginated and report missing prefabs,
missing VisualTreeAssets, invalid panels, and invalid paths separately.

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
