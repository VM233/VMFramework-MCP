# VMFramework MCP

VMFramework MCP is a Unity package that exposes VMFramework-specific editor workflows as first-class Unity MCP project tools.

The package depends on:

- `com.anklebreaker.unity-mcp`
- `com.vm233.vmcore`
- `com.vm233.vmframework`

## Tools

- `vmframework/list-game-prefab-types`
- `vmframework/add-game-prefab`
- `vmframework/find-game-prefab`
- `vmframework/inspect-game-prefab-wrapper`
- `vmframework/list-general-settings`
- `vmframework/inspect-ui-panel`
- `vmframework/inspect-bind-objects`
- `vmframework/validate-visual-element-paths`
- `vmframework/inspect-container-panel`
- `vmframework/inspect-property-manager`
- `vmframework/list-game-tags`
- `vmframework/upsert-game-tag`
- `vmframework/validate-game-tags`

These tools are declared through `MCPProjectToolAttribute`, so MCP clients can see their names, descriptions, and schemas directly through the Unity MCP metadata endpoint.

## Usage

Add the package as a Git dependency in `Packages/manifest.json`:

```json
"com.vm233.vmframework-mcp": "https://github.com/VM233/VMFramework-MCP.git#<commit>"
```

The package is Editor-only and does not add runtime code.
