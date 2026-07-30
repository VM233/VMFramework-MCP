# VMFramework MCP configuration and project-tool audit

This document is the configuration review for the VMFramework MCP project-tool
catalog. The tool-catalog regression test owns the exact list, requires one
operation kind per tool, requires strict root schemas, and keeps every tool
behind Unity MCP's `list -> get -> execute` project-extension contract.

## Ownership and precedence

The effective value order is:

1. explicit project-tool argument;
2. team setting in `ProjectSettings/VMFrameworkMCPSettings.json`;
3. user preference in `Preferences > VMFramework MCP`, or the shared
   `Preferences > Unity MCP > Tool Responses` result budget;
4. built-in default.

Hard caps, Play Mode requirements, rollback behavior, selector ambiguity, and
schema validation are invariants rather than settings.

### Team settings

Only GameTag validation coverage is team-owned:

| JSON field | Initial value | Consumer |
|---|---:|---|
| `gameTagValidation.includeMissingTranslations` | `true` | `validate-game-tags` |
| `gameTagValidation.includeGamePrefabReferences` | `true` | `validate-game-tags` |

These values define what the team's normal content audit covers. An explicit
tool argument still wins for a one-off focused validation.

### User preferences

| Preference | Initial value | Consumers |
|---|---:|---|
| GamePrefab inspection max depth | `8` | `inspect-game-prefab`, `update-game-prefab` |
| GamePrefab collection item limit | `100` | `inspect-game-prefab`, `update-game-prefab` |
| Include GamePrefab update snapshots | `false` | `update-game-prefab` |
| Property trace retained event limit | `1000` | `start-property-trace` |

These control local response size or diagnostic capacity, not project content.

The shared Unity MCP result preference is consumed by tools with one obvious
primary result collection: GamePrefab type/list/search tools, GeneralSettings,
VisualElementPath results, PropertyManagers, GameTags, GameTag issues, and
property-trace event pages. Explicit `limit` or `maxIssues` values win, and
each VMFramework tool keeps its own hard maximum.

## Per-tool review

| Tool | Configurable default | Explicit-only fields and decision |
|---|---|---|
| `get-configuration` | None | Read-only effective snapshot; accepts no arguments. |
| `list-game-prefab-types` | Shared result limit | Filter and abstract/interface inclusion change the requested set and remain explicit. |
| `add-game-prefab` | None | ID, type, overwrite, asset name, and serialized values define an asset mutation. Asset folders remain authoritative in VMFramework GeneralSettings. |
| `find-game-prefab` | Shared result limit | ID, filter, and type are selectors. |
| `inspect-game-prefab-wrapper` | Shared result limit | Exact ID/path and filter are selectors. Missing exact targets now fail instead of looking like an empty broad query. |
| `list-general-settings` | Shared result limit | `includeGamePrefabDetails` can repeat large provider lists and defaults off. |
| `inspect-ui-panel` | None | Exactly one `panelID` or `prefabPath` is required; runtime-state inclusion is request-owned and defaults off. |
| `inspect-bind-objects` | None | Exactly one `panelID` or `prefabPath` is required; runtime counts are request-owned and default off. |
| `validate-visual-element-paths` | Shared result limit | Exactly one `panelID`, one `prefabPath`, or `allPanels: true` is required. Valid records default off; all-panel output uses one global page, separates source errors from invalid paths, and excludes fields disabled by resolvable Odin `ShowIf`/`HideIf` conditions. |
| `inspect-container-panel` | None | Exactly one `panelID` or `prefabPath` is required; runtime state is request-owned and defaults off. |
| `inspect-property-manager` | Shared result limit | Target, child traversal, property filter, and selection usage remain explicit. Omitted selectors scan loaded scenes rather than depending on hidden Editor selection. |
| `inspect-game-prefab` | VMFramework depth/item preferences | Exact ID is a selector. |
| `update-game-prefab` | VMFramework depth/item/snapshot preferences | ID and ordered operations define the mutation. Complete snapshots default off; bounded operation summaries and semantic diff remain. |
| `list-game-tags` | Shared result limit | ID/group/filter and locale-value expansion remain explicit; locale values default off. |
| `upsert-game-tag` | None | Group, ID, localization keys/values, registration, and dry-run choices define the mutation. Framework `GameTagGeneralSetting` remains the localization-table authority. Global post-validation is opt-in because the dedicated validation tool owns normal audits. |
| `validate-game-tags` | Team validation coverage; shared issue limit | Explicit coverage flags can narrow one call. |
| `get-property` | None | Manager and property selectors remain explicit. |
| `set-property` | None | Target, value, and `initial` are runtime mutation inputs. The tool is classified as runtime-mutating and requires Play Mode. |
| `start-property-trace` | VMFramework retained-event preference | Target/filter/child traversal remain explicit. Starting a trace mutates diagnostic session state and is not read-only. |
| `get-property-trace` | Shared result limit | Offset/limit select a page; the call no longer exposes a hidden clear mutation. |
| `stop-property-trace` | Shared result limit | Stopping mutates diagnostic session state; returned events are paginated. |

## Values deliberately not configurable

The following remain request-owned or invariant:

- asset paths, scene paths, object IDs, GamePrefab IDs/types, GameTag groups,
  property names, and filters;
- `overwrite`, `registerGroup`, `dryRun`, `initial`, ordered operations, and
  mutation values;
- large diagnostic expansions (`includeRuntime`, all locale values, all valid
  paths, complete before/after snapshots, and global validation after upsert);
- hard result/depth/event caps, strict unknown-argument rejection, exact target
  ambiguity failures, Play Mode safety, rollback, and readback verification.

Duplicating VMFramework content settings inside MCP is also prohibited.
GamePrefab folders and the default GameTag localization table continue to come
from their owning VMFramework GeneralSettings.

## Response contract

- List and trace tools expose pagination metadata only when another page
  exists; Unity MCP's transport compactor removes redundant completed-page
  aliases.
- A zero-match primary collection is preserved as an empty collection, so a
  completed queue ticket never loses the semantic result.
- GamePrefab update replies always retain bounded operation summaries and a
  semantic diff. Complete before/after snapshots are opt-in.
- Upsert replies contain focused readback. A potentially large global GameTag
  validation is opt-in or obtained from `validate-game-tags`.
- GameTag validation replies include the effective coverage flags so callers
  can distinguish team defaults from an explicitly narrowed audit.
