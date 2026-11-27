#if UNITY_EDITOR
using UnityEngine;

namespace SnowpawStation.Editor.World
{
    /// <summary>
    /// Locked structural metrics and art-direction rules for the 350x350 SnowpawStation
    /// winter landscape. Follows scenario-unity-world-building: lock resolutions, grids,
    /// exclusion zones, and budgets before generating content.
    /// </summary>
    public static class WinterLandscapeSpec
    {
        // Scene and output asset paths
        public const string TargetScenePath = "Assets/Scenes/StationMain.unity";
        public const string OutputRoot = "Assets/Game/Environment/WinterLandscape";
        public const string TerrainDataPath = OutputRoot + "/WinterLandscape_TerrainData.asset";
        public const string LayersFolder = OutputRoot + "/Layers";
        public const string TexturesFolder = OutputRoot + "/Textures";
        public const string MaterialsFolder = OutputRoot + "/Materials";
        public const string MeshesFolder = OutputRoot + "/Meshes";
        public const string PrefabsFolder = OutputRoot + "/Prefabs";

        // Generated hierarchy root (non-destructive, idempotent)
        public const string GeneratedRootName = "WinterLandscape_Generated";
        public const string TerrainObjectName = "WinterTerrain_350";
        public const string AnchorAName = "Anchor_StationTrailhead";
        public const string AnchorBName = "Anchor_NorthRidgePass";

        // Existing scene objects that must never be moved, replaced, covered, or modified
        public const string SnowFloorName = "SnowFloor";
        public const string StatBuildingName = "stat_building";
        public const string StatShopName = "stat_shop";

        // World dimensions — matches SnowFloor (pos 0,0,0; scale 350,1,350; top at Y = 0.5)
        public const float WorldSize = 350f;
        public const float HalfWorldSize = WorldSize * 0.5f; // 175m -> [-175, +175]
        public const float TerrainMaxHeight = 48f;
        public const float SnowFloorTopY = 0.50f;
        // Sit 2 cm below SnowFloor in the flat station basin to prevent Z-fighting while
        // preserving SnowFloor as the primary playable ground surface.
        public const float StationBasinTerrainY = 0.48f;
        // Minimum height outside the station blend ring so SnowFloor never pokes through valleys.
        public const float OuterMinTerrainY = 0.54f;

        // Locked TerrainData resolutions (set BEFORE writing any height/splat/detail content)
        public const int HeightmapResolution = 513;  // 2^n + 1 (~0.68m/sample on 350x350)
        public const int AlphamapResolution = 512;
        public const int BasemapResolution = 512;
        public const int DetailResolution = 512;
        public const int DetailPerPatch = 16;        // Unity 6 Terrain manual recommendation

        // Deterministic generation seed
        public const int Seed = 20261127;

        // Gentle fBm hills + subtle wind-carved snowdrifts surrounding the station
        public const float NoiseScale = 95f;
        public const int NoiseOctaves = 5;
        public const float NoisePersistence = 0.46f;
        public const float NoiseLacunarity = 2.05f;
        public const float HillAmplitude = 18.5f;
        public const float OuterRimLift = 5.5f;
        public const float WindDriftAmplitude = 0.42f;

        // Subtle hydraulic droplet erosion (restrained for soft winter contours)
        public const int ErosionDroplets = 18000;
        public const int ErosionMaxLifetime = 36;
        public const float ErosionInertia = 0.12f;
        public const float ErosionStrength = 0.18f;
        public const float DepositionStrength = 0.22f;
        public const float EvaporationRate = 0.025f;

        // Station playable basin (kept level for gameplay, vehicle approach, and sightlines)
        // Covers gas_station (-5, 0.5, 0), House (-17.65, 1, -6.89), WoodPile (-24.34, 0.5, -7.64),
        // WoodenWithAxe (0.11, 0, -14.55), SnowPile (-13.86/-10.85), PlayerCapsule (-7.96, 0.5, 6.22).
        public static readonly Vector2 StationCenterXZ = new Vector2(-10f, -3f);
        public const float StationFlatRadius = 34f;
        public const float StationBlendRadius = 62f;

        // Hard exclusion clearance around stat_building and stat_shop (no new objects allowed)
        public const float BuildingExclusionPadding = 20f;
        public const float ExistingObjectPadding = 4.5f;

        // Two designated route anchors (world XZ):
        // Anchor A: edge of the open station approach (clear of stat_building & stat_shop)
        // Anchor B: scenic outer winter ridge pass in the northeast quadrant
        public static readonly Vector2 AnchorAXZ = new Vector2(22f, 24f);
        public static readonly Vector2 AnchorBXZ = new Vector2(126f, 118f);

        // Winter road / packed-snow trail parameters
        public const float TrailHalfWidth = 3.2f;
        public const float TrailShoulderWidth = 3.5f;
        public const float TrailSnowbankHeight = 0.18f;
        public const float MaxTrailGradePercent = 10.0f; // Hard gate: <= 10%
        public const float TrailTreeClearance = 7.5f;

        // Tree & boulder scattering rules
        public const float MaxTreeSlopeDeg = 22f;
        public const float TreeMinSpacing = 9.5f;
        public const float TreeCellSize = 11.5f;
        public const float EdgeSaplingSpacing = 5.5f;
        public const float WorldEdgeMargin = 10f;
        // Negative-Y base extension on trunks/rocks so pivots never float on slopes up to 22 deg
        public const float PropBaseEmbedY = 0.35f;

        // Ground-truth Station_SnowSurface.mat alignment (tiling 10x10 on 350m = 35m tile, _BumpScale=0.2, _Smoothness=0.05)
        public static readonly Color StationSnowBaseColor = new Color(0.86f, 0.90f, 0.94f, 1f);
        public const float StationSnowTileMeters = 35f;
        public const float StationSnowNormalScale = 0.20f;
        public const float StationSnowSmoothness = 0.05f;

        // Verified existing project textures for cohesive winter palette (no grass!)
        public const string TexSnowAerialDiff = "Assets/RawAssets/Textures/snow_field_aerial_2k.blend/textures/snow_field_aerial_col_2k.jpg";
        public const string TexSnowAerialNorm = "Assets/RawAssets/Textures/snow_field_aerial_2k.blend/textures/snow_field_aerial_nor_gl_2k.exr";
        public const string TexSnowPrimaryDiff = "Assets/RawAssets/Textures/snow_01_2k.blend/textures/snow_01_diff_2k.jpg";
        public const string TexSnowPrimaryNorm = "Assets/RawAssets/Textures/snow_01_2k.blend/textures/snow_01_nor_gl_2k.exr";
        public const string TexSnowTrailDiff = "Assets/RawAssets/Textures/asphalt_snow_2k.blend/textures/asphalt_snow_diff_2k.jpg";
        public const string TexSnowTrailNorm = "Assets/RawAssets/Textures/asphalt_snow_2k.blend/textures/asphalt_snow_nor_gl_2k.exr";
        public const string TexSnowSecondaryDiff = "Assets/RawAssets/Textures/snow_02_2k.blend/textures/snow_02_diff_2k.jpg";
        public const string TexSnowSecondaryNorm = "Assets/RawAssets/Textures/snow_02_2k.blend/textures/snow_02_nor_gl_2k.exr";
        public const string TexPineBarkDiff = "Assets/RawAssets/Textures/pine_bark_2k.blend/textures/pine_bark_diff_2k.jpg";
        public const string TexPineBarkNorm = "Assets/RawAssets/Textures/pine_bark_2k.blend/textures/pine_bark_nor_gl_2k.exr";
        public const string StationSnowMaterialPath = "Assets/Game/Materials/Station_SnowSurface.mat";
    }
}
#endif
