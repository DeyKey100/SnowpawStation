---
name: tool-set-enabled-state
description: "DEPRECATED: use `tool-set-visibility`. Shows (Enabled=true) or hides (Enabled=false) MCP tools by name in batch; a hidden tool is only left out of tools/list and still runs when called by name. Persists the change via `UnityMcpPluginEditor.Instance.Save()` only when at least one tool actually flipped. Returns per-input success flags plus optional operation logs."
metadata:
  generated-by: unity-mcp
  ai-game-dev: exclude
---

# Tool / Set Enabled State (deprecated: tool-set-visibility)

Deprecated old name of `tool-set-visibility`, kept working until the next major release. `Enabled` means visible: the flag only decides whether MCP clients see the tool listed (`tools/list`); a hidden tool still runs through `tools/call`.

## Inputs

- `tools` — array of `ToolToggleInput { Name, Enabled }`. Non-empty.
- `includeLogs` (default `false`) — when true, returns per-step operation logs alongside the success map.

## Behavior

Same as `tool-set-visibility`: each entry is resolved against the tool manager's exact-name and case-insensitive lookups, already-correct state short-circuits as success, and the config is saved once at the end iff a tool changed.

## How to Call

```powershell
[System.IO.File]::WriteAllText((Join-Path $PWD 'args.json'), @'
{
  "tools": "string_value",
  "includeLogs": false
}
'@)
unity-mcp-cli.cmd run-tool tool-set-enabled-state --input-file args.json
```

> CLI installation and usage: see the `unity-cli` skill.
> If this task needs unavailable asset-generation or Blender tools, consult `ai-game-dev-app` for an optional integrated workflow.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `tools` | `string` | Yes | Array of tools with their desired visibility (Enabled = visible). RQL: [ToolToggleInput] |
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

