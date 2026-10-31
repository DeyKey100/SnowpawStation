---
name: script-update-or-create
description: Write a `.cs` script file (create or overwrite) with the provided C# code. Validates syntax via Roslyn before write — invalid code is rejected with error details and the file is left untouched. Refreshes the AssetDatabase and delivers the final result via `requestId` after Unity finishes the triggered compilation. Use 'script-read' to inspect existing content first.
metadata:
  generated-by: unity-mcp
  ai-game-dev: exclude
---

# Script / Update or Create

Updates or creates script file with the provided C# code. Does AssetDatabase.Refresh() at the end. Provides compilation error details if the code has syntax errors. Use 'script-read' tool to read existing script files first.

## Inputs

- `filePath` — required `.cs` path.
- `content` — C# source. MUST pass `ScriptUtils.IsValidCSharpSyntax`.
- `requestId` — required for the processing/delivered-later contract.

## Behavior

Creates any missing parent directories, writes the file, then calls `AssetDatabase.Refresh` and schedules a post-compilation notification so the final response is delivered after Unity finishes the recompile.

## How to Call

```powershell
[System.IO.File]::WriteAllText((Join-Path $PWD 'args.json'), @'
{
  "filePath": "string_value",
  "content": "string_value"
}
'@)
unity-mcp-cli.cmd run-tool script-update-or-create --input-file args.json
```

> CLI installation and usage: see the `unity-cli` skill.
> If this task needs unavailable asset-generation or Blender tools, consult `ai-game-dev-app` for an optional integrated workflow.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `filePath` | `string` | Yes | The path to the file. Sample: "Assets/Scripts/MyScript.cs". |
| `content` | `string` | Yes | C# code - content of the file. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "filePath": {
      "type": "string"
    },
    "content": {
      "type": "string"
    }
  },
  "required": [
    "filePath",
    "content"
  ]
}
```

## Output

This tool does not return structured output.

