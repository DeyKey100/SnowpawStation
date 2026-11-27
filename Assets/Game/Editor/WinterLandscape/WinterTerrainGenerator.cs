#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnowpawStation.Editor.World
{
    /// <summary>
    /// Pure deterministic heightmap, subtle erosion, slope-aware A* winter trail routing,
    /// and 4-layer winter splatmap generation for the 350x350 SnowpawStation landscape.
    /// </summary>
    public static class WinterTerrainGenerator
    {
        public struct TrailResult
        {
            public List<Vector3> WaypointsWorld;
            public float MaxGradePercent;
            public float TotalLengthMeters;
        }

        public static bool IsPow2Plus1(int n) => n >= 33 && ((n - 1) & (n - 2)) == 0;

        /// <summary>
        /// Converts heightmap grid indices (gx, gz) to world XZ in [-HalfWorldSize, +HalfWorldSize].
        /// </summary>
        public static Vector2 GridToWorldXZ(int gx, int gz, int res)
        {
            float u = gx / (float)(res - 1);
            float v = gz / (float)(res - 1);
            return new Vector2(
                u * WinterLandscapeSpec.WorldSize - WinterLandscapeSpec.HalfWorldSize,
                v * WinterLandscapeSpec.WorldSize - WinterLandscapeSpec.HalfWorldSize);
        }

        /// <summary>
        /// Converts world XZ to fractional heightmap grid coordinates.
        /// </summary>
        public static Vector2 WorldXZToGrid(Vector2 worldXZ, int res)
        {
            float u = (worldXZ.x + WinterLandscapeSpec.HalfWorldSize) / WinterLandscapeSpec.WorldSize;
            float v = (worldXZ.y + WinterLandscapeSpec.HalfWorldSize) / WinterLandscapeSpec.WorldSize;
            return new Vector2(
                Mathf.Clamp(u * (res - 1), 0f, res - 1),
                Mathf.Clamp(v * (res - 1), 0f, res - 1));
        }

        /// <summary>
        /// Generates gentle, natural multi-octave fBm winter hills (heights in metres).
        /// </summary>
        public static float[,] GenerateBaseHills(int res, int seed)
        {
            var heights = new float[res, res]; // [gz, gx] to match TerrainData.SetHeights
            var rng = new System.Random(seed);
            var offsets = new Vector2[WinterLandscapeSpec.NoiseOctaves];
            for (int o = 0; o < offsets.Length; o++)
            {
                offsets[o] = new Vector2(
                    (float)(rng.NextDouble() * 2000.0 - 1000.0),
                    (float)(rng.NextDouble() * 2000.0 - 1000.0));
            }

            for (int gz = 0; gz < res; gz++)
            {
                for (int gx = 0; gx < res; gx++)
                {
                    Vector2 world = GridToWorldXZ(gx, gz, res);
                    float amp = 1f;
                    float freq = 1f / WinterLandscapeSpec.NoiseScale;
                    float sum = 0f;
                    float norm = 0f;

                    for (int o = 0; o < WinterLandscapeSpec.NoiseOctaves; o++)
                    {
                        float sx = (world.x + offsets[o].x) * freq;
                        float sz = (world.y + offsets[o].y) * freq;
                        float n = Mathf.PerlinNoise(sx + 1000f, sz + 1000f);
                        sum += n * amp;
                        norm += amp;
                        amp *= WinterLandscapeSpec.NoisePersistence;
                        freq *= WinterLandscapeSpec.NoiseLacunarity;
                    }

                    float fbm = sum / Mathf.Max(norm, 1e-5f);
                    // Soften hills into broad winter swells
                    float shaped = Mathf.Pow(Mathf.Clamp01(fbm), 1.35f) * WinterLandscapeSpec.HillAmplitude;

                    // Gentle basin rim lift near the 350x350 outer edge so the world feels naturally framed
                    float edgeDist = Mathf.Max(Mathf.Abs(world.x), Mathf.Abs(world.y)) / WinterLandscapeSpec.HalfWorldSize;
                    float rimT = Mathf.SmoothStep(0.68f, 1.0f, edgeDist);
                    float rimLift = rimT * WinterLandscapeSpec.OuterRimLift;

                    // Subtle wind-carved snowdrifts (anisotropic ripples aligned with prevailing NW->SE wind)
                    float windU = (world.x * 0.85f + world.y * 0.52f) * 0.032f;
                    float windV = (-world.x * 0.52f + world.y * 0.85f) * 0.011f;
                    float drift = (Mathf.PerlinNoise(windU + 310f, windV + 720f) - 0.5f) * 2f * WinterLandscapeSpec.WindDriftAmplitude;

                    heights[gz, gx] = Mathf.Max(
                        WinterLandscapeSpec.OuterMinTerrainY,
                        WinterLandscapeSpec.OuterMinTerrainY + shaped + rimLift + drift);
                }
            }

            return heights;
        }

        /// <summary>
        /// Applies subtle hydraulic droplet erosion and returns an erosion-intensity map [0..1]
        /// so exposed rock/frozen earth splats are physically justified by erosion.
        /// </summary>
        public static float[,] ApplySubtleErosion(float[,] heights, int seed)
        {
            int res = heights.GetLength(0);
            var erosionMap = new float[res, res];
            var rng = new System.Random(seed ^ 0x51F15E);

            for (int d = 0; d < WinterLandscapeSpec.ErosionDroplets; d++)
            {
                float px = 2f + (float)rng.NextDouble() * (res - 5);
                float pz = 2f + (float)rng.NextDouble() * (res - 5);
                float dirX = 0f;
                float dirZ = 0f;
                float speed = 1f;
                float water = 1f;
                float sediment = 0f;

                for (int step = 0; step < WinterLandscapeSpec.ErosionMaxLifetime; step++)
                {
                    int ix = (int)px;
                    int iz = (int)pz;
                    if (ix < 1 || ix >= res - 2 || iz < 1 || iz >= res - 2) break;

                    float fx = px - ix;
                    float fz = pz - iz;

                    float h00 = heights[iz, ix];
                    float h10 = heights[iz, ix + 1];
                    float h01 = heights[iz + 1, ix];
                    float h11 = heights[iz + 1, ix + 1];

                    float gradX = (h10 - h00) * (1f - fz) + (h11 - h01) * fz;
                    float gradZ = (h01 - h00) * (1f - fx) + (h11 - h10) * fx;
                    float hOld = (h00 * (1f - fx) + h10 * fx) * (1f - fz)
                               + (h01 * (1f - fx) + h11 * fx) * fz;

                    dirX = dirX * WinterLandscapeSpec.ErosionInertia - gradX * (1f - WinterLandscapeSpec.ErosionInertia);
                    dirZ = dirZ * WinterLandscapeSpec.ErosionInertia - gradZ * (1f - WinterLandscapeSpec.ErosionInertia);
                    float len = Mathf.Sqrt(dirX * dirX + dirZ * dirZ);
                    if (len < 1e-5f) break;
                    dirX /= len;
                    dirZ /= len;

                    px += dirX;
                    pz += dirZ;
                    int nx = (int)px;
                    int nz = (int)pz;
                    if (nx < 1 || nx >= res - 2 || nz < 1 || nz >= res - 2) break;

                    float hNew = SampleHeightBilinear(heights, px, pz);
                    float deltaH = hNew - hOld;
                    float capacity = Mathf.Max(-deltaH * speed * water * 3.0f, 0.01f);

                    if (sediment > capacity || deltaH > 0f)
                    {
                        float deposit = deltaH > 0f
                            ? Mathf.Min(deltaH, sediment)
                            : (sediment - capacity) * WinterLandscapeSpec.DepositionStrength;
                        sediment -= deposit;
                        heights[iz, ix] += deposit * (1f - fx) * (1f - fz);
                        heights[iz, ix + 1] += deposit * fx * (1f - fz);
                        heights[iz + 1, ix] += deposit * (1f - fx) * fz;
                        heights[iz + 1, ix + 1] += deposit * fx * fz;
                    }
                    else
                    {
                        float erode = Mathf.Min((capacity - sediment) * WinterLandscapeSpec.ErosionStrength, -deltaH);
                        sediment += erode;
                        heights[iz, ix] -= erode * (1f - fx) * (1f - fz);
                        heights[iz, ix + 1] -= erode * fx * (1f - fz);
                        heights[iz + 1, ix] -= erode * (1f - fx) * fz;
                        heights[iz + 1, ix + 1] -= erode * fx * fz;
                        erosionMap[iz, ix] = Mathf.Clamp01(erosionMap[iz, ix] + erode * 2.5f);
                    }

                    speed = Mathf.Sqrt(Mathf.Max(0.01f, speed * speed - deltaH * 4f));
                    water *= (1f - WinterLandscapeSpec.EvaporationRate);
                    if (water < 0.05f) break;
                }
            }

            return erosionMap;
        }

        /// <summary>
        /// Levels the station playable area to StationBasinTerrainY (0.48m, just under SnowFloor's 0.50m)
        /// with a smooth cosine transition into the surrounding winter hills, and guarantees no sample
        /// outside StationBlendRadius dips below OuterMinTerrainY (0.54m) to prevent SnowFloor poke-through.
        /// </summary>
        public static void ApplyStationLeveler(float[,] heights, float[,] erosionMap)
        {
            int res = heights.GetLength(0);
            for (int gz = 0; gz < res; gz++)
            {
                for (int gx = 0; gx < res; gx++)
                {
                    Vector2 world = GridToWorldXZ(gx, gz, res);
                    float dist = Vector2.Distance(world, WinterLandscapeSpec.StationCenterXZ);
                    if (dist <= WinterLandscapeSpec.StationFlatRadius)
                    {
                        heights[gz, gx] = WinterLandscapeSpec.StationBasinTerrainY;
                        erosionMap[gz, gx] = 0f;
                    }
                    else if (dist < WinterLandscapeSpec.StationBlendRadius)
                    {
                        float t = (dist - WinterLandscapeSpec.StationFlatRadius)
                                / (WinterLandscapeSpec.StationBlendRadius - WinterLandscapeSpec.StationFlatRadius);
                        float smooth = t * t * (3f - 2f * t);
                        float outerH = Mathf.Max(heights[gz, gx], WinterLandscapeSpec.OuterMinTerrainY);
                        heights[gz, gx] = Mathf.Lerp(WinterLandscapeSpec.StationBasinTerrainY, outerH, smooth);
                        erosionMap[gz, gx] *= smooth;
                    }
                    else if (heights[gz, gx] < WinterLandscapeSpec.OuterMinTerrainY)
                    {
                        heights[gz, gx] = WinterLandscapeSpec.OuterMinTerrainY;
                    }
                }
            }
        }

        /// <summary>
        /// Routes a traversable winter trail between Anchor A and Anchor B using slope-aware A*,
        /// grades the corridor so max grade <= 10%, and returns the final waypoints and measured grade.
        /// </summary>
        public static TrailResult RouteAndGradeWinterTrail(float[,] heights, Vector2 anchorAXZ, Vector2 anchorBXZ)
        {
            int res = heights.GetLength(0);
            // Coarse A* grid (every 4th heightmap sample -> ~2.7m cells)
            const int stride = 4;
            int cRes = (res - 1) / stride + 1;
            float cellMeters = WinterLandscapeSpec.WorldSize / (cRes - 1);

            Vector2 gA = WorldXZToGrid(anchorAXZ, cRes);
            Vector2 gB = WorldXZToGrid(anchorBXZ, cRes);
            int startX = Mathf.RoundToInt(gA.x);
            int startZ = Mathf.RoundToInt(gA.y);
            int goalX = Mathf.RoundToInt(gB.x);
            int goalZ = Mathf.RoundToInt(gB.y);

            var rawPath = RunSlopeAwareAStar(heights, res, stride, cRes, cellMeters, startX, startZ, goalX, goalZ);

            // Convert to dense world-space waypoints (~2.0m spacing)
            var waypoints = ResampleAndSmoothPath(rawPath, heights, res, 2.0f);

            // Enforce max 10% grade along the path's target elevation profile
            float maxSlopeRatio = (WinterLandscapeSpec.MaxTrailGradePercent - 0.5f) / 100f; // 9.5% safety margin
            EnforceMaxGradeProfile(waypoints, maxSlopeRatio);

            // Carve/grade the trail corridor gently into the heightmap
            GradeCorridorIntoHeightmap(heights, waypoints);

            // Re-sample final heights on the graded terrain and measure actual max grade
            float maxGrade = 0f;
            float totalLen = 0f;
            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector3 wp = waypoints[i];
                wp.y = SampleWorldHeight(heights, wp.x, wp.z);
                waypoints[i] = wp;

                if (i > 0)
                {
                    Vector3 prev = waypoints[i - 1];
                    float dx = wp.x - prev.x;
                    float dz = wp.z - prev.z;
                    float horiz = Mathf.Sqrt(dx * dx + dz * dz);
                    if (horiz > 0.01f)
                    {
                        float gradePct = Mathf.Abs(wp.y - prev.y) / horiz * 100f;
                        if (gradePct > maxGrade) maxGrade = gradePct;
                        totalLen += horiz;
                    }
                }
            }

            return new TrailResult
            {
                WaypointsWorld = waypoints,
                MaxGradePercent = maxGrade,
                TotalLengthMeters = totalLen
            };
        }

        private static List<Vector2> RunSlopeAwareAStar(
            float[,] heights, int res, int stride, int cRes, float cellMeters,
            int sx, int sz, int gx, int gz)
        {
            int count = cRes * cRes;
            var gScore = new float[count];
            var fScore = new float[count];
            var cameFrom = new int[count];
            var closed = new bool[count];

            for (int i = 0; i < count; i++)
            {
                gScore[i] = float.PositiveInfinity;
                fScore[i] = float.PositiveInfinity;
                cameFrom[i] = -1;
            }

            int startIdx = sz * cRes + sx;
            int goalIdx = gz * cRes + gx;
            gScore[startIdx] = 0f;
            fScore[startIdx] = Vector2.Distance(new Vector2(sx, sz), new Vector2(gx, gz)) * cellMeters;

            var open = new List<int> { startIdx };
            int[] dx = { -1, 1, 0, 0, -1, 1, -1, 1 };
            int[] dz = { 0, 0, -1, 1, -1, -1, 1, 1 };

            while (open.Count > 0)
            {
                int bestPos = 0;
                float bestF = fScore[open[0]];
                for (int i = 1; i < open.Count; i++)
                {
                    float f = fScore[open[i]];
                    if (f < bestF)
                    {
                        bestF = f;
                        bestPos = i;
                    }
                }

                int curr = open[bestPos];
                open.RemoveAt(bestPos);
                if (curr == goalIdx) break;
                if (closed[curr]) continue;
                closed[curr] = true;

                int cx = curr % cRes;
                int cz = curr / cRes;
                float ch = heights[Mathf.Min(cz * stride, res - 1), Mathf.Min(cx * stride, res - 1)];

                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + dx[k];
                    int nz = cz + dz[k];
                    if (nx < 1 || nx >= cRes - 1 || nz < 1 || nz >= cRes - 1) continue;
                    int nIdx = nz * cRes + nx;
                    if (closed[nIdx]) continue;

                    float stepDist = (k < 4 ? 1f : 1.4142135f) * cellMeters;
                    float nh = heights[Mathf.Min(nz * stride, res - 1), Mathf.Min(nx * stride, res - 1)];
                    float grade = Mathf.Abs(nh - ch) / stepDist;

                    // Strongly penalize slopes approaching or exceeding 10%
                    float slopePenalty = 1f + grade * grade * 180f;
                    if (grade > 0.10f) slopePenalty += (grade - 0.10f) * 600f;

                    float tentativeG = gScore[curr] + stepDist * slopePenalty;
                    if (tentativeG < gScore[nIdx])
                    {
                        cameFrom[nIdx] = curr;
                        gScore[nIdx] = tentativeG;
                        fScore[nIdx] = tentativeG + Vector2.Distance(new Vector2(nx, nz), new Vector2(gx, gz)) * cellMeters;
                        if (!open.Contains(nIdx)) open.Add(nIdx);
                    }
                }
            }

            var path = new List<Vector2>();
            int cur = goalIdx;
            if (cameFrom[cur] == -1)
            {
                // Fallback straight line if unreachable
                path.Add(GridToWorldXZ(sx * stride, sz * stride, res));
                path.Add(GridToWorldXZ(gx * stride, gz * stride, res));
                return path;
            }

            while (cur != -1)
            {
                int px = cur % cRes;
                int pz = cur / cRes;
                path.Add(GridToWorldXZ(Mathf.Min(px * stride, res - 1), Mathf.Min(pz * stride, res - 1), res));
                cur = cameFrom[cur];
            }
            path.Reverse();
            return path;
        }

        private static List<Vector3> ResampleAndSmoothPath(List<Vector2> rawXZ, float[,] heights, int res, float spacing)
        {
            // Chaikin smoothing passes for natural winter trail curves
            var pts = new List<Vector2>(rawXZ);
            for (int pass = 0; pass < 3; pass++)
            {
                if (pts.Count < 3) break;
                var next = new List<Vector2> { pts[0] };
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    next.Add(Vector2.Lerp(pts[i], pts[i + 1], 0.25f));
                    next.Add(Vector2.Lerp(pts[i], pts[i + 1], 0.75f));
                }
                next.Add(pts[pts.Count - 1]);
                pts = next;
            }

            var dense = new List<Vector3>();
            if (pts.Count == 0) return dense;
            dense.Add(new Vector3(pts[0].x, SampleWorldHeight(heights, pts[0].x, pts[0].y), pts[0].y));
            float accum = 0f;

            for (int i = 1; i < pts.Count; i++)
            {
                Vector2 a = pts[i - 1];
                Vector2 b = pts[i];
                float seg = Vector2.Distance(a, b);
                if (seg < 1e-4f) continue;

                float pos = spacing - accum;
                while (pos <= seg)
                {
                    Vector2 p = Vector2.Lerp(a, b, pos / seg);
                    dense.Add(new Vector3(p.x, SampleWorldHeight(heights, p.x, p.y), p.y));
                    pos += spacing;
                }
                accum = seg - (pos - spacing);
            }

            Vector2 last = pts[pts.Count - 1];
            dense.Add(new Vector3(last.x, SampleWorldHeight(heights, last.x, last.y), last.y));
            return dense;
        }

        private static void EnforceMaxGradeProfile(List<Vector3> waypoints, float maxSlopeRatio)
        {
            if (waypoints.Count < 2) return;

            // Anchor A starts at SnowFloor level
            var first = waypoints[0];
            first.y = Mathf.Max(first.y, WinterLandscapeSpec.SnowFloorTopY);
            waypoints[0] = first;

            // Forward and backward relaxation passes guarantee |dy/ds| <= maxSlopeRatio everywhere
            for (int iter = 0; iter < 12; iter++)
            {
                for (int i = 1; i < waypoints.Count; i++)
                {
                    Vector3 a = waypoints[i - 1];
                    Vector3 b = waypoints[i];
                    float horiz = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                    float maxDy = horiz * maxSlopeRatio;
                    b.y = Mathf.Clamp(b.y, a.y - maxDy, a.y + maxDy);
                    waypoints[i] = b;
                }

                for (int i = waypoints.Count - 2; i >= 0; i--)
                {
                    Vector3 a = waypoints[i + 1];
                    Vector3 b = waypoints[i];
                    float horiz = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                    float maxDy = horiz * maxSlopeRatio;
                    if (i > 0)
                    {
                        b.y = Mathf.Clamp(b.y, a.y - maxDy, a.y + maxDy);
                        waypoints[i] = b;
                    }
                }
            }
        }

        private static void GradeCorridorIntoHeightmap(float[,] heights, List<Vector3> waypoints)
        {
            int res = heights.GetLength(0);
            float totalHalf = WinterLandscapeSpec.TrailHalfWidth + WinterLandscapeSpec.TrailShoulderWidth;

            for (int gz = 0; gz < res; gz++)
            {
                for (int gx = 0; gx < res; gx++)
                {
                    Vector2 world = GridToWorldXZ(gx, gz, res);
                    float stationDist = Vector2.Distance(world, WinterLandscapeSpec.StationCenterXZ);
                    // Keep the flat station basin strictly at StationBasinTerrainY
                    if (stationDist <= WinterLandscapeSpec.StationFlatRadius)
                    {
                        continue;
                    }

                    FindNearestWaypoint(world, waypoints, out float minDist, out float targetY);
                    if (minDist >= totalHalf) continue;

                    float graded;
                    if (minDist <= WinterLandscapeSpec.TrailHalfWidth)
                    {
                        graded = targetY;
                    }
                    else
                    {
                        float t = (minDist - WinterLandscapeSpec.TrailHalfWidth) / WinterLandscapeSpec.TrailShoulderWidth;
                        float smooth = t * t * (3f - 2f * t);
                        // Soft plowed-snow snowbank berm peaking along the trail shoulder
                        float berm = Mathf.Sin(t * Mathf.PI) * WinterLandscapeSpec.TrailSnowbankHeight;
                        graded = Mathf.Lerp(targetY, heights[gz, gx], smooth) + berm;
                    }

                    if (stationDist >= WinterLandscapeSpec.StationBlendRadius)
                    {
                        graded = Mathf.Max(graded, WinterLandscapeSpec.OuterMinTerrainY);
                    }
                    heights[gz, gx] = graded;
                }
            }
        }

        /// <summary>
        /// Builds the 4-layer winter splatmap (no grass!):
        ///   0: Primary Snow (dominant, matches SnowFloor)
        ///   1: Packed-Snow Winter Trail
        ///   2: Restrained Frozen Earth (only on eroded moderate slopes)
        ///   3: Restrained Exposed Rock (only on steeper eroded faces)
        /// Returns average coverage per layer [0..1].
        /// </summary>
        public static float[,,] BuildWinterAlphamaps(
            float[,] heights, float[,] erosionMap, List<Vector3> trailWaypoints, out float[] layerCoverage)
        {
            int aRes = WinterLandscapeSpec.AlphamapResolution;
            int hRes = heights.GetLength(0);
            var alpha = new float[aRes, aRes, 4];
            var sums = new double[4];

            float trailPaintHalf = WinterLandscapeSpec.TrailHalfWidth + 1.0f;

            for (int az = 0; az < aRes; az++)
            {
                for (int ax = 0; ax < aRes; ax++)
                {
                    Vector2 world = GridToWorldXZ(ax, az, aRes);
                    float slopeDeg = SampleWorldSlopeDeg(heights, world.x, world.y);

                    Vector2 hg = WorldXZToGrid(world, hRes);
                    float erosion = erosionMap[Mathf.RoundToInt(hg.y), Mathf.RoundToInt(hg.x)];

                    FindNearestWaypoint(world, trailWaypoints, out float trailDist, out _);
                    float wTrail = 0f;
                    if (trailDist < trailPaintHalf)
                    {
                        float t = Mathf.Clamp01(trailDist / trailPaintHalf);
                        wTrail = 1f - (t * t * (3f - 2f * t));
                    }

                    // Noise-modulated slope & erosion thresholds so rock/earth read organically
                    float organicNoise = Mathf.PerlinNoise((world.x + 512f) * 0.045f, (world.y + 512f) * 0.045f);
                    float fineNoise = Mathf.PerlinNoise((world.x + 128f) * 0.14f, (world.y + 128f) * 0.14f);
                    float slopeJitter = (organicNoise - 0.5f) * 5.0f + (fineNoise - 0.5f) * 2.0f;
                    float effSlope = slopeDeg + slopeJitter;

                    // Restrained exposed rock only on steeper eroded faces (> 23 deg)
                    float wRock = Mathf.SmoothStep(23f, 34f, effSlope) * Mathf.Clamp01(0.32f + erosion * 1.45f) * 0.72f;

                    // Restrained frozen earth only on moderate eroded slopes (14..26 deg)
                    float slopeMid = Mathf.SmoothStep(14f, 21f, effSlope) * (1f - Mathf.SmoothStep(24f, 32f, effSlope));
                    float wEarth = slopeMid * Mathf.Clamp01(0.22f + erosion * 1.25f) * (0.28f + 0.16f * organicNoise);

                    // Never paint earth/rock inside the flat station basin
                    if (Vector2.Distance(world, WinterLandscapeSpec.StationCenterXZ) < WinterLandscapeSpec.StationFlatRadius + 6f)
                    {
                        wRock = 0f;
                        wEarth = 0f;
                    }

                    // Suppress rock/earth on the packed-snow trail
                    wRock *= (1f - wTrail);
                    wEarth *= (1f - wTrail);

                    // Snow remains the dominant ground cover everywhere
                    float wSnow = Mathf.Max(0.15f, 1f - (wTrail + wRock + wEarth));
                    float total = wSnow + wTrail + wEarth + wRock;

                    alpha[az, ax, 0] = wSnow / total;
                    alpha[az, ax, 1] = wTrail / total;
                    alpha[az, ax, 2] = wEarth / total;
                    alpha[az, ax, 3] = wRock / total;

                    sums[0] += alpha[az, ax, 0];
                    sums[1] += alpha[az, ax, 1];
                    sums[2] += alpha[az, ax, 2];
                    sums[3] += alpha[az, ax, 3];
                }
            }

            double pixels = (double)aRes * aRes;
            layerCoverage = new float[4];
            for (int i = 0; i < 4; i++) layerCoverage[i] = (float)(sums[i] / pixels);
            return alpha;
        }

        /// <summary>
        /// Projects worldXZ onto every trail segment (waypoints[i], waypoints[i+1]) and returns
        /// the exact shortest horizontal distance and linearly interpolated elevation (zero stair-stepping).
        /// </summary>
        public static void FindNearestWaypoint(Vector2 worldXZ, List<Vector3> waypoints, out float minDist, out float nearestY)
        {
            minDist = float.PositiveInfinity;
            nearestY = WinterLandscapeSpec.SnowFloorTopY;
            if (waypoints == null || waypoints.Count == 0) return;

            if (waypoints.Count == 1)
            {
                Vector3 wp = waypoints[0];
                minDist = Vector2.Distance(worldXZ, new Vector2(wp.x, wp.z));
                nearestY = wp.y;
                return;
            }

            float minDistSq = float.PositiveInfinity;
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Vector3 a = waypoints[i];
                Vector3 b = waypoints[i + 1];
                float abX = b.x - a.x;
                float abZ = b.z - a.z;
                float abLenSq = abX * abX + abZ * abZ;

                float t = 0f;
                if (abLenSq > 1e-6f)
                {
                    t = Mathf.Clamp01(((worldXZ.x - a.x) * abX + (worldXZ.y - a.z) * abZ) / abLenSq);
                }

                float projX = a.x + abX * t;
                float projZ = a.z + abZ * t;
                float dx = worldXZ.x - projX;
                float dz = worldXZ.y - projZ;
                float d2 = dx * dx + dz * dz;
                if (d2 < minDistSq)
                {
                    minDistSq = d2;
                    nearestY = Mathf.Lerp(a.y, b.y, t);
                }
            }

            minDist = Mathf.Sqrt(minDistSq);
        }

        public static float SampleWorldHeight(float[,] heights, float worldX, float worldZ)
        {
            int res = heights.GetLength(0);
            Vector2 g = WorldXZToGrid(new Vector2(worldX, worldZ), res);
            return SampleHeightBilinear(heights, g.x, g.y);
        }

        public static float SampleWorldSlopeDeg(float[,] heights, float worldX, float worldZ)
        {
            const float eps = 1.0f;
            float hL = SampleWorldHeight(heights, worldX - eps, worldZ);
            float hR = SampleWorldHeight(heights, worldX + eps, worldZ);
            float hD = SampleWorldHeight(heights, worldX, worldZ - eps);
            float hU = SampleWorldHeight(heights, worldX, worldZ + eps);
            float dx = (hR - hL) / (2f * eps);
            float dz = (hU - hD) / (2f * eps);
            float grad = Mathf.Sqrt(dx * dx + dz * dz);
            return Mathf.Atan(grad) * Mathf.Rad2Deg;
        }

        private static float SampleHeightBilinear(float[,] heights, float gx, float gz)
        {
            int res = heights.GetLength(0);
            int ix = Mathf.Clamp((int)gx, 0, res - 2);
            int iz = Mathf.Clamp((int)gz, 0, res - 2);
            float fx = Mathf.Clamp01(gx - ix);
            float fz = Mathf.Clamp01(gz - iz);

            float h00 = heights[iz, ix];
            float h10 = heights[iz, ix + 1];
            float h01 = heights[iz + 1, ix];
            float h11 = heights[iz + 1, ix + 1];

            return (h00 * (1f - fx) + h10 * fx) * (1f - fz)
                 + (h01 * (1f - fx) + h11 * fx) * fz;
        }
    }
}
#endif
