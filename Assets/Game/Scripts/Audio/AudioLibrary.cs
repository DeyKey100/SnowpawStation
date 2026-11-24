using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Reads the existing audio folders in the Editor. Player builds read the same
/// catalogue from StreamingAssets; AudioBuildProcessor packages it into build output.
/// No Resources moves, scene edits or asset GUID changes are required.
/// </summary>
public static class AudioLibrary
{
    public const string AudioRoot = "Assets/Game/Audio";
    public const string MusicFolder = AudioRoot + "/Music";
    public const string SfxFolder = AudioRoot + "/Sfx";
    public const string OutputFolder = "SnowpawAudio";

    [Serializable]
    public sealed class Entry
    {
        [SerializeField] private string assetPath;
        [SerializeField] private string file;
        [NonSerialized] private AudioClip clip;
        [NonSerialized] private bool ownsClip;

        public string AssetPath => assetPath ?? "";
        public string File => file ?? "";
        public string Name => clip != null ? clip.name : Path.GetFileNameWithoutExtension(AssetPath);
        public AudioClip Clip => clip;

        public Entry(string path, string relativeFile)
        {
            assetPath = path;
            file = relativeFile;
        }

        public Entry(AudioClip embeddedClip)
        {
            assetPath = "";
            file = "";
            clip = embeddedClip;
        }

        internal void SetClip(AudioClip value, bool owned)
        {
            clip = value;
            ownsClip = owned;
        }

        internal void Release()
        {
            if (ownsClip && clip != null)
            {
                UnityEngine.Object.Destroy(clip);
            }
            // Drop catalogue references in the Editor too, but never destroy an asset.
            // Embedded playlists must keep their direct clip reference for replay.
            if (ownsClip || !string.IsNullOrEmpty(assetPath)) { clip = null; }
            ownsClip = false;
        }
    }

    [Serializable]
    public sealed class Catalog
    {
        [SerializeField] private int version = 1;
        [SerializeField] private Entry[] entries;
        public int Version => version;
        public Entry[] Entries => entries ?? Array.Empty<Entry>();
        public Catalog(Entry[] items) { entries = items; }
    }

    private static readonly List<Entry> entries = new List<Entry>();
    private static readonly Dictionary<string, AudioClip> effects =
        new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<UnityWebRequest> requests = new List<UnityWebRequest>();

    public static bool IsReady { get; private set; }

    internal static void Reset()
    {
        CancelPendingLoads();
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].Release();
        }
        entries.Clear();
        effects.Clear();
        IsReady = false;
    }

    public static IEnumerator Initialize()
    {
        if (IsReady)
        {
            yield break;
        }
#if UNITY_EDITOR
        entries.Clear();
        entries.AddRange(CreateEditorCatalog().Entries);
#else
        UnityWebRequest request = UnityWebRequest.Get(GetUrl("catalog.json"));
        request.timeout = 10;
        requests.Add(request);
        try
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                Catalog catalog = null;
                try { catalog = JsonUtility.FromJson<Catalog>(request.downloadHandler.text); }
                catch (ArgumentException exception) { Debug.LogWarning("[Audio] Invalid catalogue: " + exception.Message); }
                if (catalog != null && catalog.Version == 1)
                {
                    entries.Clear();
                    Entry[] items = catalog.Entries;
                    for (int i = 0; i < items.Length; i++)
                    {
                        Entry item = items[i];
                        // The manifest is data, not an arbitrary filesystem path.
                        if (item != null && item.AssetPath.StartsWith(AudioRoot + "/", StringComparison.Ordinal) &&
                            !item.File.Contains("..") && !Path.IsPathRooted(item.File))
                        {
                            entries.Add(item);
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("[Audio] Folder catalogue unavailable. Embedded playlist clips remain usable. " + request.error);
            }
        }
        finally
        {
            if (requests.Remove(request)) { request.Dispose(); }
        }

        // Short effects are cached once. Music is loaded only when selected and
        // released after its crossfade, rather than loading the whole music folder.
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (IsSfx(entry))
            {
                yield return LoadClip(entry);
                CacheEffect(entry);
            }
        }
#endif
        IsReady = true;
    }

    public static string NormalizeFolder(string folder)
    {
        string path = (folder ?? "").Trim().Replace('\\', '/').TrimEnd('/');
        if (path == "Music" || path.StartsWith("Music/", StringComparison.Ordinal))
        {
            path = AudioRoot + "/" + path;
        }
        return path;
    }

    public static bool IsMusicFolder(string folder)
    {
        string path = NormalizeFolder(folder);
        return !path.Contains("..") &&
            (path == MusicFolder || path.StartsWith(MusicFolder + "/", StringComparison.Ordinal));
    }

    public static void FillPlaylist(string folder, List<Entry> destination)
    {
        destination.Clear();
        string prefix = NormalizeFolder(folder) + "/";
        if (!IsMusicFolder(folder)) { return; }
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].AssetPath.StartsWith(prefix, StringComparison.Ordinal))
            {
                destination.Add(entries[i]);
            }
        }
    }

    public static AudioClip FindSfx(string relativePathOrName)
    {
        if (string.IsNullOrWhiteSpace(relativePathOrName)) { return null; }
        string key = relativePathOrName.Trim().Replace('\\', '/');
        if (effects.TryGetValue(key, out AudioClip cached) && cached != null) { return cached; }
#if UNITY_EDITOR
        // Editor clips remain assets; they must never be destroyed by the pool.
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (IsSfx(entry) && MatchesEffect(entry, key))
            {
                if (entry.Clip == null)
                {
                    entry.SetClip(AssetDatabase.LoadAssetAtPath<AudioClip>(entry.AssetPath), false);
                }
                CacheEffect(entry);
                return entry.Clip;
            }
        }
#endif
        return null;
    }

    private static bool IsSfx(Entry entry) => entry.AssetPath.StartsWith(SfxFolder + "/", StringComparison.Ordinal);

    private static bool MatchesEffect(Entry entry, string key)
    {
        return string.Equals(entry.AssetPath, key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.File, key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.AssetPath.Substring(SfxFolder.Length + 1), key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(entry.AssetPath), key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.Name, key, StringComparison.OrdinalIgnoreCase);
    }

    private static void CacheEffect(Entry entry)
    {
        if (entry.Clip == null) { return; }
        effects[entry.AssetPath] = entry.Clip;
        effects[entry.File] = entry.Clip;
        effects[entry.AssetPath.Substring(SfxFolder.Length + 1)] = entry.Clip;
        string filename = Path.GetFileName(entry.AssetPath);
        if (!effects.ContainsKey(filename)) { effects.Add(filename, entry.Clip); }
        if (!effects.ContainsKey(entry.Name)) { effects.Add(entry.Name, entry.Clip); }
    }

    internal static IEnumerator LoadClip(Entry entry)
    {
        if (entry.Clip != null) { yield break; }
#if UNITY_EDITOR
        entry.SetClip(AssetDatabase.LoadAssetAtPath<AudioClip>(entry.AssetPath), false);
        yield break;
#else
        if (!TryGetAudioType(entry.File, out AudioType type)) { yield break; }
        UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(GetUrl(entry.File), type);
        request.timeout = 15;
        if (!IsSfx(entry)) { ((DownloadHandlerAudioClip)request.downloadHandler).compressed = true; }
        requests.Add(request);
        try
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                clip.name = entry.Name;
                entry.SetClip(clip, true);
            }
            else
            {
                Debug.LogWarning("[Audio] Could not load '" + entry.AssetPath + "': " + request.error);
            }
        }
        finally
        {
            if (requests.Remove(request)) { request.Dispose(); }
        }
#endif
    }

    internal static void CancelPendingLoads()
    {
        for (int i = 0; i < requests.Count; i++)
        {
            requests[i].Abort();
            requests[i].Dispose();
        }
        requests.Clear();
    }

    public static bool TryGetAudioType(string path, out AudioType type)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".ogg": type = AudioType.OGGVORBIS; return true;
            case ".wav": type = AudioType.WAV; return true;
            case ".mp3": type = AudioType.MPEG; return true;
            case ".aif": case ".aiff": type = AudioType.AIFF; return true;
            default: type = AudioType.UNKNOWN; return false;
        }
    }

    private static string GetUrl(string file)
    {
        string root = Application.streamingAssetsPath.TrimEnd('/') + "/" + OutputFolder;
        if (root.Contains("://") || root.StartsWith("jar:", StringComparison.Ordinal))
        {
            string[] segments = file.Replace('\\', '/').Split('/');
            for (int i = 0; i < segments.Length; i++) { segments[i] = Uri.EscapeDataString(segments[i]); }
            return root + "/" + string.Join("/", segments);
        }
        return new Uri(Path.GetFullPath(Path.Combine(root, file))).AbsoluteUri;
    }

#if UNITY_EDITOR
    public static Catalog CreateEditorCatalog()
    {
        if (!AssetDatabase.IsValidFolder(AudioRoot)) { return new Catalog(Array.Empty<Entry>()); }
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot });
        List<Entry> result = new List<Entry>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (TryGetAudioType(path, out _) &&
                (path.StartsWith(MusicFolder + "/", StringComparison.Ordinal) || path.StartsWith(SfxFolder + "/", StringComparison.Ordinal)))
            {
                result.Add(new Entry(path, path.Substring(AudioRoot.Length + 1)));
            }
        }
        result.Sort((a, b) => string.Compare(a.AssetPath, b.AssetPath, StringComparison.Ordinal));
        return new Catalog(result.ToArray());
    }
#endif
}
