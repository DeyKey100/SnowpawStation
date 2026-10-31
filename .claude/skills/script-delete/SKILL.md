---
name: script-delete
description: Delete one or more `.cs` script files from disk, refresh the AssetDatabase, and wait for Unity compilation to settle before delivering the final result via the request's `requestId`. Pair with 'script-read' to inspect files before deletion.
metadata:
  generated-by: unity-mcp
  ai-game-dev: exclude
---

# Script / Delete

Delete the script file(s). Does AssetDatabase.Refresh() and waits for Unity compilation to complete before reporting results. Use 'script-read' tool to read existing script files first.

## Inputs

- `files` — non-empty array of `.cs` paths. Every entry must exist on disk.
- `requestId` — required for the processing/delivered-later contract.

## Behavior

Validates the array (non-empty, every entry ends with `.cs`, every entry exists). Deletes each file plus its sibling `.meta` (when present). Calls `AssetDatabase.Refresh` and schedules a post-compilation notification — the final response is delivered after Unity finishes the recompile triggered by the delete.

## How to Call

```powershell
[System.IO.File]::WriteAllText((Join-Path $PWD 'args.json'), @'
{
  "files": []
}
'@)
unity-mcp-cli.cmd run-tool script-delete --input-file args.json
```

> CLI installation and usage: see the `unity-cli` skill.
> If this task needs unavailable asset-generation or Blender tools, consult `ai-game-dev-app` for an optional integrated workflow.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `files` | `array` | Yes | File paths to the files. Sample: "Assets/Scripts/MyScript.cs". |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "files": {
      "type": "array",
      "items": {
        "type": "string"
      }
    }
  },
  "required": [
    "files"
  ]
}
```

## Output

This tool does not return structured output.

