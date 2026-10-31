---
name: package-search
description: Search Unity's package registry plus locally installed packages (Git, local, embedded sources) by query string. Returns available versions and installation status. Online mode fetches exact matches from the live registry then supplements with cached substring matches.
metadata:
  generated-by: unity-mcp
  ai-game-dev: exclude
---

# Package Manager / Search

Search for packages in both Unity Package Manager registry and installed packages. Use this to find packages by name before installing them. Returns available versions and installation status. Searches both the Unity registry and locally installed packages (including Git, local, and embedded sources). Results are prioritized: exact name match, exact display name match, name substring, display name substring, description substring. Note: Online mode fetches exact matches from live registry, then supplements with cached substring matches.

## Inputs

- `query` — package id, name, display name, or description keyword (case-insensitive). Required.
- `maxResults` (default 10) — caps the returned list.
- `offlineMode` (default `true`) — when `false`, hits the live registry for exact matches; cached registry data still backs the substring matches in both modes.

## Result composition

Each entry includes name, display name, latest version, truncated description, install status, installed version (if any), and the top-5 compatible versions.

## How to Call

```powershell
[System.IO.File]::WriteAllText((Join-Path $PWD 'args.json'), @'
{
  "query": "string_value",
  "maxResults": 0,
  "offlineMode": false
}
'@)
unity-mcp-cli.cmd run-tool package-search --input-file args.json
```

> CLI installation and usage: see the `unity-cli` skill.
> If this task needs unavailable asset-generation or Blender tools, consult `ai-game-dev-app` for an optional integrated workflow.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `query` | `string` | Yes | The package id, name, or description. Can be: Full package id 'com.unity.textmeshpro', Full package name 'TextMesh Pro', Partial name 'TextMesh' (will search in Unity registry and installed packages), Description keyword 'rendering' (searches in package descriptions). |
| `maxResults` | `integer` | No | Maximum number of results to return. Default: 10 |
| `offlineMode` | `boolean` | No | Whether to perform the search in offline mode (uses cached registry data only). Default: true. Set to false to fetch latest exact matches from Unity registry. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "query": {
      "type": "string"
    },
    "maxResults": {
      "type": "integer"
    },
    "offlineMode": {
      "type": "boolean"
    }
  },
  "required": [
    "query"
  ]
}
```

## Output

This tool does not return structured output.

