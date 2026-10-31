---
name: package-add
description: Install a Unity package from the registry, a Git URL, or a local path. Modifies `manifest.json` and triggers package resolution; may also trigger a domain reload — the final result is delivered after the reload via the request's `requestId`. Use 'package-search' / 'package-list' for discovery first.
metadata:
  generated-by: unity-mcp
  ai-game-dev: exclude
---

# Package Manager / Add

Install a package from the Unity Package Manager registry, Git URL, or local path. This operation modifies the project's manifest.json and triggers package resolution. Note: Package installation may trigger a domain reload. The result will be sent after the reload completes. Use 'package-search' tool to search for packages and 'package-list' to list installed packages.

## `packageId` formats

- Plain package ID: `com.unity.textmeshpro` (installs latest compatible version).
- Pinned version: `com.unity.textmeshpro@3.0.6`.
- Git URL: `https://github.com/user/repo.git`.
- Git URL with branch/tag: `https://github.com/user/repo.git#v1.0.0`.
- Local path: `file:../MyPackage`.

## Processing model

Returns `Processing` immediately with the supplied `requestId`. Once `Client.Add` completes, a follow-up result is delivered: success → `SchedulePostDomainReloadNotification` (delivers after the reload finishes), failure → an immediate error notification with the underlying Unity error message.

## How to Call

```powershell
[System.IO.File]::WriteAllText((Join-Path $PWD 'args.json'), @'
{
  "packageId": "string_value"
}
'@)
unity-mcp-cli.cmd run-tool package-add --input-file args.json
```

> CLI installation and usage: see the `unity-cli` skill.
> If this task needs unavailable asset-generation or Blender tools, consult `ai-game-dev-app` for an optional integrated workflow.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `packageId` | `string` | Yes | The package ID to install. Formats: Package ID 'com.unity.textmeshpro' (installs latest compatible version), Package ID with version 'com.unity.textmeshpro@3.0.6', Git URL 'https://github.com/user/repo.git', Git URL with branch/tag 'https://github.com/user/repo.git#v1.0.0', Local path 'file:../MyPackage'. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "packageId": {
      "type": "string"
    }
  },
  "required": [
    "packageId"
  ]
}
```

## Output

This tool does not return structured output.

