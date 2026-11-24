using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Explicit, undoable authoring command; never runs automatically on import or in Play Mode.
/// All generated visuals use one flat-shaded mesh and one point-filtered texture per product.
/// </summary>
internal static class ShopGoodDisplayBuilder
{
    private const string Folder = "Assets/Game/Resources/ShopGoods";
    private const int TextureSize = 128;
    private const string MenuPath = "Snowpaw/Shop/Build Product Displays";
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int SnapRowsId = Shader.PropertyToID("_SnapRows");
    private static readonly Rect Front = new Rect(0, 0, 64, 64);
    private static readonly Rect Back = new Rect(64, 0, 64, 64);
    private static readonly Rect Side = new Rect(0, 64, 32, 32);
    private static readonly Rect Top = new Rect(32, 64, 32, 32);
    private static readonly Rect Dark = new Rect(64, 64, 32, 32);
    private static readonly Rect Accent = new Rect(96, 64, 32, 32);

    [MenuItem(MenuPath)]
    private static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Build product displays only in Edit Mode, after compilation.");

        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "GasStationInterior" || !scene.isLoaded)
            throw new InvalidOperationException("GasStationInterior must be the active loaded scene.");

        Shader shader = Shader.Find("Snowpaw/PSX Lit Vertex Snap");
        if (shader == null || !shader.isSupported)
            throw new InvalidOperationException("The project's PSX shader must be available and supported.");

        // Check every target and every destination before writing anything.
        var targets = new GameObject[ShopGoods.All.Length];
        var bottoms = new float[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            ShopGoods.Display display = ShopGoods.All[i];
            targets[i] = FindTarget(scene, display.ObjectName);
            GameObject target = targets[i];
            if (!target.TryGetComponent(out MeshFilter filter) ||
                !target.TryGetComponent(out MeshRenderer renderer) ||
                !target.TryGetComponent(out ShopGoodItem _))
                throw new InvalidOperationException(display.ObjectName + " needs its existing mesh, renderer and ShopGoodItem.");
            if (filter.sharedMesh == null || !renderer.enabled)
                throw new InvalidOperationException(display.ObjectName + " has no visible source mesh.");
            Vector3 shelfScale = target.transform.parent != null
                ? target.transform.parent.lossyScale : Vector3.one;
            if ((shelfScale - Vector3.one).sqrMagnitude > 0.0001f)
                throw new InvalidOperationException("Unexpected scaled shelf for " + display.ObjectName);
            bottoms[i] = renderer.bounds.min.y;
            CheckPath<Mesh>(AssetPath(display.GoodId, "Mesh", "asset"));
            CheckPath<Texture2D>(AssetPath(display.GoodId, "Texture", "asset"));
            CheckPath<Material>(AssetPath(display.GoodId, "Material", "mat"));
        }

        var generatedMeshes = new Mesh[targets.Length];
        var generatedTextures = new Texture2D[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            generatedMeshes[i] = BuildMesh(ShopGoods.All[i].GoodId);
            generatedTextures[i] = BuildTexture(ShopGoods.All[i].GoodId, i + 17);
        }

        EnsureFolder(Folder);
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Snowpaw: build six product displays");
        for (int i = 0; i < targets.Length; i++)
        {
            ShopGoods.Display display = ShopGoods.All[i];
            Mesh mesh = SaveGenerated(generatedMeshes[i], AssetPath(display.GoodId, "Mesh", "asset"));
            Texture2D texture = SaveGenerated(generatedTextures[i], AssetPath(display.GoodId, "Texture", "asset"));
            var material = new Material(shader) { name = "ShopGood_" + display.GoodId + "_Material" };
            material.SetTexture(BaseMapId, texture);
            material.SetColor(BaseColorId, Color.white);
            material.SetFloat(SmoothnessId, 0.12f);
            material.SetFloat(SnapRowsId, 400f);
            material = SaveGenerated(material, AssetPath(display.GoodId, "Material", "mat"));

            GameObject target = targets[i];
            target.TryGetComponent(out MeshFilter filter);
            target.TryGetComponent(out MeshRenderer renderer);
            Undo.RecordObjects(new Object[] { filter, renderer, target.transform }, "Update " + display.ObjectName);
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            Vector3 position = target.transform.position;
            // Keep the shelf contact plane and X/Z placement, including the taller cylinder convention.
            position.y = bottoms[i] - mesh.bounds.min.y * display.Size.y;
            target.transform.SetPositionAndRotation(position, Quaternion.identity);
            target.transform.localScale = display.Size;
            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        // RQL saves the changed scene after this explicit menu command returns.
        Debug.Log("[Snowpaw Station] Built six textured product displays: " + Folder + ". Existing ShopGoodItem roots and interaction components preserved.");
    }

    [MenuItem("Snowpaw/Shop/Validate Product Displays")]
    private static void ValidateDisplays()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "GasStationInterior")
            throw new InvalidOperationException("Validate displays in GasStationInterior Edit Mode.");
        if (Resources.GetBuiltinResource<Mesh>("Cube.fbx") == null ||
            Resources.GetBuiltinResource<Mesh>("Cylinder.fbx") == null)
            throw new InvalidOperationException("Runtime primitive lookup must resolve Unity's Cube and Cylinder meshes.");
        foreach (ShopGoods.Display display in ShopGoods.All)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetPath(display.GoodId, "Mesh", "asset"));
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath(display.GoodId, "Texture", "asset"));
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetPath(display.GoodId, "Material", "mat"));
            GameObject target = FindTarget(scene, display.ObjectName);
            target.TryGetComponent(out MeshFilter filter);
            target.TryGetComponent(out MeshRenderer renderer);
            float halfHeight = display.Shape == PrimitiveType.Cylinder ? 1f : 0.5f;
            if (mesh == null || mesh.vertexCount == 0 || mesh.subMeshCount != 1 ||
                mesh.normals.Length != mesh.vertexCount || mesh.uv.Length != mesh.vertexCount ||
                Mathf.Abs(mesh.bounds.min.y + halfHeight) > 0.001f ||
                Mathf.Abs(mesh.bounds.max.y - halfHeight) > 0.001f)
                throw new InvalidOperationException("Invalid generated mesh: " + display.GoodId);
            ValidateUvOrientation(mesh);
            if (SystemInfo.supportsAsyncGPUReadback && CountUploadedUvMismatches(mesh) != 0)
                throw new InvalidOperationException("CPU/GPU UV mismatch: " + display.GoodId);
            if (texture == null || texture.width != TextureSize || texture.height != TextureSize ||
                texture.filterMode != FilterMode.Point || texture.mipmapCount != 1)
                throw new InvalidOperationException("Invalid pixel texture: " + display.GoodId);
            if (material == null || material.shader == null || !material.shader.isSupported ||
                material.shader.name != "Snowpaw/PSX Lit Vertex Snap" || material.GetTexture(BaseMapId) != texture ||
                material.GetColor(BaseColorId) != Color.white)
                throw new InvalidOperationException("Invalid textured material: " + display.GoodId);
            if (filter == null || renderer == null || filter.sharedMesh != mesh ||
                renderer.sharedMaterial != material || !renderer.enabled ||
                (target.transform.localScale - display.Size).sqrMagnitude > 0.000001f ||
                !ShopGoodVisuals.CanManage(target, display.GoodId))
                throw new InvalidOperationException("Scene/resource or runtime management mismatch: " + display.GoodId);
        }
        Debug.Log("[Snowpaw Station] Product validation passed: 6 meshes with non-mirrored UVs, 6 point textures, 6 PSX materials, preserved roots and runtime resource recognition.");
    }

    [MenuItem("Snowpaw/Shop/Inspect Uploaded UVs")]
    private static void InspectUploadedUvs()
    {
        if (!SystemInfo.supportsAsyncGPUReadback)
        {
            Debug.LogWarning("[Snowpaw Station] GPU UV inspection unavailable: async GPU readback is not supported.");
            return;
        }
        foreach (ShopGoods.Display display in ShopGoods.All)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetPath(display.GoodId, "Mesh", "asset"));
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetPath(display.GoodId, "Material", "mat"));
            if (mesh == null || material == null)
                throw new InvalidOperationException("Missing visual resources for " + display.GoodId);
            int mismatches = CountUploadedUvMismatches(mesh);
            Debug.Log("[Snowpaw Station] GPU UV inspection " + display.GoodId + ": " +
                mismatches + "/" + mesh.vertexCount + " mismatches; texture scale=" +
                material.GetTextureScale(BaseMapId) + ", offset=" + material.GetTextureOffset(BaseMapId));
        }
    }

    private static int CountUploadedUvMismatches(Mesh mesh)
    {
        const VertexAttribute attribute = VertexAttribute.TexCoord0;
        if (mesh.GetVertexAttributeFormat(attribute) != VertexAttributeFormat.Float32 ||
            mesh.GetVertexAttributeDimension(attribute) != 2)
            throw new InvalidOperationException("GPU UV inspection requires Float32 Vector2 UVs: " + mesh.name);
        int stream = mesh.GetVertexAttributeStream(attribute);
        int strideBytes = mesh.GetVertexBufferStride(stream);
        int offsetBytes = mesh.GetVertexAttributeOffset(attribute);
        if (strideBytes % sizeof(float) != 0 || offsetBytes % sizeof(float) != 0)
            throw new InvalidOperationException("Unexpected GPU vertex layout: " + mesh.name);
        int stride = strideBytes / sizeof(float), offset = offsetBytes / sizeof(float);
        Vector2[] cpu = mesh.uv;
        using (GraphicsBuffer buffer = mesh.GetVertexBuffer(stream))
        {
            var request = AsyncGPUReadback.Request(buffer);
            request.WaitForCompletion();
            if (request.hasError)
                throw new InvalidOperationException("GPU UV readback failed: " + mesh.name);
            var data = request.GetData<float>();
            int mismatches = 0;
            for (int i = 0; i < cpu.Length; i++)
            {
                int index = i * stride + offset;
                if (index + 1 >= data.Length)
                    throw new InvalidOperationException("Incomplete GPU vertex buffer: " + mesh.name);
                if (Mathf.Abs(data[index] - cpu[i].x) > 0.000001f ||
                    Mathf.Abs(data[index + 1] - cpu[i].y) > 0.000001f)
                    mismatches++;
            }
            return mismatches;
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool CanBuild()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode &&
               !EditorApplication.isCompiling && SceneManager.GetActiveScene().name == "GasStationInterior";
    }

    private static string AssetPath(string id, string kind, string extension)
    {
        return Folder + "/" + id + "_" + kind + "." + extension;
    }

    private static GameObject FindTarget(Scene scene, string name)
    {
        GameObject result = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name != name) continue;
                if (result != null) throw new InvalidOperationException("Duplicate product root: " + name);
                result = transform.gameObject;
            }
        }
        if (result == null) throw new InvalidOperationException("Missing product root: " + name);
        return result;
    }

    private static void CheckPath<T>(string path) where T : Object
    {
        Object existing = AssetDatabase.LoadMainAssetAtPath(path);
        if ((existing != null && !(existing is T)) ||
            (existing == null && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path))))
            throw new InvalidOperationException("Refusing to overwrite an unrelated asset: " + path);
        string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
        if (existing != null && !existing.name.StartsWith("ShopGood_", StringComparison.Ordinal) &&
            existing.name != fileName)
            throw new InvalidOperationException("Refusing to overwrite an unowned asset: " + path);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }

    private static T SaveGenerated<T>(T generated, string path) where T : Object
    {
        generated.name = System.IO.Path.GetFileNameWithoutExtension(path);
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            Undo.RegisterCreatedObjectUndo(generated, "Create " + generated.name);
            return generated;
        }
        Undo.RecordObject(existing, "Rebuild " + existing.name);
        EditorUtility.CopySerialized(generated, existing);
        // Native rendering resources must also be updated through their public APIs.
        if (existing is Mesh mesh && generated is Mesh sourceMesh)
        {
            mesh.SetUVs(0, sourceMesh.uv);
            mesh.RecalculateTangents();
            mesh.UploadMeshData(false);
        }
        else if (existing is Texture2D texture && generated is Texture2D sourceTexture)
        {
            texture.SetPixels32(sourceTexture.GetPixels32());
            texture.Apply(false, false);
        }
        else if (existing is Material material && generated is Material sourceMaterial)
        {
            material.CopyPropertiesFromMaterial(sourceMaterial);
        }
        Object.DestroyImmediate(generated); // Only our newly created temporary object.
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static Mesh BuildMesh(string id)
    {
        var builder = new ProductMesh();
        switch (id)
        {
            case "coffee":
                builder.Frustum(Vector3.zero, 0.34f, 0.47f, -1f, 0.78f, Front, Side, 12);
                builder.Frustum(Vector3.zero, 0.50f, 0.50f, 0.78f, 0.92f, Dark, Top, 12);
                builder.Frustum(Vector3.zero, 0.44f, 0.44f, 0.92f, 1f, Dark, Top, 12);
                break;
            case "canister":
                builder.BevelBox(new Vector3(0, -0.135f, 0), new Vector3(1, 0.73f, 1), 0.10f, Front, Side, Top);
                builder.Box(new Vector3(0, 0.255f, 0), new Vector3(0.84f, 0.05f, 0.80f), Side, Side, Top);
                builder.Box(new Vector3(-0.12f, 0.375f, 0), new Vector3(0.10f, 0.20f, 0.22f), Dark, Dark, Dark);
                builder.Box(new Vector3(0.22f, 0.375f, 0), new Vector3(0.10f, 0.20f, 0.22f), Dark, Dark, Dark);
                builder.Box(new Vector3(0.05f, 0.465f, 0), new Vector3(0.44f, 0.07f, 0.22f), Dark, Dark, Dark);
                builder.Frustum(new Vector3(-0.34f, 0, 0), 0.10f, 0.10f, 0.28f, 0.37f, Dark, Dark, 8);
                break;
            case "heater":
                builder.Box(Vector3.zero, new Vector3(1, 1.60f, 0.90f), Front, Side, Top);
                builder.Box(new Vector3(-0.32f, -0.90f, 0), new Vector3(0.22f, 0.20f, 1), Dark, Dark, Dark);
                builder.Box(new Vector3(0.32f, -0.90f, 0), new Vector3(0.22f, 0.20f, 1), Dark, Dark, Dark);
                builder.Box(new Vector3(-0.22f, 0.88f, 0), new Vector3(0.10f, 0.16f, 0.18f), Dark, Dark, Dark);
                builder.Box(new Vector3(0.22f, 0.88f, 0), new Vector3(0.10f, 0.16f, 0.18f), Dark, Dark, Dark);
                builder.Box(new Vector3(0, 0.96f, 0), new Vector3(0.54f, 0.08f, 0.18f), Dark, Dark, Dark);
                builder.Box(new Vector3(0.32f, 0.60f, 0.47f), new Vector3(0.14f, 0.16f, 0.06f), Accent, Dark, Dark);
                break;
            case "cookies":
                builder.WrappedPack();
                break;
            case "tea":
                builder.Box(Vector3.zero, Vector3.one, Front, Side, Top);
                break;
            case "cigarettes":
                builder.BevelBox(Vector3.zero, Vector3.one, 0.045f, Front, Side, Top);
                break;
            default:
                throw new ArgumentException("Unknown product: " + id);
        }
        Mesh mesh = builder.Finish("ShopGood_" + id + "_Mesh");
        float halfHeight = id == "coffee" || id == "heater" ? 1f : 0.5f;
        if (Mathf.Abs(mesh.bounds.min.y + halfHeight) > 0.001f ||
            Mathf.Abs(mesh.bounds.max.y - halfHeight) > 0.001f || mesh.vertexCount == 0)
            throw new InvalidOperationException("Invalid shelf bounds for " + id);
        ValidateUvOrientation(mesh);
        return mesh;
    }

    private static void ValidateUvOrientation(Mesh mesh)
    {
        Vector2[] coordinates = mesh.uv;
        int[] indices = mesh.triangles;
        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector2 first = coordinates[indices[i + 1]] - coordinates[indices[i]];
            Vector2 second = coordinates[indices[i + 2]] - coordinates[indices[i]];
            float determinant = first.x * second.y - first.y * second.x;
            // Geometry faces outward; clockwise UV triangles keep text readable from outside.
            if (determinant >= -0.000001f)
                throw new InvalidOperationException("Mirrored or degenerate UV triangle in " + mesh.name + ": " + i / 3);
        }
    }

    private static Texture2D BuildTexture(string id, int seed)
    {
        Color32 paper = C(229, 219, 190), ink = C(37, 42, 40);
        Color32 body;
        switch (id)
        {
            case "coffee": body = C(120, 87, 59); break;
            case "tea": body = C(46, 83, 59); break;
            case "cookies": body = C(198, 142, 54); break;
            case "cigarettes": body = C(218, 218, 204); break;
            case "canister": body = C(153, 43, 34); break;
            case "heater": body = C(173, 102, 43); break;
            default: throw new ArgumentException(id);
        }
        var paint = new PixelPaint(body, seed);
        paint.Noise(Front, body, 8);
        paint.Noise(Back, body, 8);
        paint.Noise(Side, body, 6);
        paint.Noise(Top, body, 5);
        paint.Fill(Dark, ink);
        paint.Fill(Accent, C(227, 166, 65));
        paint.Frame(3, 3, 58, 58, paper);
        switch (id)
        {
            case "coffee":
                paint.Fill(new Rect(1, 12, 62, 41), paper);
                paint.Text("КОФЕ", 20, 17, ink);
                paint.Disc(32, 35, 8, body);
                paint.Line(29, 40, 35, 30, paper);
                paint.Line(23, 7, 25, 3, paper);
                paint.Line(32, 8, 34, 3, paper);
                paint.Text("SNOWPAW", 11, 45, ink);
                paint.Fill(Top, ink);
                break;
            case "tea":
                paint.Fill(new Rect(8, 12, 48, 41), paper);
                paint.Text("ЧАЙ", 24, 17, body);
                paint.Disc(29, 35, 8, body);
                paint.Disc(36, 31, 5, C(86, 115, 64));
                paint.Line(25, 41, 39, 26, paper);
                paint.Text("ЛИСТОВОЙ", 8, 56, paper);
                paint.Text("100", 22, 44, body);
                break;
            case "cookies":
                paint.Fill(new Rect(7, 9, 50, 12), ink);
                paint.Text("ПЕЧЕНЬЕ", 11, 12, paper);
                paint.Disc(31, 38, 13, C(233, 183, 103));
                paint.Disc(27, 32, 2, ink);
                paint.Disc(36, 39, 2, ink);
                paint.Disc(25, 42, 2, ink);
                paint.Disc(36, 30, 2, ink);
                paint.Disc(32, 45, 2, ink);
                paint.Text("200", 23, 55, paper);
                for (int x = 1; x < 7; x += 2) paint.Line(x, 0, x, 63, ink);
                for (int x = 57; x < 64; x += 2) paint.Line(x, 0, x, 63, ink);
                break;
            case "cigarettes":
                paint.Fill(new Rect(1, 1, 62, 20), C(61, 79, 88));
                paint.Text("СИГАРЕТЫ", 8, 7, paper);
                paint.Line(2, 24, 61, 24, ink);
                paint.Fill(new Rect(10, 31, 44, 7), C(179, 146, 86));
                paint.Text("20", 26, 41, ink);
                paint.Fill(new Rect(3, 51, 58, 10), ink);
                paint.Text("18+", 23, 53, paper);
                paint.Line(65, 46, 125, 46, ink);
                for (int y = 50; y < 61; y += 3) paint.Line(70, y, 119, y, ink);
                break;
            case "canister":
                paint.Line(11, 15, 51, 49, C(114, 36, 29));
                paint.Line(51, 15, 11, 49, C(114, 36, 29));
                paint.Fill(new Rect(8, 15, 48, 11), paper);
                paint.Text("ТОПЛИВО", 11, 17, ink);
                paint.Text("95", 20, 32, paper, 2);
                paint.Line(7, 58, 19, 58, paper);
                paint.Line(42, 5, 54, 5, paper);
                paint.Disc(112, 32, 12, paper);
                paint.Text("!", 110, 29, ink);
                break;
            case "heater":
                paint.Fill(new Rect(7, 19, 50, 35), ink);
                paint.Text("ОБОГРЕВ", 11, 7, paper);
                for (int x = 11; x < 54; x += 6)
                {
                    paint.Line(x, 22, x, 50, C(214, 112, 45));
                    paint.Line(x + 1, 22, x + 1, 50, C(234, 170, 64));
                }
                for (int y = 24; y < 53; y += 7) paint.Line(8, y, 56, y, C(86, 82, 74));
                paint.Text("1500", 19, 56, paper);
                for (int y = 68; y < 95; y += 5) paint.Line(4, y, 27, y, ink);
                paint.Fill(Top, C(82, 81, 72));
                break;
        }
        // Back label, common batch lines and barcode. All texture tiles have distinct UV islands.
        paint.Fill(new Rect(75, 10, 42, 11), paper);
        paint.Text("SNOWPAW", 75, 12, ink);
        for (int y = 28; y < 41; y += 4) paint.Line(76, y, 114, y, paper);
        paint.Fill(new Rect(80, 48, 30, 12), paper);
        for (int x = 82; x < 109; x += 3) paint.Line(x, 49, x, 57, ink);
        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "ShopGood_" + id + "_Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0
        };
        texture.SetPixels32(paint.Pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Color32 C(byte r, byte g, byte b) { return new Color32(r, g, b, 255); }

    private sealed class PixelPaint
    {
        public readonly Color32[] Pixels = new Color32[TextureSize * TextureSize];
        private readonly System.Random random;
        public PixelPaint(Color32 color, int seed)
        {
            random = new System.Random(seed);
            for (int i = 0; i < Pixels.Length; i++) Pixels[i] = color;
        }
        private void Pixel(int x, int y, Color32 color)
        {
            if (x >= 0 && x < TextureSize && y >= 0 && y < TextureSize)
                Pixels[(TextureSize - 1 - y) * TextureSize + x] = color;
        }
        public void Fill(Rect rect, Color32 color)
        {
            for (int y = (int)rect.yMin; y < rect.yMax; y++)
                for (int x = (int)rect.xMin; x < rect.xMax; x++) Pixel(x, y, color);
        }
        public void Noise(Rect rect, Color32 color, int range)
        {
            for (int y = (int)rect.yMin; y < rect.yMax; y++)
                for (int x = (int)rect.xMin; x < rect.xMax; x++)
                {
                    int variation = random.Next(-range, range + 1);
                    Pixel(x, y, C((byte)Mathf.Clamp(color.r + variation, 0, 255),
                        (byte)Mathf.Clamp(color.g + variation, 0, 255), (byte)Mathf.Clamp(color.b + variation, 0, 255)));
                }
        }
        public void Frame(int x, int y, int width, int height, Color32 color)
        {
            Line(x, y, x + width - 1, y, color);
            Line(x, y + height - 1, x + width - 1, y + height - 1, color);
            Line(x, y, x, y + height - 1, color);
            Line(x + width - 1, y, x + width - 1, y + height - 1, color);
        }
        public void Line(int x0, int y0, int x1, int y1, Color32 color)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : (float)i / steps;
                Pixel(Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), color);
            }
        }
        public void Disc(int x, int y, int radius, Color32 color)
        {
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                    if (dx * dx + dy * dy <= radius * radius) Pixel(x + dx, y + dy, color);
        }
        public void Text(string text, int x, int y, Color32 color, int scale = 1)
        {
            foreach (char character in text)
            {
                if (Glyphs.TryGetValue(character, out string glyph))
                    for (int row = 0; row < 7; row++)
                        for (int column = 0; column < 5; column++)
                            if (glyph[row * 5 + column] == '1')
                                Fill(new Rect(x + column * scale, y + row * scale, scale, scale), color);
                x += 6 * scale;
            }
        }
    }

    private static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
    {
        ['А'] = "01110"+"10001"+"10001"+"11111"+"10001"+"10001"+"10001",
        ['Б'] = "11111"+"10000"+"10000"+"11110"+"10001"+"10001"+"11110",
        ['В'] = "11110"+"10001"+"10001"+"11110"+"10001"+"10001"+"11110",
        ['Г'] = "11111"+"10000"+"10000"+"10000"+"10000"+"10000"+"10000",
        ['Е'] = "11111"+"10000"+"10000"+"11110"+"10000"+"10000"+"11111",
        ['И'] = "10001"+"10001"+"10011"+"10101"+"11001"+"10001"+"10001",
        ['Й'] = "01010"+"00100"+"10001"+"10011"+"10101"+"11001"+"10001",
        ['К'] = "10001"+"10010"+"10100"+"11000"+"10100"+"10010"+"10001",
        ['Л'] = "00111"+"01001"+"01001"+"01001"+"01001"+"10001"+"10001",
        ['Н'] = "10001"+"10001"+"10001"+"11111"+"10001"+"10001"+"10001",
        ['О'] = "01110"+"10001"+"10001"+"10001"+"10001"+"10001"+"01110",
        ['П'] = "11111"+"10001"+"10001"+"10001"+"10001"+"10001"+"10001",
        ['Р'] = "11110"+"10001"+"10001"+"11110"+"10000"+"10000"+"10000",
        ['С'] = "01111"+"10000"+"10000"+"10000"+"10000"+"10000"+"01111",
        ['Т'] = "11111"+"00100"+"00100"+"00100"+"00100"+"00100"+"00100",
        ['Ф'] = "00100"+"01110"+"10101"+"10101"+"10101"+"01110"+"00100",
        ['Ч'] = "10001"+"10001"+"10001"+"01111"+"00001"+"00001"+"00001",
        ['Ы'] = "10001"+"10001"+"10001"+"11001"+"10101"+"10101"+"11001",
        ['Ь'] = "10000"+"10000"+"10000"+"11110"+"10001"+"10001"+"11110",
        ['S'] = "01111"+"10000"+"10000"+"01110"+"00001"+"00001"+"11110",
        ['N'] = "10001"+"11001"+"10101"+"10011"+"10001"+"10001"+"10001",
        ['O'] = "01110"+"10001"+"10001"+"10001"+"10001"+"10001"+"01110",
        ['W'] = "10001"+"10001"+"10001"+"10101"+"10101"+"11011"+"10001",
        ['P'] = "11110"+"10001"+"10001"+"11110"+"10000"+"10000"+"10000",
        ['A'] = "01110"+"10001"+"10001"+"11111"+"10001"+"10001"+"10001",
        ['0'] = "01110"+"10001"+"10011"+"10101"+"11001"+"10001"+"01110",
        ['1'] = "00100"+"01100"+"00100"+"00100"+"00100"+"00100"+"01110",
        ['2'] = "01110"+"10001"+"00001"+"00010"+"00100"+"01000"+"11111",
        ['5'] = "11111"+"10000"+"11110"+"00001"+"00001"+"10001"+"01110",
        ['8'] = "01110"+"10001"+"10001"+"01110"+"10001"+"10001"+"01110",
        ['9'] = "01110"+"10001"+"10001"+"01111"+"00001"+"00001"+"01110",
        ['+'] = "00000"+"00100"+"00100"+"11111"+"00100"+"00100"+"00000",
        ['!'] = "00100"+"00100"+"00100"+"00100"+"00100"+"00000"+"00100"
    };

    private sealed class ProductMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>(512);
        private readonly List<Vector2> uvs = new List<Vector2>(512);
        private readonly List<int> triangles = new List<int>(768);
        private static Vector2 UV(Rect tile, float u, float v)
        {
            // The outward-facing vertex order runs right-to-left when viewed from outside.
            // Reverse U inside this tile, not across the entire atlas; keep V upright.
            // Half-pixel inset prevents neighbouring atlas tiles from bleeding at their borders.
            return new Vector2((tile.x + 0.5f + (1f - u) * (tile.width - 1f)) / TextureSize,
                (TextureSize - tile.y - tile.height + 0.5f + v * (tile.height - 1f)) / TextureSize);
        }
        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Rect tile, float left = 0f, float right = 1f)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            uvs.Add(UV(tile, left, 0)); uvs.Add(UV(tile, right, 0));
            uvs.Add(UV(tile, right, 1)); uvs.Add(UV(tile, left, 1));
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Rect tile)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            uvs.Add(UV(tile, 0.5f, 0.5f)); uvs.Add(UV(tile, 0.15f, 0.15f)); uvs.Add(UV(tile, 0.85f, 0.15f));
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }
        public void Box(Vector3 center, Vector3 size, Rect front, Rect side, Rect top)
        {
            Vector3 min = center - size * 0.5f, max = center + size * 0.5f;
            Quad(new Vector3(min.x,min.y,max.z), new Vector3(max.x,min.y,max.z), new Vector3(max.x,max.y,max.z), new Vector3(min.x,max.y,max.z), front);
            Quad(new Vector3(max.x,min.y,min.z), new Vector3(min.x,min.y,min.z), new Vector3(min.x,max.y,min.z), new Vector3(max.x,max.y,min.z), front == Front ? Back : side);
            Quad(new Vector3(max.x,min.y,max.z), new Vector3(max.x,min.y,min.z), new Vector3(max.x,max.y,min.z), new Vector3(max.x,max.y,max.z), side);
            Quad(new Vector3(min.x,min.y,min.z), new Vector3(min.x,min.y,max.z), new Vector3(min.x,max.y,max.z), new Vector3(min.x,max.y,min.z), side);
            Quad(new Vector3(min.x,max.y,max.z), new Vector3(max.x,max.y,max.z), new Vector3(max.x,max.y,min.z), new Vector3(min.x,max.y,min.z), top);
            Quad(new Vector3(min.x,min.y,min.z), new Vector3(max.x,min.y,min.z), new Vector3(max.x,min.y,max.z), new Vector3(min.x,min.y,max.z), side);
        }
        public void Frustum(Vector3 offset, float bottomRadius, float topRadius, float bottomY, float topY, Rect sides, Rect cap, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = -Mathf.PI + 2f * Mathf.PI * i / segments;
                float b = -Mathf.PI + 2f * Mathf.PI * (i + 1) / segments;
                Vector3 bottomA = offset + new Vector3(Mathf.Sin(a)*bottomRadius, bottomY, Mathf.Cos(a)*bottomRadius);
                Vector3 bottomB = offset + new Vector3(Mathf.Sin(b)*bottomRadius, bottomY, Mathf.Cos(b)*bottomRadius);
                Vector3 topA = offset + new Vector3(Mathf.Sin(a)*topRadius, topY, Mathf.Cos(a)*topRadius);
                Vector3 topB = offset + new Vector3(Mathf.Sin(b)*topRadius, topY, Mathf.Cos(b)*topRadius);
                Quad(bottomA, bottomB, topB, topA, sides, (float)i/segments, (float)(i+1)/segments);
                Triangle(offset + new Vector3(0,topY,0), topA, topB, cap);
                Triangle(offset + new Vector3(0,bottomY,0), bottomB, bottomA, cap);
            }
        }
        public void BevelBox(Vector3 center, Vector3 size, float bevel, Rect front, Rect side, Rect top)
        {
            float x = size.x * 0.5f, z = size.z * 0.5f;
            var ring = new[] { new Vector2(-x+bevel,z), new Vector2(x-bevel,z), new Vector2(x,z-bevel), new Vector2(x,-z+bevel),
                new Vector2(x-bevel,-z), new Vector2(-x+bevel,-z), new Vector2(-x,-z+bevel), new Vector2(-x,z-bevel) };
            for (int i = 0; i < ring.Length; i++)
            {
                Vector2 a = ring[i], b = ring[(i+1)%ring.Length];
                Vector3 bottomA = center + new Vector3(a.x,-size.y*0.5f,a.y);
                Vector3 bottomB = center + new Vector3(b.x,-size.y*0.5f,b.y);
                Vector3 topA = center + new Vector3(a.x,size.y*0.5f,a.y);
                Vector3 topB = center + new Vector3(b.x,size.y*0.5f,b.y);
                Quad(bottomA,bottomB,topB,topA, i == 0 ? front : (i == 4 ? Back : side));
                Triangle(center + new Vector3(0,size.y*0.5f,0),topA,topB,top);
                Triangle(center + new Vector3(0,-size.y*0.5f,0),bottomB,bottomA,side);
            }
        }
        public void WrappedPack()
        {
            float[] xs = { -0.5f, -0.40f, 0.40f, 0.5f };
            float[] heights = { 0.32f, 0.5f, 0.5f, 0.32f };
            float[] depths = { 0.42f, 0.5f, 0.5f, 0.42f };
            for (int i = 0; i < 3; i++)
            {
                float a=xs[i], b=xs[i+1], ha=heights[i], hb=heights[i+1], da=depths[i], db=depths[i+1];
                Quad(new Vector3(a,-ha,da),new Vector3(b,-hb,db),new Vector3(b,hb,db),new Vector3(a,ha,da),Front,a+0.5f,b+0.5f);
                Quad(new Vector3(b,-hb,-db),new Vector3(a,-ha,-da),new Vector3(a,ha,-da),new Vector3(b,hb,-db),Back,0.5f-b,0.5f-a);
                Quad(new Vector3(a,ha,da),new Vector3(b,hb,db),new Vector3(b,hb,-db),new Vector3(a,ha,-da),Top);
                Quad(new Vector3(a,-ha,-da),new Vector3(b,-hb,-db),new Vector3(b,-hb,db),new Vector3(a,-ha,da),Side);
            }
            Quad(new Vector3(0.5f,-0.32f,0.42f),new Vector3(0.5f,-0.32f,-0.42f),new Vector3(0.5f,0.32f,-0.42f),new Vector3(0.5f,0.32f,0.42f),Side);
            Quad(new Vector3(-0.5f,-0.32f,-0.42f),new Vector3(-0.5f,-0.32f,0.42f),new Vector3(-0.5f,0.32f,0.42f),new Vector3(-0.5f,0.32f,-0.42f),Side);
        }
        public Mesh Finish(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
