#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Packages folder audio into a standalone player's OUTPUT, never into project
/// Assets/StreamingAssets and never by rewriting scenes, prefabs or .meta files.
/// The original audio files and their GUIDs remain untouched.
/// </summary>
public sealed class AudioBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 100;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!IsStandalone(report.summary.platform))
        {
            Debug.LogWarning("[Audio] Automatic folder packaging supports Windows, Linux and macOS standalone players. " +
                "For other targets use AudioManager's embedded Playlist Clips (or supply the catalogue in StreamingAssets).");
        }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (!IsStandalone(report.summary.platform)) { return; }
        string output = Path.GetFullPath(report.summary.outputPath);
        string dataDirectory = report.summary.platform == BuildTarget.StandaloneOSX
            ? Path.Combine(output, "Contents", "Resources", "Data")
            : Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "_Data");
        string destination = Path.GetFullPath(Path.Combine(dataDirectory, "StreamingAssets", AudioLibrary.OutputFolder));
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        // A misconfigured build location must not overwrite project assets or caches.
        string[] protectedDirectories = { "Assets", "Packages", "ProjectSettings", "Library", "Temp", "Logs" };
        for (int i = 0; i < protectedDirectories.Length; i++)
        {
            if (IsInside(destination, Path.Combine(projectRoot, protectedDirectories[i])))
            {
                throw new BuildFailedException("[Audio] Build audio output resolves into a protected project directory: " + destination);
            }
        }
        if (!Directory.Exists(dataDirectory))
        {
            throw new BuildFailedException("[Audio] Player data directory not found: " + dataDirectory);
        }

        AudioLibrary.Catalog catalog = AudioLibrary.CreateEditorCatalog();
        Directory.CreateDirectory(destination);
        AudioLibrary.Entry[] entries = catalog.Entries;
        for (int i = 0; i < entries.Length; i++)
        {
            AudioLibrary.Entry entry = entries[i];
            string source = Path.GetFullPath(Path.Combine(projectRoot, entry.AssetPath));
            string target = Path.GetFullPath(Path.Combine(destination, entry.File));
            if (!IsInside(source, Path.Combine(projectRoot, AudioLibrary.AudioRoot)) || !IsInside(target, destination))
            {
                throw new BuildFailedException("[Audio] Invalid catalogue path: " + entry.AssetPath);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target, true);
        }
        File.WriteAllText(Path.Combine(destination, "catalog.json"), JsonUtility.ToJson(catalog, true), new UTF8Encoding(false));
        Debug.Log("[Audio] Packaged " + entries.Length + " audio files into standalone StreamingAssets.");
    }

    private static bool IsStandalone(BuildTarget target) => target == BuildTarget.StandaloneWindows ||
        target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneLinux64 || target == BuildTarget.StandaloneOSX;

    private static bool IsInside(string path, string directory)
    {
        string prefix = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
#endif
