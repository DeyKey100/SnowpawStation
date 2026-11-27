#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SnowpawStation.Editor.World
{
    /// <summary>
    /// Idempotent, non-destructive Unity Editor builder and mechanical auditor for the
    /// 350x350 SnowpawStation winter landscape in Assets/Scenes/StationMain.unity.
    ///
    /// Follows scenario-unity-world-building & scenario-unity-expert:
    ///   - Locks TerrainData resolutions BEFORE content and persists TerrainData BEFORE SetAlphamaps.
    ///   - Preserves SnowFloor, stat_building, stat_shop, and every existing scene object untouched.
    ///   - Generates gentle fBm hills + subtle droplet erosion + level station basin.
    ///   - Routes a slope-aware A* packed-snow winter trail (<= 10% max grade) between two anchors.
    ///   - Uses a cohesive winter palette (4 layers, snow dominant, no grass).
    ///   - Scatters sparse LOD trees/rocks respecting slope (<= 22 deg), trail, and building clearances.
    /// </summary>
    public static class WinterLandscapeBuilder
    {
        private struct ExclusionCircle
        {
            public Vector2 CenterXZ;
            public float Radius;
            public string Label;
        }

        [MenuItem("SnowpawStation/World/Build 350x350 Winter Landscape")]
        public static void BuildMenu()
        {
            BuildLandscape(saveScene: true);
        }

        [MenuItem("SnowpawStation/World/Audit Winter Landscape")]
        public static void AuditMenu()
        {
            AuditLandscape();
        }

        [MenuItem("SnowpawStation/World/Clean Generated Winter Landscape")]
        public static void CleanMenu()
        {
            Scene scene = EnsureTargetSceneOpen();
            RemoveExistingGeneratedRoot(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[WinterLandscape] Removed generated root '" + WinterLandscapeSpec.GeneratedRootName + "'. Existing scene objects remain untouched.");
        }

        /// <summary>
        /// Entry point for Unity batch mode: -executeMethod SnowpawStation.Editor.World.WinterLandscapeBuilder.BuildFromBatch
        /// </summary>
        public static void BuildFromBatch()
        {
            BuildLandscape(saveScene: true);
        }

        /// <summary>
        /// Entry point for Unity batch mode: -executeMethod SnowpawStation.Editor.World.WinterLandscapeBuilder.AuditFromBatch
        /// </summary>
        public static void AuditFromBatch()
        {
            AuditLandscape();
        }

        public static void BuildLandscape(bool saveScene)
        {
            if (!WinterTerrainGenerator.IsPow2Plus1(WinterLandscapeSpec.HeightmapResolution))
            {
                throw new InvalidOperationException("HeightmapResolution must be 2^n + 1.");
            }

            Scene scene = EnsureTargetSceneOpen();
            RemoveExistingGeneratedRoot(scene);

            // Collect hard exclusion zones from ALL existing scene objects before adding anything.
            List<ExclusionCircle> exclusions = CollectSceneExclusions(scene);

            WinterFoliageFactory.EnsureFolder(WinterLandscapeSpec.OutputRoot);
            WinterFoliageFactory.EnsureFolder(WinterLandscapeSpec.LayersFolder);

            // 1. Lock TerrainData resolutions FIRST and persist asset BEFORE writing layers/alphamaps.
            var td = new TerrainData();
            td.heightmapResolution = WinterLandscapeSpec.HeightmapResolution;
            td.size = new Vector3(
                WinterLandscapeSpec.WorldSize,
                WinterLandscapeSpec.TerrainMaxHeight,
                WinterLandscapeSpec.WorldSize);
            td.alphamapResolution = WinterLandscapeSpec.AlphamapResolution;
            td.baseMapResolution = WinterLandscapeSpec.BasemapResolution;
            td.SetDetailResolution(WinterLandscapeSpec.DetailResolution, WinterLandscapeSpec.DetailPerPatch);
            td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);

            if (AssetDatabase.LoadAssetAtPath<TerrainData>(WinterLandscapeSpec.TerrainDataPath) != null)
            {
                AssetDatabase.DeleteAsset(WinterLandscapeSpec.TerrainDataPath);
            }
            AssetDatabase.CreateAsset(td, WinterLandscapeSpec.TerrainDataPath);

            // 2. Generate heights: gentle fBm hills -> subtle erosion -> level station basin -> A* winter trail (<= 10%).
            int hRes = WinterLandscapeSpec.HeightmapResolution;
            float[,] heightsMeters = WinterTerrainGenerator.GenerateBaseHills(hRes, WinterLandscapeSpec.Seed);
            float[,] erosionMap = WinterTerrainGenerator.ApplySubtleErosion(heightsMeters, WinterLandscapeSpec.Seed);
            WinterTerrainGenerator.ApplyStationLeveler(heightsMeters, erosionMap);

            WinterTerrainGenerator.TrailResult trail = WinterTerrainGenerator.RouteAndGradeWinterTrail(
                heightsMeters,
                WinterLandscapeSpec.AnchorAXZ,
                WinterLandscapeSpec.AnchorBXZ);

            // Normalize to [0..1] for TerrainData.SetHeights
            var heightsNorm = new float[hRes, hRes];
            for (int gz = 0; gz < hRes; gz++)
            {
                for (int gx = 0; gx < hRes; gx++)
                {
                    heightsNorm[gz, gx] = Mathf.Clamp01(heightsMeters[gz, gx] / WinterLandscapeSpec.TerrainMaxHeight);
                }
            }
            td.SetHeights(0, 0, heightsNorm);

            // 3. Create 4 cohesive winter TerrainLayers (NO grass!) and write alphamaps.
            td.terrainLayers = CreateWinterTerrainLayers();
            float[,,] alphamaps = WinterTerrainGenerator.BuildWinterAlphamaps(
                heightsMeters, erosionMap, trail.WaypointsWorld, out float[] layerCoverage);
            td.SetAlphamaps(0, 0, alphamaps);
            EditorUtility.SetDirty(td);

            // 4. Create root hierarchy and Terrain GameObject aligned with SnowFloor [-175..+175].
            var generatedRoot = new GameObject(WinterLandscapeSpec.GeneratedRootName);
            SceneManager.MoveGameObjectToScene(generatedRoot, scene);

            GameObject terrainGo = Terrain.CreateTerrainGameObject(td);
            terrainGo.name = WinterLandscapeSpec.TerrainObjectName;
            terrainGo.transform.SetParent(generatedRoot.transform, false);
            terrainGo.transform.position = new Vector3(-WinterLandscapeSpec.HalfWorldSize, 0f, -WinterLandscapeSpec.HalfWorldSize);
            terrainGo.isStatic = true;

            Terrain terrain = terrainGo.GetComponent<Terrain>();
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 260f;
            terrain.drawTreesAndFoliage = true;
            terrain.treeDistance = 280f;
            terrain.detailObjectDistance = 80f;

            // 5. Place the two designated route anchors with subtle snow-capped trail markers.
            WinterFoliageFactory.FoliageAssets foliage = WinterFoliageFactory.EnsureFoliagePrefabs();
            PlaceRouteAnchors(generatedRoot.transform, heightsMeters, trail, foliage.TrailMarkerPrefab);

            // 6. Scatter sparse, natural LOD winter trees and occasional frost rocks.
            int treeCount = ScatterWinterVegetation(
                generatedRoot.transform,
                heightsMeters,
                trail.WaypointsWorld,
                exclusions,
                foliage,
                out int rockCount,
                out float maxTreeSlope);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
            {
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log(
                $"[WinterLandscape] Build complete in {WinterLandscapeSpec.TargetScenePath}\n" +
                $"  Terrain: {WinterLandscapeSpec.WorldSize}x{WinterLandscapeSpec.WorldSize}m, Heightmap={hRes}, DetailPatch={WinterLandscapeSpec.DetailPerPatch}\n" +
                $"  Coverage: Snow={layerCoverage[0] * 100f:F1}%, PackedTrail={layerCoverage[1] * 100f:F1}%, FrozenEarth={layerCoverage[2] * 100f:F1}%, ExposedRock={layerCoverage[3] * 100f:F1}%\n" +
                $"  Trail: length={trail.TotalLengthMeters:F1}m, maxGrade={trail.MaxGradePercent:F2}% (gate <= {WinterLandscapeSpec.MaxTrailGradePercent:F1}%)\n" +
                $"  Vegetation: trees={treeCount} (maxSlope={maxTreeSlope:F1} deg, gate <= {WinterLandscapeSpec.MaxTreeSlopeDeg:F1} deg), frostRocks={rockCount}");

            AuditLandscape();
        }

        /// <summary>
        /// Mechanical audit gate verifying terrain resolutions, SnowFloor integrity,
        /// asset persistence, no SnowFloor poke-through outside basin, stat_building/stat_shop
        /// clearance, zero floating props, and procedural mesh / LODGroup health.
        /// </summary>
        public static bool AuditLandscape()
        {
            Scene scene = EnsureTargetSceneOpen();
            var errors = new List<string>();
            var warnings = new List<string>();

            // 1. Check SnowFloor untouched
            GameObject snowFloor = FindInScene(scene, WinterLandscapeSpec.SnowFloorName);
            if (snowFloor == null)
            {
                errors.Add("SnowFloor not found in scene.");
            }
            else
            {
                if (Vector3.Distance(snowFloor.transform.position, Vector3.zero) > 0.001f ||
                    Vector3.Distance(snowFloor.transform.localScale, new Vector3(350f, 1f, 350f)) > 0.001f)
                {
                    errors.Add($"SnowFloor transform modified: pos={snowFloor.transform.position}, scale={snowFloor.transform.localScale}");
                }
            }

            // 2. Check generated terrain & asset persistence
            GameObject genRoot = FindInScene(scene, WinterLandscapeSpec.GeneratedRootName);
            if (genRoot == null)
            {
                errors.Add($"Generated root '{WinterLandscapeSpec.GeneratedRootName}' not found. Run Build first.");
                LogAuditReport(errors, warnings);
                return false;
            }

            Terrain terrain = genRoot.GetComponentInChildren<Terrain>();
            if (terrain == null || terrain.terrainData == null)
            {
                errors.Add("Terrain or TerrainData missing under generated root.");
                LogAuditReport(errors, warnings);
                return false;
            }

            TerrainData td = terrain.terrainData;
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(td)))
            {
                errors.Add("TerrainData is not persisted on disk.");
            }
            if (Mathf.Abs(td.size.x - 350f) > 0.01f || Mathf.Abs(td.size.z - 350f) > 0.01f)
            {
                errors.Add($"Terrain size is {td.size.x}x{td.size.z}, expected 350x350.");
            }
            if (!WinterTerrainGenerator.IsPow2Plus1(td.heightmapResolution))
            {
                errors.Add($"Heightmap resolution {td.heightmapResolution} is not 2^n + 1.");
            }
            if (td.detailResolutionPerPatch != 16)
            {
                warnings.Add($"DetailResolutionPerPatch is {td.detailResolutionPerPatch}, recommended 16.");
            }
            if (td.terrainLayers == null || td.terrainLayers.Length == 0 || td.terrainLayers.Length > 4)
            {
                errors.Add($"Expected 1..4 terrain layers, found {(td.terrainLayers == null ? 0 : td.terrainLayers.Length)}.");
            }
            else
            {
                for (int i = 0; i < td.terrainLayers.Length; i++)
                {
                    TerrainLayer layer = td.terrainLayers[i];
                    if (layer == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(layer)))
                    {
                        errors.Add($"TerrainLayer[{i}] is null or not persisted on disk.");
                    }
                    else if (layer.diffuseTexture == null)
                    {
                        errors.Add($"TerrainLayer[{i}] ('{layer.name}') has a null diffuseTexture.");
                    }
                }
            }

            // 3. Check no terrain sample outside StationBlendRadius drops below SnowFloor (Y = 0.50m)
            int hRes = td.heightmapResolution;
            float[,] hNorm = td.GetHeights(0, 0, hRes, hRes);
            int outerDipCount = 0;
            for (int gz = 0; gz < hRes; gz += 2)
            {
                for (int gx = 0; gx < hRes; gx += 2)
                {
                    Vector2 world = WinterTerrainGenerator.GridToWorldXZ(gx, gz, hRes);
                    if (Vector2.Distance(world, WinterLandscapeSpec.StationCenterXZ) >= WinterLandscapeSpec.StationBlendRadius)
                    {
                        float wy = hNorm[gz, gx] * td.size.y + terrain.transform.position.y;
                        if (wy < WinterLandscapeSpec.SnowFloorTopY - 0.001f)
                        {
                            outerDipCount++;
                        }
                    }
                }
            }
            if (outerDipCount > 0)
            {
                errors.Add($"{outerDipCount} outer terrain samples dip below SnowFloor (Y={WinterLandscapeSpec.SnowFloorTopY}m).");
            }

            // 4. Check stat_building, stat_shop, and all existing root exclusions + LODs + ground contact + meshes
            List<ExclusionCircle> exclusions = CollectSceneExclusions(scene);
            int lodMissing = 0;
            int lodOrderInvalid = 0;
            int meshInvalid = 0;
            int floatingProps = 0;
            int exclusionViolations = 0;

            Transform vegRoot = genRoot.transform.Find("WinterVegetation");
            if (vegRoot != null)
            {
                for (int i = 0; i < vegRoot.childCount; i++)
                {
                    Transform child = vegRoot.GetChild(i);
                    var lodGroup = child.GetComponent<LODGroup>();
                    if (lodGroup == null)
                    {
                        lodMissing++;
                    }
                    else
                    {
                        LOD[] lods = lodGroup.GetLODs();
                        float prevH = 2f;
                        for (int l = 0; l < lods.Length; l++)
                        {
                            if (lods[l].renderers == null || lods[l].renderers.Length == 0 || lods[l].screenRelativeTransitionHeight >= prevH)
                            {
                                lodOrderInvalid++;
                                break;
                            }
                            prevH = lods[l].screenRelativeTransitionHeight;
                        }
                    }

                    // Ground contact check: pivot must sit on or slightly embedded in terrain
                    float groundY = terrain.SampleHeight(child.position) + terrain.transform.position.y;
                    if (child.position.y - groundY > 0.15f)
                    {
                        floatingProps++;
                    }

                    // Mesh & material persistence check
                    foreach (MeshFilter mf in child.GetComponentsInChildren<MeshFilter>(true))
                    {
                        Mesh m = mf.sharedMesh;
                        if (m == null || m.vertexCount == 0 || m.triangles.Length == 0 ||
                            !IsFinite(m.bounds.center) || !IsFinite(m.bounds.extents) ||
                            string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m)))
                        {
                            meshInvalid++;
                        }
                    }
                    foreach (MeshRenderer mr in child.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        Material mat = mr.sharedMaterial;
                        if (mat == null || mat.shader == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mat)))
                        {
                            meshInvalid++;
                        }
                    }

                    // Exclusion check against stat_building, stat_shop, gas_station, and existing root objects
                    Vector2 posXZ = new Vector2(child.position.x, child.position.z);
                    if (IsInsideAnyExclusion(posXZ, exclusions))
                    {
                        exclusionViolations++;
                    }
                }
            }

            if (lodMissing > 0) errors.Add($"{lodMissing} scattered vegetation objects are missing a LODGroup.");
            if (lodOrderInvalid > 0) errors.Add($"{lodOrderInvalid} LODGroups have empty renderers or non-descending transition heights.");
            if (exclusionViolations > 0) errors.Add($"{exclusionViolations} generated objects violate stat_building/stat_shop/existing-object exclusion zones.");

            // 5. Check anchors exist, validate marker meshes/materials/grounding, and verify trail grade <= 10%
            Transform anchorA = genRoot.transform.Find(WinterLandscapeSpec.AnchorAName);
            Transform anchorB = genRoot.transform.Find(WinterLandscapeSpec.AnchorBName);
            if (anchorA == null || anchorB == null)
            {
                errors.Add("Designated route anchors missing under generated root.");
            }
            else
            {
                ValidateAnchorMarker(anchorA, terrain, ref meshInvalid, ref floatingProps);
                ValidateAnchorMarker(anchorB, terrain, ref meshInvalid, ref floatingProps);

                // Re-verify sampled terrain grade along the routed trail corridor (<= 10.0%)
                var auditHeights = new float[hRes, hRes];
                for (int gz = 0; gz < hRes; gz++)
                {
                    for (int gx = 0; gx < hRes; gx++)
                    {
                        auditHeights[gz, gx] = hNorm[gz, gx] * td.size.y + terrain.transform.position.y;
                    }
                }
                WinterTerrainGenerator.TrailResult auditTrail = WinterTerrainGenerator.RouteAndGradeWinterTrail(
                    auditHeights,
                    new Vector2(anchorA.position.x, anchorA.position.z),
                    new Vector2(anchorB.position.x, anchorB.position.z));

                float maxSampledGrade = 0f;
                for (int i = 1; i < auditTrail.WaypointsWorld.Count; i++)
                {
                    Vector3 p0 = auditTrail.WaypointsWorld[i - 1];
                    Vector3 p1 = auditTrail.WaypointsWorld[i];
                    float y0 = terrain.SampleHeight(p0) + terrain.transform.position.y;
                    float y1 = terrain.SampleHeight(p1) + terrain.transform.position.y;
                    float horiz = Vector2.Distance(new Vector2(p0.x, p0.z), new Vector2(p1.x, p1.z));
                    if (horiz > 0.05f)
                    {
                        float gradePct = Mathf.Abs(y1 - y0) / horiz * 100f;
                        if (gradePct > maxSampledGrade) maxSampledGrade = gradePct;
                    }
                }

                if (maxSampledGrade > WinterLandscapeSpec.MaxTrailGradePercent + 0.01f)
                {
                    errors.Add($"Sampled trail grade {maxSampledGrade:F2}% exceeds {WinterLandscapeSpec.MaxTrailGradePercent:F1}% limit.");
                }
            }

            if (meshInvalid > 0) errors.Add($"{meshInvalid} procedural meshes/materials (vegetation or markers) are invalid or not persisted on disk.");
            if (floatingProps > 0) errors.Add($"{floatingProps} props/markers are floating above terrain surface.");

            LogAuditReport(errors, warnings);
            return errors.Count == 0;
        }

        private static void ValidateAnchorMarker(Transform anchor, Terrain terrain, ref int meshInvalid, ref int floatingProps)
        {
            for (int i = 0; i < anchor.childCount; i++)
            {
                Transform marker = anchor.GetChild(i);
                float groundY = terrain.SampleHeight(marker.position) + terrain.transform.position.y;
                if (marker.position.y - groundY > 0.15f)
                {
                    floatingProps++;
                }

                foreach (MeshFilter mf in marker.GetComponentsInChildren<MeshFilter>(true))
                {
                    Mesh m = mf.sharedMesh;
                    if (m == null || m.vertexCount == 0 || m.triangles.Length == 0 ||
                        !IsFinite(m.bounds.center) || !IsFinite(m.bounds.extents) ||
                        string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m)))
                    {
                        meshInvalid++;
                    }
                }
                foreach (MeshRenderer mr in marker.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Material mat = mr.sharedMaterial;
                    if (mat == null || mat.shader == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mat)))
                    {
                        meshInvalid++;
                    }
                }
            }
        }

        private static bool IsFinite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
            !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
            !float.IsNaN(v.z) && !float.IsInfinity(v.z);

        private static TerrainLayer[] CreateWinterTerrainLayers()
        {
            // Align Layer 0 directly with Station_SnowSurface.mat ground truth
            Texture2D snowDiff = null;
            Texture2D snowNorm = null;
            var snowFloorMat = AssetDatabase.LoadAssetAtPath<Material>(WinterLandscapeSpec.StationSnowMaterialPath);
            if (snowFloorMat != null)
            {
                snowDiff = snowFloorMat.GetTexture("_BaseMap") as Texture2D;
                snowNorm = snowFloorMat.GetTexture("_BumpMap") as Texture2D;
            }
            snowDiff ??= AssetDatabase.LoadAssetAtPath<Texture2D>(WinterLandscapeSpec.TexSnowAerialDiff)
                      ?? AssetDatabase.LoadAssetAtPath<Texture2D>(WinterLandscapeSpec.TexSnowPrimaryDiff);
            snowNorm ??= AssetDatabase.LoadAssetAtPath<Texture2D>(WinterLandscapeSpec.TexSnowAerialNorm)
                      ?? AssetDatabase.LoadAssetAtPath<Texture2D>(WinterLandscapeSpec.TexSnowPrimaryNorm);

            Texture2D trailDiff = AssetDatabase.LoadAssetAtPath<Texture2D>(WinterLandscapeSpec.TexSnowTrailDiff) ?? snowDiff;
            Texture2D trailNorm = AssetDatabase.LoadAssetAtPath<Texture2D>(WinterLandscapeSpec.TexSnowTrailNorm) ?? snowNorm;
            Texture2D snow2Norm = AssetDatabase.LoadAssetAtPath<Texture2D>(WinterLandscapeSpec.TexSnowSecondaryNorm) ?? snowNorm;

            // URP 17 Terrain/Lit ignores TerrainLayer.diffuseRemapMax; bake genuine dark winter
            // albedo PNGs on disk for FrozenEarth and ExposedWinterRock so they never render white.
            WinterFoliageFactory.EnsureFolder(WinterLandscapeSpec.TexturesFolder);
            Texture2D frozenEarthDiff = CreateOrLoadProceduralWinterTexture(
                WinterLandscapeSpec.TexturesFolder + "/Tex_FrozenEarth_Diff.png",
                new Color(0.28f, 0.25f, 0.24f, 1f),
                new Color(0.39f, 0.36f, 0.35f, 1f),
                new Color(0.76f, 0.80f, 0.85f, 1f),
                frostAmount: 0.14f,
                seed: 101);
            Texture2D exposedRockDiff = CreateOrLoadProceduralWinterTexture(
                WinterLandscapeSpec.TexturesFolder + "/Tex_ExposedWinterRock_Diff.png",
                new Color(0.23f, 0.26f, 0.31f, 1f),
                new Color(0.36f, 0.40f, 0.46f, 1f),
                new Color(0.78f, 0.82f, 0.88f, 1f),
                frostAmount: 0.18f,
                seed: 202);

            return new[]
            {
                SaveTerrainLayer(
                    "Layer_00_WinterSnowPrimary", snowDiff, snowNorm,
                    new Vector2(WinterLandscapeSpec.StationSnowTileMeters, WinterLandscapeSpec.StationSnowTileMeters),
                    WinterLandscapeSpec.StationSnowNormalScale,
                    WinterLandscapeSpec.StationSnowSmoothness),
                SaveTerrainLayer(
                    "Layer_01_PackedSnowTrail", trailDiff, trailNorm,
                    new Vector2(10f, 10f),
                    0.35f,
                    0.16f),
                SaveTerrainLayer(
                    "Layer_02_FrozenEarth", frozenEarthDiff, snow2Norm,
                    new Vector2(14f, 14f),
                    0.45f,
                    0.08f),
                SaveTerrainLayer(
                    "Layer_03_ExposedWinterRock", exposedRockDiff, snow2Norm,
                    new Vector2(12f, 12f),
                    0.55f,
                    0.14f)
            };
        }

        private static Texture2D CreateOrLoadProceduralWinterTexture(
            string assetPath, Color darkCol, Color midCol, Color frostCol, float frostAmount, int seed)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    // Tileable periodic noise via 2D trigonometric domain wrap
                    float nx = Mathf.Cos(u * Mathf.PI * 2f) * 1.4f + seed;
                    float ny = Mathf.Sin(u * Mathf.PI * 2f) * 1.4f + seed;
                    float nz = Mathf.Cos(v * Mathf.PI * 2f) * 1.4f + seed * 0.5f;
                    float nw = Mathf.Sin(v * Mathf.PI * 2f) * 1.4f + seed * 0.5f;

                    float n1 = Mathf.PerlinNoise(nx * 2.2f + nz * 1.1f, ny * 2.2f + nw * 1.1f);
                    float n2 = Mathf.PerlinNoise(nx * 6.5f - nw * 3.2f, ny * 6.5f + nz * 3.2f);
                    float blend = Mathf.Clamp01(n1 * 0.65f + n2 * 0.35f);

                    Color c = Color.Lerp(darkCol, midCol, blend);
                    if (n2 > 1f - frostAmount)
                    {
                        float f = Mathf.InverseLerp(1f - frostAmount, 1f, n2);
                        c = Color.Lerp(c, frostCol, f * 0.55f);
                    }
                    pixels[y * size + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            string fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static TerrainLayer SaveTerrainLayer(
            string name, Texture2D diff, Texture2D norm, Vector2 tileSize,
            float normalScale, float smoothness)
        {
            string path = WinterLandscapeSpec.LayersFolder + "/" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }

            layer.diffuseTexture = diff;
            layer.normalMapTexture = norm;
            layer.tileSize = tileSize;
            layer.normalScale = normalScale;
            layer.specular = new Color(0.18f, 0.20f, 0.22f, 1f);
            layer.smoothness = smoothness;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static void PlaceRouteAnchors(
            Transform parent, float[,] heights, WinterTerrainGenerator.TrailResult trail, GameObject markerPrefab)
        {
            if (trail.WaypointsWorld == null || trail.WaypointsWorld.Count < 2) return;

            int last = trail.WaypointsWorld.Count - 1;
            Vector3 posA = trail.WaypointsWorld[0];
            Vector3 tanA = (trail.WaypointsWorld[1] - posA);
            tanA.y = 0f;

            Vector3 posB = trail.WaypointsWorld[last];
            Vector3 tanB = (trail.WaypointsWorld[last - 1] - posB);
            tanB.y = 0f;

            CreateAnchorObject(WinterLandscapeSpec.AnchorAName, parent, heights, posA, tanA, markerPrefab);
            CreateAnchorObject(WinterLandscapeSpec.AnchorBName, parent, heights, posB, tanB, markerPrefab);
        }

        private static void CreateAnchorObject(
            string name, Transform parent, float[,] heights, Vector3 worldPos, Vector3 trailTangentXZ, GameObject markerPrefab)
        {
            var anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.position = worldPos;

            if (markerPrefab != null)
            {
                Vector3 fwd = trailTangentXZ.sqrMagnitude > 1e-4f ? trailTangentXZ.normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                Vector3 markerXZ = worldPos + right * (WinterLandscapeSpec.TrailHalfWidth + 1.2f);
                float markerY = WinterTerrainGenerator.SampleWorldHeight(heights, markerXZ.x, markerXZ.z);

                var marker = (GameObject)PrefabUtility.InstantiatePrefab(markerPrefab);
                marker.name = name + "_Marker";
                marker.transform.SetParent(anchor.transform, false);
                marker.transform.position = new Vector3(markerXZ.x, markerY, markerXZ.z);
                marker.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
        }

        private static int ScatterWinterVegetation(
            Transform parent,
            float[,] heights,
            List<Vector3> trailWaypoints,
            List<ExclusionCircle> exclusions,
            WinterFoliageFactory.FoliageAssets foliage,
            out int rockCount,
            out float maxTreeSlopeDeg)
        {
            var vegRoot = new GameObject("WinterVegetation");
            vegRoot.transform.SetParent(parent, false);

            var rng = new System.Random(WinterLandscapeSpec.Seed ^ 0x71E3);
            var placedXZ = new List<Vector2>();
            int treeCount = 0;
            rockCount = 0;
            maxTreeSlopeDeg = 0f;

            float minCoord = -WinterLandscapeSpec.HalfWorldSize + WinterLandscapeSpec.WorldEdgeMargin;
            float maxCoord = WinterLandscapeSpec.HalfWorldSize - WinterLandscapeSpec.WorldEdgeMargin;
            float cell = WinterLandscapeSpec.TreeCellSize;

            for (float z = minCoord; z < maxCoord; z += cell)
            {
                for (float x = minCoord; x < maxCoord; x += cell)
                {
                    float wx = x + (float)(rng.NextDouble() * 0.78 + 0.11) * cell;
                    float wz = z + (float)(rng.NextDouble() * 0.78 + 0.11) * cell;
                    Vector2 posXZ = new Vector2(wx, wz);

                    // Keep sparse & natural using a low-frequency forest grove mask
                    float groveMask = Mathf.PerlinNoise((wx + 400f) * 0.018f, (wz + 400f) * 0.018f);
                    if (groveMask < 0.44f) continue;

                    // Station playable basin exclusion
                    if (Vector2.Distance(posXZ, WinterLandscapeSpec.StationCenterXZ) < WinterLandscapeSpec.StationFlatRadius + 10f)
                        continue;

                    // Trail clearance
                    WinterTerrainGenerator.FindNearestWaypoint(posXZ, trailWaypoints, out float trailDist, out _);
                    if (trailDist < WinterLandscapeSpec.TrailTreeClearance) continue;

                    // Hard exclusion around stat_building, stat_shop, and all existing scene objects
                    if (IsInsideAnyExclusion(posXZ, exclusions)) continue;

                    // Slope rule: avoid steep slopes
                    float slopeDeg = WinterTerrainGenerator.SampleWorldSlopeDeg(heights, wx, wz);
                    if (slopeDeg > WinterLandscapeSpec.MaxTreeSlopeDeg) continue;

                    // Minimum spacing to prevent clutter & intersections
                    if (IsTooClose(posXZ, placedXZ, WinterLandscapeSpec.TreeMinSpacing)) continue;

                    float wy = WinterTerrainGenerator.SampleWorldHeight(heights, wx, wz);
                    // Only place on terrain above SnowFloor level
                    if (wy < WinterLandscapeSpec.SnowFloorTopY - 0.02f) continue;

                    // Organic grove composition: smaller saplings near grove edges, taller trees in grove cores
                    float groveCoreT = Mathf.Clamp01((groveMask - 0.44f) / 0.36f);
                    bool placeRock = slopeDeg > 14f && rng.NextDouble() < 0.24;
                    GameObject prefab = placeRock
                        ? foliage.FrostRockPrefab
                        : (rng.NextDouble() < 0.55 ? foliage.PinePrefab : foliage.SprucePrefab);

                    if (prefab == null) continue;

                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    inst.transform.SetParent(vegRoot.transform, false);
                    inst.transform.position = new Vector3(wx, wy, wz);
                    inst.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                    float scale = placeRock
                        ? (float)(0.75 + rng.NextDouble() * 0.65)
                        : Mathf.Lerp(0.62f, 1.24f, groveCoreT) * (float)(0.88 + rng.NextDouble() * 0.24);
                    inst.transform.localScale = Vector3.one * scale;
                    inst.isStatic = true;

                    placedXZ.Add(posXZ);
                    if (placeRock)
                    {
                        rockCount++;
                    }
                    else
                    {
                        treeCount++;
                        if (slopeDeg > maxTreeSlopeDeg) maxTreeSlopeDeg = slopeDeg;

                        // Occasional smaller satellite sapling near grove cores for natural clustering
                        if (groveCoreT > 0.45f && rng.NextDouble() < 0.30)
                        {
                            float ang = (float)(rng.NextDouble() * Math.PI * 2.0);
                            float dist = WinterLandscapeSpec.EdgeSaplingSpacing + (float)(rng.NextDouble() * 2.2);
                            Vector2 satXZ = posXZ + new Vector2(Mathf.Cos(ang) * dist, Mathf.Sin(ang) * dist);
                            if (satXZ.x > minCoord && satXZ.x < maxCoord && satXZ.y > minCoord && satXZ.y < maxCoord &&
                                Vector2.Distance(satXZ, WinterLandscapeSpec.StationCenterXZ) >= WinterLandscapeSpec.StationFlatRadius + 10f &&
                                !IsInsideAnyExclusion(satXZ, exclusions) &&
                                !IsTooClose(satXZ, placedXZ, WinterLandscapeSpec.EdgeSaplingSpacing))
                            {
                                WinterTerrainGenerator.FindNearestWaypoint(satXZ, trailWaypoints, out float satTrailDist, out _);
                                float satSlope = WinterTerrainGenerator.SampleWorldSlopeDeg(heights, satXZ.x, satXZ.y);
                                float satY = WinterTerrainGenerator.SampleWorldHeight(heights, satXZ.x, satXZ.y);
                                if (satTrailDist >= WinterLandscapeSpec.TrailTreeClearance &&
                                    satSlope <= WinterLandscapeSpec.MaxTreeSlopeDeg &&
                                    satY >= WinterLandscapeSpec.SnowFloorTopY)
                                {
                                    GameObject satPrefab = rng.NextDouble() < 0.5 ? foliage.PinePrefab : foliage.SprucePrefab;
                                    var sat = (GameObject)PrefabUtility.InstantiatePrefab(satPrefab);
                                    sat.transform.SetParent(vegRoot.transform, false);
                                    sat.transform.position = new Vector3(satXZ.x, satY, satXZ.y);
                                    sat.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                                    sat.transform.localScale = Vector3.one * (float)(0.52 + rng.NextDouble() * 0.22);
                                    sat.isStatic = true;
                                    placedXZ.Add(satXZ);
                                    treeCount++;
                                    if (satSlope > maxTreeSlopeDeg) maxTreeSlopeDeg = satSlope;
                                }
                            }
                        }
                    }
                }
            }

            return treeCount;
        }

        private static List<ExclusionCircle> CollectSceneExclusions(Scene scene)
        {
            var list = new List<ExclusionCircle>();

            // Explicit generous exclusion for stat_building and stat_shop (and all their children)
            var buildingBounds = new List<Bounds>();
            CollectNamedBounds(scene, WinterLandscapeSpec.StatBuildingName, buildingBounds);
            CollectNamedBounds(scene, WinterLandscapeSpec.StatShopName, buildingBounds);
            foreach (Bounds b in buildingBounds)
            {
                list.Add(new ExclusionCircle
                {
                    CenterXZ = new Vector2(b.center.x, b.center.z),
                    Radius = Mathf.Max(b.extents.x, b.extents.z) + WinterLandscapeSpec.BuildingExclusionPadding,
                    Label = "stat_building/stat_shop"
                });
            }

            // Exclusion for every existing scene renderer (except SnowFloor itself)
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == WinterLandscapeSpec.SnowFloorName || root.name == WinterLandscapeSpec.GeneratedRootName)
                    continue;

                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    list.Add(new ExclusionCircle
                    {
                        CenterXZ = new Vector2(root.transform.position.x, root.transform.position.z),
                        Radius = WinterLandscapeSpec.ExistingObjectPadding,
                        Label = root.name
                    });
                    continue;
                }

                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);

                float pad = root.name.IndexOf("gas_station", StringComparison.OrdinalIgnoreCase) >= 0
                    ? WinterLandscapeSpec.BuildingExclusionPadding
                    : WinterLandscapeSpec.ExistingObjectPadding;

                list.Add(new ExclusionCircle
                {
                    CenterXZ = new Vector2(combined.center.x, combined.center.z),
                    Radius = Mathf.Max(combined.extents.x, combined.extents.z) + pad,
                    Label = root.name
                });
            }

            return list;
        }

        private static void CollectNamedBounds(Scene scene, string targetName, List<Bounds> output)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!string.Equals(t.name, targetName, StringComparison.OrdinalIgnoreCase)) continue;
                    Renderer[] renderers = t.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length > 0)
                    {
                        Bounds b = renderers[0].bounds;
                        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                        output.Add(b);
                    }
                    else
                    {
                        output.Add(new Bounds(t.position, new Vector3(12f, 6f, 12f)));
                    }
                }
            }
        }

        private static bool IsInsideAnyExclusion(Vector2 posXZ, List<ExclusionCircle> exclusions)
        {
            for (int i = 0; i < exclusions.Count; i++)
            {
                if (Vector2.Distance(posXZ, exclusions[i].CenterXZ) < exclusions[i].Radius)
                    return true;
            }
            return false;
        }

        private static bool IsTooClose(Vector2 posXZ, List<Vector2> existing, float minSpacing)
        {
            float minSq = minSpacing * minSpacing;
            for (int i = 0; i < existing.Count; i++)
            {
                if ((posXZ - existing[i]).sqrMagnitude < minSq) return true;
            }
            return false;
        }

        private static Scene EnsureTargetSceneOpen()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && string.Equals(active.path, WinterLandscapeSpec.TargetScenePath, StringComparison.OrdinalIgnoreCase))
            {
                return active;
            }
            return EditorSceneManager.OpenScene(WinterLandscapeSpec.TargetScenePath, OpenSceneMode.Single);
        }

        private static void RemoveExistingGeneratedRoot(Scene scene)
        {
            GameObject existing = FindInScene(scene, WinterLandscapeSpec.GeneratedRootName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }
            return null;
        }

        private static void LogAuditReport(List<string> errors, List<string> warnings)
        {
            if (errors.Count == 0 && warnings.Count == 0)
            {
                Debug.Log("[WinterLandscape][AUDIT] PASS: 0 errors, 0 warnings.");
                return;
            }

            foreach (string w in warnings) Debug.LogWarning("[WinterLandscape][AUDIT] WARN: " + w);
            foreach (string e in errors) Debug.LogError("[WinterLandscape][AUDIT] ERROR: " + e);
        }
    }
}
#endif
