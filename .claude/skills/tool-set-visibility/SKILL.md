---
name: tool-set-visibility
description: "Show or hide MCP tools by name in batch: a visible tool is listed to MCP clients (tools/list), a hidden one is left out of the list to save context but still runs when called by name. Persists the change via `UnityMcpPluginEditor.Instance.Save()` only when at least one tool actually flipped. Returns per-input success flags plus optional operation logs."
metadata:
  generated-by: unity-mcp
  ai-game-dev: exclude
---

# Tool / Set Visibility

Show or hide MCP tools by name. Visibility only decides which tools MCP clients see listed (`tools/list`), which keeps their context small; it is not an access control: a hidden tool still runs through `tools/call`.

## Inputs

- `tools` — array of `ToolVisibilityInput { Name, Visible }`. Non-empty.
- `includeLogs` (default `false`) — when true, returns per-step operation logs alongside the success map.

## Behavior

Each entry is resolved against the tool manager's exact-name and case-insensitive lookups. Already-correct state short-circuits as success without writing. The plugin's config is saved once at the end iff at least one tool actually changed state.

`tool-set-enabled-state` is the deprecated old name of this tool (input `{ Name, Enabled }`); it works the same.

## How to Call

```powershell
[System.IO.File]::WriteAllText((Join-Path $PWD 'args.json'), @'
{
  "tools": "string_value",
  "includeLogs": false
}
'@)
unity-mcp-cli.cmd run-tool tool-set-visibility --input-file args.json
```

> CLI installation and usage: see the `unity-cli` skill.
> If this task needs unavailable asset-generation or Blender tools, consult `ai-game-dev-app` for an optional integrated workflow.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `tools` | `string` | Yes | Array of tools with their desired visibility. RQL: [ToolVisibilityInput] |
| `includeLogs` | `boolean` | No | Include operation logs in the result. Default: false |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "tools": {
      "type": "string"
    },
    "includeLogs": {
      "type": "boolean"
    }
  },
  "required": [
    "tools"
  ]
}
```

## Output

This tool does not return structured output.

