---
name: script-read
description: Read a `.cs` script file and return its content as a string. Supports a 1-based `lineFrom`/`lineTo` slice for partial reads. Pair with 'script-update-or-create' to write back.
metadata:
  generated-by: unity-mcp
  ai-game-dev: exclude
---

# Script / Read

Reads the content of a script file and returns it as a string. Use 'script-update-or-create' tool to update or create script files.

## Inputs

- `filePath` — required `.cs` path. Throws if missing on disk.
- `lineFrom` (default 1, 1-based) — start line; clamped into valid range.
- `lineTo` (default -1 = end-of-file, 1-based) — inclusive end line; clamped into valid range.

## Behavior

Reads the file with `File.ReadAllLines` and slices `[lineFrom..lineTo]` (inclusive). The slice indices are clamped — passing out-of-range `lineFrom`/`lineTo` is forgiving (read returns at-most the whole file).

## How to Call

```powershell
[System.IO.File]::WriteAllText((Join-Path $PWD 'args.json'), @'
{
  "filePath": "string_value",
  "lineFrom": 0,
  "lineTo": 0
}
'@)
unity-mcp-cli.cmd run-tool script-read --input-file args.json
```

> CLI installation and usage: see the `unity-cli` skill.
> If this task needs unavailable asset-generation or Blender tools, consult `ai-game-dev-app` for an optional integrated workflow.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `filePath` | `string` | Yes | The path to the file. Sample: "Assets/Scripts/MyScript.cs". |
| `lineFrom` | `integer` | No | The line number to start reading from (1-based). |
| `lineTo` | `integer` | No | The line number to stop reading at (1-based, -1 for all lines). |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "filePath": {
      "type": "string"
    },
    "lineFrom": {
      "type": "integer"
    },
    "lineTo": {
      "type": "integer"
    }
  },
  "required": [
    "filePath"
  ]
}
```

## Output

This tool does not return structured output.

