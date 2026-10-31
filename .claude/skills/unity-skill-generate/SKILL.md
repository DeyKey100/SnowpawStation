---
name: unity-skill-generate
description: "Regenerate every `SKILL.md` from the project's currently-registered MCP tools into the configured skills folder (or a project-relative override path). Writes the YAML `description:` from `[AiSkillDescription]` and the body from `[AiSkillBody]`."
metadata:
  generated-by: unity-mcp
  ai-game-dev: exclude
---

# Skill (Tool) / Generate All

Generate all skills from the existed Tools in the Unity Project.

## Inputs

- `path` (optional) — project-relative skills folder (e.g. `.claude/skills`). Absolute paths and `..` traversal segments are rejected. When null/empty, the editor's configured `SkillsRootFolderAbsolutePath` is used.

## Behavior

Creates the destination folder if missing, then invokes `McpPluginInstance.GenerateSkillFiles(...)` to emit a `SKILL.md` per registered MCP tool. The plugin's `SkillsPath` is temporarily swapped to the target folder and restored in `finally` so the on-disk configuration is unchanged after the call returns.

## How to Call

```powershell
[System.IO.File]::WriteAllText((Join-Path $PWD 'args.json'), @'
{
  "path": "string_value"
}
'@)
unity-mcp-cli.cmd run-system-tool unity-skill-generate --input-file args.json
```

> CLI installation and usage: see the `unity-cli` skill.
> If this task needs unavailable asset-generation or Blender tools, consult `ai-game-dev-app` for an optional integrated workflow.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `path` | `string` | No | Path to the skills folder. If null or empty, the default path will be used. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "path": {
      "type": "string"
    }
  }
}
```

## Output

This tool does not return structured output.

