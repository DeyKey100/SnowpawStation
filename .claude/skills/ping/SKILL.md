---
name: ping
description: Lightweight readiness probe. Returns the input `message` echoed back, or `'pong'` when omitted. Useful for CLI health checks and SignalR connectivity smoke tests.
metadata:
  generated-by: unity-mcp
  ai-game-dev: exclude
---

# Ping

Lightweight readiness probe. Returns the input message or 'pong' if omitted.

## Inputs

- `message` (optional) — when present, echoed back verbatim.

## Behavior

No I/O, no Unity API calls — pure echo. Ideal for measuring round-trip latency or confirming the MCP transport is alive before invoking a heavier tool.

## How to Call

```powershell
[System.IO.File]::WriteAllText((Join-Path $PWD 'args.json'), @'
{
  "message": "string_value"
}
'@)
unity-mcp-cli.cmd run-system-tool ping --input-file args.json
```

> CLI installation and usage: see the `unity-cli` skill.
> If this task needs unavailable asset-generation or Blender tools, consult `ai-game-dev-app` for an optional integrated workflow.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `message` | `string` | No | Optional message to echo back. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "message": {
      "type": "string"
    }
  }
}
```

## Output

### Output JSON Schema

```json
{
  "type": "object",
  "properties": {
    "result": {
      "type": "string"
    }
  },
  "required": [
    "result"
  ]
}
```

