#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SnowpawStation.Editor.World
{
    /// <summary>
    /// Creates cohesive low-poly winter tree (Snowy Pine, Snowy Spruce), faceted frost rock,
    /// and directional trail marker prefabs with 3-level opaque LODGroups and URP Lit materials
    /// matching SnowpawStation's art direction.
    ///
    /// Winding convention: Unity left-handed clockwise outward-facing triangles.
    /// Base embed: trunks, rocks, and posts extend -PropBaseEmbedY below Y=0 so pivots never
    /// float on slopes up to 22 deg.
    /// </summary>
    public static class WinterFoliageFactory
    {
        public struct FoliageAssets
        {
            public GameObject PinePrefab;
            public GameObject SprucePrefab;
            public GameObject FrostRockPrefab;
            public GameObject TrailMarkerPrefab;
        }

        public static FoliageAssets EnsureFoliagePrefabs()
        {
            EnsureFolder(WinterLandscapeSpec.MaterialsFolder);
            EnsureFolder(WinterLandscapeSpec.MeshesFolder);
            EnsureFolder(WinterLandscapeSpec.PrefabsFolder);

            Material barkMat = CreateOrLoadMaterial(
                WinterLandscapeSpec.MaterialsFolder + "/Mat_WinterBark.mat",
                new Color(0.32f, 0.25f, 0.21f, 1f),
                WinterLandscapeSpec.TexPineBarkDiff,
                WinterLandscapeSpec.TexPineBarkNorm,
                smoothness: 0.10f);

            Material foliageMat = CreateOrLoadMaterial(
                WinterLandscapeSpec.MaterialsFolder + "/Mat_WinterEvergreen.mat",
                new Color(0.15f, 0.24f, 0.21f, 1f),
                null,
                null,
                smoothness: 0.08f);

            Material snowCapMat = CreateOrLoadMaterial(
                WinterLandscapeSpec.MaterialsFolder + "/Mat_WinterSnowCap.mat",
                WinterLandscapeSpec.StationSnowBaseColor,
                WinterLandscapeSpec.TexSnowPrimaryDiff,
                WinterLandscapeSpec.TexSnowPrimaryNorm,
                smoothness: WinterLandscapeSpec.StationSnowSmoothness);

            Material rockMat = CreateOrLoadMaterial(
                WinterLandscapeSpec.MaterialsFolder + "/Mat_WinterFrostRock.mat",
                new Color(0.40f, 0.44f, 0.50f, 1f),
                WinterLandscapeSpec.TexSnowSecondaryDiff,
                WinterLandscapeSpec.TexSnowSecondaryNorm,
                smoothness: 0.14f);

            return new FoliageAssets
            {
                PinePrefab = BuildTreePrefab("WinterPine_LOD", 7.8f, 2.35f, 4, 17, barkMat, foliageMat, snowCapMat),
                SprucePrefab = BuildTreePrefab("WinterSpruce_LOD", 9.2f, 2.05f, 5, 31, barkMat, foliageMat, snowCapMat),
                FrostRockPrefab = BuildRockPrefab("WinterFrostRock_LOD", rockMat, snowCapMat),
                TrailMarkerPrefab = BuildTrailMarkerPrefab("WinterTrailMarker", barkMat, snowCapMat)
            };
        }

        private static GameObject BuildTreePrefab(
            string name, float height, float baseRadius, int tiers, int shapeSeed,
            Material barkMat, Material foliageMat, Material snowCapMat)
        {
            string prefabPath = WinterLandscapeSpec.PrefabsFolder + "/" + name + ".prefab";
            var root = new GameObject(name);

            // LOD0: full asymmetric drooping tiers + snow caps (10-sided)
            GameObject lod0 = BuildTreeLodObject("LOD0", height, baseRadius, tiers, 10, true, shapeSeed, barkMat, foliageMat, snowCapMat, name);
            lod0.transform.SetParent(root.transform, false);

            // LOD1: simplified tiers (6-sided)
            GameObject lod1 = BuildTreeLodObject("LOD1", height, baseRadius, Mathf.Max(2, tiers - 1), 6, true, shapeSeed, barkMat, foliageMat, snowCapMat, name);
            lod1.transform.SetParent(root.transform, false);

            // LOD2: lightweight opaque silhouette (4-sided)
            GameObject lod2 = BuildTreeLodObject("LOD2", height, baseRadius, 2, 4, false, shapeSeed, barkMat, foliageMat, snowCapMat, name);
            lod2.transform.SetParent(root.transform, false);

            var lodGroup = root.AddComponent<LODGroup>();
            lodGroup.SetLODs(new[]
            {
                new LOD(0.32f, lod0.GetComponentsInChildren<Renderer>()),
                new LOD(0.12f, lod1.GetComponentsInChildren<Renderer>()),
                new LOD(0.035f, lod2.GetComponentsInChildren<Renderer>())
            });
            lodGroup.RecalculateBounds();

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, height * 0.35f, 0f);
            col.radius = 0.28f;
            col.height = height * 0.7f;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildTreeLodObject(
            string lodName, float height, float baseRadius, int tiers, int sides, bool includeSnowCaps,
            int shapeSeed, Material barkMat, Material foliageMat, Material snowCapMat, string assetPrefix)
        {
            var lodObj = new GameObject(lodName);
            float embed = WinterLandscapeSpec.PropBaseEmbedY;

            // Tapered trunk extending -embed below Y=0 so it never floats on slopes
            float trunkH = height * 0.36f + embed;
            Mesh trunkMesh = SaveMesh(
                CreateFrustumMesh(assetPrefix + "_" + lodName + "_Trunk", 0.27f, 0.13f, trunkH, Mathf.Max(5, sides), -embed, 0f),
                WinterLandscapeSpec.MeshesFolder + "/" + assetPrefix + "_" + lodName + "_Trunk.asset");
            CreateMeshChild("Trunk", lodObj.transform, trunkMesh, barkMat, Vector3.zero, Quaternion.identity);

            // Multi-tiered, slightly asymmetric drooping evergreen boughs + snow-laden tops
            float crownStart = height * 0.21f;
            float crownSpan = height - crownStart;
            for (int t = 0; t < tiers; t++)
            {
                float frac = t / (float)tiers;
                float y = crownStart + frac * crownSpan * 0.78f;
                float tierRadius = baseRadius * Mathf.Lerp(1.04f, 0.32f, frac);
                float tierHeight = crownSpan * Mathf.Lerp(0.42f, 0.27f, frac);

                // Subtle organic asymmetry per tier
                float yaw = (t * 47f + shapeSeed * 19f) % 360f;
                float offsetMag = (1f - frac) * 0.08f;
                Vector3 tierOffset = new Vector3(
                    Mathf.Cos(yaw * Mathf.Deg2Rad) * offsetMag,
                    y,
                    Mathf.Sin(yaw * Mathf.Deg2Rad) * offsetMag);
                Quaternion tierRot = Quaternion.Euler(0f, yaw, 0f);

                Mesh boughMesh = SaveMesh(
                    CreateFrustumMesh($"{assetPrefix}_{lodName}_Bough_{t}", tierRadius, 0.04f, tierHeight, sides, 0f, droop: 0.14f, closeBottom: true),
                    $"{WinterLandscapeSpec.MeshesFolder}/{assetPrefix}_{lodName}_Bough_{t}.asset");
                CreateMeshChild($"Bough_{t}", lodObj.transform, boughMesh, foliageMat, tierOffset, tierRot);

                if (includeSnowCaps)
                {
                    // At y = 0.52 * tierHeight the bough cone radius is ~0.48 * tierRadius;
                    // bottomRadius = 0.52 * tierRadius hugs the upper cone tightly without overhanging.
                    Mesh capMesh = SaveMesh(
                        CreateFrustumMesh($"{assetPrefix}_{lodName}_SnowCap_{t}", tierRadius * 0.52f, 0.03f, tierHeight * 0.49f, sides, 0f, droop: 0.04f, closeBottom: false),
                        $"{WinterLandscapeSpec.MeshesFolder}/{assetPrefix}_{lodName}_SnowCap_{t}.asset");
                    CreateMeshChild($"SnowCap_{t}", lodObj.transform, capMesh, snowCapMat,
                        tierOffset + new Vector3(0f, tierHeight * 0.52f, 0f), tierRot);
                }
            }

            return lodObj;
        }

        private static GameObject BuildRockPrefab(string name, Material rockMat, Material snowCapMat)
        {
            string prefabPath = WinterLandscapeSpec.PrefabsFolder + "/" + name + ".prefab";
            var root = new GameObject(name);

            Mesh rockLod0 = SaveMesh(CreateFacetedBoulderMesh(name + "_LOD0", 1.35f, 0.88f, 8, 5, 11),
                WinterLandscapeSpec.MeshesFolder + "/" + name + "_LOD0.asset");
            Mesh rockLod1 = SaveMesh(CreateFacetedBoulderMesh(name + "_LOD1", 1.35f, 0.88f, 5, 4, 11),
                WinterLandscapeSpec.MeshesFolder + "/" + name + "_LOD1.asset");

            var g0 = CreateMeshChild("LOD0", root.transform, rockLod0, rockMat, Vector3.zero, Quaternion.identity);
            // At Y = 0.42m the boulder cross-section radius is ~0.88m; bottomRadius = 0.76m embeds cleanly onto top facets.
            var capMesh = SaveMesh(CreateFrustumMesh(name + "_SnowCap", 0.76f, 0.22f, 0.22f, 7, 0f, 0.03f, closeBottom: false),
                WinterLandscapeSpec.MeshesFolder + "/" + name + "_SnowCap.asset");
            CreateMeshChild("SnowCap", g0.transform, capMesh, snowCapMat, new Vector3(0f, 0.42f, 0f), Quaternion.identity);

            var g1 = CreateMeshChild("LOD1", root.transform, rockLod1, rockMat, Vector3.zero, Quaternion.identity);

            var lodGroup = root.AddComponent<LODGroup>();
            lodGroup.SetLODs(new[]
            {
                new LOD(0.18f, g0.GetComponentsInChildren<Renderer>()),
                new LOD(0.04f, g1.GetComponentsInChildren<Renderer>())
            });
            lodGroup.RecalculateBounds();

            var col = root.AddComponent<SphereCollider>();
            col.center = new Vector3(0f, 0.38f, 0f);
            col.radius = 1.0f;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildTrailMarkerPrefab(string name, Material barkMat, Material snowCapMat)
        {
            string prefabPath = WinterLandscapeSpec.PrefabsFolder + "/" + name + ".prefab";
            var root = new GameObject(name);
            float embed = WinterLandscapeSpec.PropBaseEmbedY;

            Mesh postMesh = SaveMesh(
                CreateFrustumMesh(name + "_Post", 0.12f, 0.09f, 1.95f + embed, 6, -embed, 0f),
                WinterLandscapeSpec.MeshesFolder + "/" + name + "_Post.asset");
            Mesh capMesh = SaveMesh(
                CreateFrustumMesh(name + "_Cap", 0.17f, 0.04f, 0.16f, 6, 0f, 0f),
                WinterLandscapeSpec.MeshesFolder + "/" + name + "_Cap.asset");
            Mesh bladeMesh = SaveMesh(
                CreateDirectionalBladeMesh(name + "_SignBlade", 0.72f, 0.20f, 0.05f),
                WinterLandscapeSpec.MeshesFolder + "/" + name + "_SignBlade.asset");

            CreateMeshChild("Post", root.transform, postMesh, barkMat, Vector3.zero, Quaternion.identity);
            CreateMeshChild("SnowCap", root.transform, capMesh, snowCapMat, new Vector3(0f, 1.91f, 0f), Quaternion.identity);
            // Directional wooden blade pointing along +Z (oriented along trail tangent on placement)
            CreateMeshChild("SignBlade", root.transform, bladeMesh, barkMat, new Vector3(0f, 1.56f, 0.05f), Quaternion.identity);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject CreateMeshChild(
            string name, Transform parent, Mesh mesh, Material mat, Vector3 localPos, Quaternion localRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            return go;
        }

        /// <summary>
        /// Outward-facing CW frustum/cone mesh with periodic bottom rim droop (no seam tear)
        /// and optional closed downward-facing bottom cap for tree boughs.
        /// </summary>
        private static Mesh CreateFrustumMesh(
            string name, float bottomRadius, float topRadius, float height, int sides,
            float yBase, float droop, bool closeBottom = false)
        {
            var mesh = new Mesh { name = name };
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int i = 0; i <= sides; i++)
            {
                float a = (i / (float)sides) * Mathf.PI * 2f;
                float c = Mathf.Cos(a);
                float s = Mathf.Sin(a);
                // Periodic wave in angle `a` so i == 0 and i == sides match identically (zero seam tear)
                float rimWave = droop > 0f ? -droop * (0.7f + 0.3f * Mathf.Sin(a * 3f)) : 0f;
                verts.Add(new Vector3(c * bottomRadius, yBase + rimWave, s * bottomRadius));
                uvs.Add(new Vector2(i / (float)sides, 0f));
                verts.Add(new Vector3(c * topRadius, yBase + height, s * topRadius));
                uvs.Add(new Vector2(i / (float)sides, 1f));
            }

            for (int i = 0; i < sides; i++)
            {
                int i0 = i * 2;
                int i1 = i0 + 1;
                int i2 = i0 + 2;
                int i3 = i0 + 3;
                // Unity left-handed CW outward-facing winding
                tris.Add(i0); tris.Add(i1); tris.Add(i2);
                tris.Add(i2); tris.Add(i1); tris.Add(i3);
            }

            // Top cap (upward +Y normal)
            int topCenter = verts.Count;
            verts.Add(new Vector3(0f, yBase + height, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < sides; i++)
            {
                tris.Add(topCenter);
                tris.Add((i + 1) * 2 + 1);
                tris.Add(i * 2 + 1);
            }

            // Optional bottom cap (downward -Y normal so looking up under boughs is solid)
            if (closeBottom)
            {
                int bottomCenter = verts.Count;
                verts.Add(new Vector3(0f, yBase - droop * 0.7f, 0f));
                uvs.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i < sides; i++)
                {
                    tris.Add(bottomCenter);
                    tris.Add(i * 2);
                    tris.Add((i + 1) * 2);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Crisp faceted low-poly boulder mesh with verified outward CW winding in Unity's
        /// left-handed coordinate system and grounded base extending -PropBaseEmbedY below Y=0.
        /// </summary>
        private static Mesh CreateFacetedBoulderMesh(
            string name, float radiusXZ, float radiusY, int segsH, int segsV, int seed)
        {
            var grid = new Vector3[(segsV + 1) * (segsH + 1)];
            var gridUV = new Vector2[grid.Length];
            float embed = WinterLandscapeSpec.PropBaseEmbedY;

            for (int v = 0; v <= segsV; v++)
            {
                float phi = (v / (float)segsV) * Mathf.PI;
                for (int h = 0; h <= segsH; h++)
                {
                    float theta = (h / (float)segsH) * Mathf.PI * 2f;
                    float x = Mathf.Sin(phi) * Mathf.Cos(theta);
                    float y = Mathf.Cos(phi);
                    float z = Mathf.Sin(phi) * Mathf.Sin(theta);
                    float jitter = 0.80f + 0.36f * Mathf.PerlinNoise(x * 1.7f + seed, z * 1.7f + y * 1.3f + seed);
                    float py = (y * 0.5f + 0.40f) * radiusY * jitter - embed * 0.5f;
                    int idx = v * (segsH + 1) + h;
                    grid[idx] = new Vector3(x * radiusXZ * jitter, py, z * radiusXZ * jitter);
                    gridUV[idx] = new Vector2(h / (float)segsH, v / (float)segsV);
                }
            }

            // Build split-vertex faceted triangles for crisp PSX/low-poly rock planes
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int stride = segsH + 1;

            void AddTri(int a, int b, int c)
            {
                Vector3 va = grid[a];
                Vector3 vb = grid[b];
                Vector3 vc = grid[c];
                // Skip degenerate pole triangles
                if (Vector3.Cross(vb - va, vc - va).sqrMagnitude < 1e-8f) return;
                int baseIdx = verts.Count;
                verts.Add(va); verts.Add(vb); verts.Add(vc);
                uvs.Add(gridUV[a]); uvs.Add(gridUV[b]); uvs.Add(gridUV[c]);
                tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
            }

            for (int v = 0; v < segsV; v++)
            {
                for (int h = 0; h < segsH; h++)
                {
                    int i0 = v * stride + h;
                    int i1 = i0 + 1;
                    int i2 = i0 + stride;
                    int i3 = i2 + 1;
                    // Outward-facing CW winding in Unity's left-handed space: (i0, i1, i2) and (i1, i3, i2)
                    AddTri(i0, i1, i2);
                    AddTri(i1, i3, i2);
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Angled wooden directional sign blade pointing toward +Z (outward CW winding).
        /// </summary>
        private static Mesh CreateDirectionalBladeMesh(string name, float length, float height, float thickness)
        {
            float hx = thickness * 0.5f;
            float hy = height * 0.5f;
            float tipZ = length;
            float shoulderZ = length * 0.74f;

            // 5-vertex arrow profile extruded along X [-hx, +hx]
            Vector3[] left =
            {
                new Vector3(-hx, -hy, 0f),
                new Vector3(-hx,  hy, 0f),
                new Vector3(-hx,  hy, shoulderZ),
                new Vector3(-hx,  0f, tipZ),
                new Vector3(-hx, -hy, shoulderZ)
            };
            Vector3[] right =
            {
                new Vector3(hx, -hy, 0f),
                new Vector3(hx,  hy, 0f),
                new Vector3(hx,  hy, shoulderZ),
                new Vector3(hx,  0f, tipZ),
                new Vector3(hx, -hy, shoulderZ)
            };

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1));
                uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }

            void AddTri(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0.5f, 1)); uvs.Add(new Vector2(1, 0));
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }

            // Left face (-X normal, Unity LH CW)
            AddTri(left[0], left[2], left[1]);
            AddTri(left[0], left[4], left[2]);
            AddTri(left[4], left[3], left[2]);

            // Right face (+X normal, Unity LH CW)
            AddTri(right[0], right[1], right[2]);
            AddTri(right[0], right[2], right[4]);
            AddTri(right[4], right[2], right[3]);

            // Perimeter edge quads (outward-facing Unity LH CW)
            for (int k = 0; k < 5; k++)
            {
                int nk = (k + 1) % 5;
                AddQuad(left[k], left[nk], right[nk], right[k]);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveMesh(Mesh mesh, string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
            AssetDatabase.CreateAsset(mesh, assetPath);
            return AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        }

        private static Material CreateOrLoadMaterial(
            string matPath, Color baseColor, string diffPath, string normPath, float smoothness)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = urpLit;
            }

            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);

            if (!string.IsNullOrEmpty(diffPath))
            {
                var diff = AssetDatabase.LoadAssetAtPath<Texture2D>(diffPath);
                if (diff != null) mat.SetTexture("_BaseMap", diff);
            }

            if (!string.IsNullOrEmpty(normPath))
            {
                var norm = AssetDatabase.LoadAssetAtPath<Texture2D>(normPath);
                if (norm != null)
                {
                    mat.SetTexture("_BumpMap", norm);
                    mat.SetFloat("_BumpScale", 0.35f);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
