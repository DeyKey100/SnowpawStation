---
name: editor-session-quit
description: Save and quit an editor that a CLI session started (`unity-mcp-cli exec`, `open --headless`). Needs the session lease nonce; an editor the user opened is never closed. Used by `unity-mcp-cli close`.
metadata:
  generated-by: unity-mcp
  ai-game-dev: exclude
---

# Editor Session / Quit

Save and quit an editor that a CLI session started.

## Inputs

- `nonce` — the `nonce` of the project's session lease (`.ai-game-dev/session.json`), which the CLI also passed to the editor as `AIGD_SESSION_NONCE`.

## Behavior

Refuses unless this editor was started by a CLI session (`AIGD_SESSION_OWNER=cli`) with the same nonce. Otherwise answers right away, then saves changed scenes that have a file and changed assets and exits — as the idle shutdown does. Unlike the OS polite-quit signal it also reaches a headless (`-batchmode`) editor. Prefer `unity-mcp-cli close --owned-only` over calling it directly.

## How to Call

```powershell
[System.IO.File]::WriteAllText((Join-Path $PWD 'args.json'), @'
{
  "nonce": "string_value"
}
'@)
unity-mcp-cli.cmd run-system-tool editor-session-quit --input-file args.json
```

> CLI installation and usage: see the `unity-cli` skill.
> If this task needs unavailable asset-generation or Blender tools, consult `ai-game-dev-app` for an optional integrated workflow.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `nonce` | `string` | Yes | The nonce of the project's CLI session lease (.ai-game-dev/session.json). |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "nonce": {
      "type": "string"
    }
  },
  "required": [
    "nonce"
  ]
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

