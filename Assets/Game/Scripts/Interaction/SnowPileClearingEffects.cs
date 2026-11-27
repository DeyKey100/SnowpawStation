using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Runtime-only, owner-scoped pool. Never edits the mound mesh or creates saved assets.</summary>
internal sealed class SnowPileClearingEffects
{
    private const int ChunkCount = 4;
    private const float ScrapeSeconds = 0.29f;
    private readonly GameObject root;
    private readonly Transform[] chunks = new Transform[ChunkCount];
    private readonly Vector3[] origins = new Vector3[ChunkCount];
    private readonly Vector3[] travels = new Vector3[ChunkCount];
    private readonly Mesh chunkMesh;
    private readonly AudioClip scrapeClip;
    private AudioManager audio;
    private int voiceHandle;
    private float burstStart;
    private float burstLifetime;

    public SnowPileClearingEffects(GameObject owner, Material snowMaterial)
    {
        // A scene root, not a child of the pile: shrinking its root must not squash the chunks.
        root = new GameObject("SnowClearingFX (runtime)") { hideFlags = HideFlags.DontSave };
        root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, owner.scene);
        chunkMesh = CreateChunkMesh();
        for (int i = 0; i < ChunkCount; i++)
        {
            GameObject chunk = new GameObject("SnowChunk_" + i);
            chunks[i] = chunk.transform;
            chunks[i].SetParent(root.transform, false);
            chunk.AddComponent<MeshFilter>().sharedMesh = chunkMesh;
            MeshRenderer renderer = chunk.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = snowMaterial;
            renderer.enabled = snowMaterial != null;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        scrapeClip = CreateScrapeClip();
        audio = AudioManager.EnsureInstance();
    }

    public void PlayStroke(Bounds moundBounds, Vector3 forward, int stroke, float elapsed, float interval)
    {
        Reset();
        burstStart = elapsed;
        burstLifetime = Mathf.Min(0.32f, interval * 0.85f);
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        Vector3 direction = (forward + side * (stroke == 1 ? -0.45f : 0.45f)).normalized;
        for (int i = 0; i < ChunkCount; i++)
        {
            float spread = (i - 1.5f) / 1.5f;
            origins[i] = moundBounds.center + Vector3.up * (moundBounds.extents.y * 0.4f)
                + side * (spread * Mathf.Min(moundBounds.extents.x * 0.55f, 0.45f));
            travels[i] = direction * (0.25f + 0.065f * i) + side * (spread * 0.16f);
            chunks[i].position = origins[i];
            chunks[i].rotation = Quaternion.Euler(i * 43f, stroke * 51f + i * 67f, i * 29f);
            chunks[i].localScale = Vector3.one * (0.07f + 0.012f * i);
        }
        root.SetActive(true);
        // Small pitch variation, never chipmunk-speed. Short settings cannot overlap voices.
        if (audio == null) { audio = AudioManager.Instance; }
        if (audio != null)
        {
            float pitch = stroke == 1 ? 0.97f : stroke == 2 ? 1.03f : 1f;
            voiceHandle = audio.PlaySfx(scrapeClip, moundBounds.center, 0.55f, 1f, pitch,
                false, 1.5f, 12f);
        }
    }

    public void Advance(float elapsed)
    {
        if (root == null || !root.activeSelf) { return; }
        float t = Mathf.Clamp01((elapsed - burstStart) / Mathf.Max(0.001f, burstLifetime));
        if (t >= 1f)
        {
            root.SetActive(false);
            return;
        }
        // Cosmetic arcs, not rigidbody simulation. Opaque chunks shrink only at the tail.
        float shrink = 1f - Mathf.InverseLerp(0.65f, 1f, t);
        for (int i = 0; i < ChunkCount; i++)
        {
            chunks[i].position = origins[i] + travels[i] * t
                + Vector3.up * ((0.12f + 0.025f * i) * 4f * t * (1f - t) - 0.04f * t);
            chunks[i].localScale = Vector3.one * ((0.07f + 0.012f * i) * shrink);
        }
    }

    public void Reset()
    {
        if (root != null) { root.SetActive(false); }
        if (audio != null && voiceHandle > 0) { audio.StopSfx(voiceHandle); }
        voiceHandle = 0;
    }

    public void Dispose()
    {
        Reset();
        // Only runtime objects owned by this helper; no shared materials or imported assets.
        if (root != null) { Object.Destroy(root); }
        if (chunkMesh != null) { Object.Destroy(chunkMesh); }
        if (scrapeClip != null) { Object.Destroy(scrapeClip); }
    }

    private static Mesh CreateChunkMesh()
    {
        Vector3 a = new Vector3(0f, 0.65f, 0f);
        Vector3 b = new Vector3(-0.55f, -0.35f, -0.45f);
        Vector3 c = new Vector3(0.55f, -0.35f, -0.45f);
        Vector3 d = new Vector3(0f, -0.35f, 0.6f);
        // Separate vertices per face keep the four faces flat shaded.
        Mesh mesh = new Mesh
        {
            name = "SnowChunk (runtime)",
            hideFlags = HideFlags.DontSave,
            vertices = new[] { a, c, b, a, d, c, a, b, d, b, c, d },
            triangles = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
            uv = new[]
            {
                Vector2.up, Vector2.right, Vector2.zero,
                Vector2.up, Vector2.right, Vector2.zero,
                Vector2.up, Vector2.right, Vector2.zero,
                Vector2.up, Vector2.right, Vector2.zero
            }
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static AudioClip CreateScrapeClip()
    {
        // A deliberately dry, low-bandwidth shovel scrape, not the old snowball impact.
        // Built once per pool member; deterministic noise does not consume gameplay Random.
        const int sampleRate = 22050;
        float[] samples = new float[Mathf.CeilToInt(sampleRate * ScrapeSeconds)];
        System.Random noise = new System.Random(7143);
        float low = 0f;
        float body = 0f;
        float grain = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float seconds = i / (float)sampleRate;
            float t = i / (float)(samples.Length - 1);
            float white = (float)noise.NextDouble() * 2f - 1f;
            low += 0.035f * (white - low);
            body += 0.42f * (white - body);
            if (i % 130 == 0) { grain = (float)noise.NextDouble(); }
            float envelope = Mathf.SmoothStep(0f, 1f, t / 0.045f)
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 1f, t)));
            float rasp = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 87f * seconds);
            samples[i] = Mathf.Clamp((body - low) * (0.65f + grain * 0.35f) * rasp
                * envelope * 0.8f, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("Shovel scrape (runtime)", samples.Length, 1, sampleRate, false);
        clip.hideFlags = HideFlags.DontSave;
        clip.SetData(samples, 0);
        return clip;
    }
}
