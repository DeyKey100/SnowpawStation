using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Вращение колёс машины по пройденному пути. У моделей из Assets/NpcCars колёса —
/// отдельные меши на осях, кроме фургона: у него все четыре колеса запечены в один меш,
/// поэтому он один раз режется на четыре узла по вершинам.
///
/// Машина едет по земле без физики, поэтому угол поворота колеса считается вручную:
/// путь / радиус, вокруг поперечной оси машины (её локальный right).
/// </summary>
[DisallowMultipleComponent]
public sealed class CarWheelSpin : MonoBehaviour
{
    private const float RadiansToDegrees = 57.29578f;
    private const float DefaultWheelRadius = 0.35f;

    /// <summary>Нарезанные колёса фургона: строятся один раз на модель и переиспользуются.</summary>
    private sealed class WheelSlice
    {
        public Mesh mesh;
        public Vector3 pivot;
        public Material[] materials;
        public string name;
    }

    private static readonly Dictionary<string, WheelSlice[]> SplitCache = new Dictionary<string, WheelSlice[]>();

    private Transform body;
    private Transform[] wheels;
    private float radius = DefaultWheelRadius;

    /// <summary>Сколько колёс удалось найти (0 — модель без отдельных колёс).</summary>
    public int WheelCount => wheels != null ? wheels.Length : 0;

    /// <summary>Радиус колеса, метры (по нему считается угол поворота).</summary>
    public float WheelRadius => radius;

    /// <summary>
    /// Найденные колёса. Нужны снаружи: раскачка кузова переносит под свой узел
    /// только корпус, а колёса обязаны остаться прямыми на дороге.
    /// </summary>
    public Transform[] Wheels => wheels;

    /// <summary>Повесить вращение на корень машины и найти у неё колёса.</summary>
    public static CarWheelSpin Attach(Transform carRoot, string assetPath)
    {
        if (carRoot == null)
        {
            return null;
        }

        CarWheelSpin spin = carRoot.GetComponent<CarWheelSpin>();
        if (spin == null)
        {
            spin = carRoot.gameObject.AddComponent<CarWheelSpin>();
        }

        spin.Build(carRoot, assetPath);
        return spin;
    }

    /// <summary>Провернуть колёса на пройденный путь: вперёд — плюс, назад — минус.</summary>
    public void Advance(float distance)
    {
        if (wheels == null || wheels.Length == 0 || radius <= 0.001f)
        {
            return;
        }

        if (Mathf.Abs(distance) < 0.00001f)
        {
            return;
        }

        float degrees = distance / radius * RadiansToDegrees;
        Vector3 axle = body != null ? body.right : transform.right;
        for (int i = 0; i < wheels.Length; i++)
        {
            Transform wheel = wheels[i];
            if (wheel != null)
            {
                wheel.Rotate(axle, degrees, Space.World);
            }
        }
    }

    private void Build(Transform carRoot, string assetPath)
    {
        body = carRoot;
        List<Transform> found = new List<Transform>();
        Transform[] all = carRoot.GetComponentsInChildren<Transform>(true);

        string[] names = WheelNames(assetPath);
        if (names != null)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform wheel = FindByName(all, names[i]);
                if (wheel != null)
                {
                    found.Add(wheel);
                }
            }
        }

        // Фургон: все колёса в одном меше — вместо него ставим четыре узла.
        if (found.Count == 0)
        {
            string single = SingleMeshWheels(assetPath);
            if (single != null)
            {
                Transform holder = FindByName(all, single);
                if (holder != null)
                {
                    found.AddRange(SpawnSlicedWheels(carRoot, holder, assetPath));
                }
            }
        }

        wheels = found.ToArray();
        radius = wheels.Length > 0 ? MeasureRadius(wheels[0]) : DefaultWheelRadius;
    }

    private static Transform FindByName(Transform[] all, string name)
    {
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == name)
            {
                return all[i];
            }
        }

        return null;
    }

    private static float MeasureRadius(Transform wheel)
    {
        MeshFilter filter = wheel.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            return DefaultWheelRadius;
        }

        // У колеса самый большой локальный габарит — это диаметр: остальные два
        // приходятся на ось и толщину шины. Домножаем на масштаб узла в модели.
        Vector3 size = filter.sharedMesh.bounds.size;
        float diameter = Mathf.Max(Mathf.Abs(size.x), Mathf.Max(Mathf.Abs(size.y), Mathf.Abs(size.z)));
        if (diameter <= 0.001f)
        {
            return DefaultWheelRadius;
        }

        Vector3 scale = wheel.lossyScale;
        float scaleMax = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        if (scaleMax <= 0.0001f)
        {
            scaleMax = 1f;
        }

        return diameter * 0.5f * scaleMax;
    }

    /// <summary>Поставить четыре колеса вместо меша, в котором они запечены вместе.</summary>
    private static Transform[] SpawnSlicedWheels(Transform carRoot, Transform holder, string assetPath)
    {
        List<Transform> result = new List<Transform>();
        WheelSlice[] slices = GetSlices(assetPath, carRoot, holder);
        if (slices == null || slices.Length == 0)
        {
            return result.ToArray();
        }

        MeshRenderer source = holder.GetComponent<MeshRenderer>();
        if (source != null)
        {
            source.enabled = false;
        }

        for (int i = 0; i < slices.Length; i++)
        {
            WheelSlice slice = slices[i];
            GameObject go = new GameObject(slice.name);
            go.transform.SetParent(carRoot, false);
            go.transform.position = carRoot.TransformPoint(slice.pivot);
            go.transform.rotation = carRoot.rotation;
            go.transform.localScale = Vector3.one;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = slice.mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = slice.materials;
            result.Add(go.transform);
        }

        return result.ToArray();
    }

    private static WheelSlice[] GetSlices(string assetPath, Transform carRoot, Transform holder)
    {
        if (SplitCache.TryGetValue(assetPath, out WheelSlice[] cached))
        {
            return cached;
        }

        WheelSlice[] built = SliceMesh(carRoot, holder);
        SplitCache[assetPath] = built;
        return built;
    }

    /// <summary>
    /// Разрезать меш со всеми колёсами на четыре части. Пивот каждой части — в её центре,
    /// поэтому дальше колесо крутится вокруг собственной оси, а не вокруг машины.
    /// </summary>
    private static WheelSlice[] SliceMesh(Transform carRoot, Transform holder)
    {
        List<WheelSlice> result = new List<WheelSlice>();
        MeshFilter filter = holder.GetComponent<MeshFilter>();
        MeshRenderer renderer = holder.GetComponent<MeshRenderer>();
        Mesh source = filter != null ? filter.sharedMesh : null;
        if (source == null || renderer == null || !source.isReadable)
        {
            return result.ToArray();
        }

        Vector3[] sourceVertices = source.vertices;
        if (sourceVertices.Length == 0)
        {
            return result.ToArray();
        }

        Matrix4x4 toRoot = carRoot.worldToLocalMatrix * holder.localToWorldMatrix;
        Vector3[] place = new Vector3[sourceVertices.Length];
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < sourceVertices.Length; i++)
        {
            Vector3 point = toRoot.MultiplyPoint3x4(sourceVertices[i]);
            place[i] = point;
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        Vector3 center = (min + max) * 0.5f;
        int subMeshCount = source.subMeshCount;

        // Четыре стороны машины: 0 — лево-зад, 1 — право-зад, 2 — лево-перед, 3 — право-перед.
        List<List<List<int>>> buckets = new List<List<List<int>>>(4);
        for (int bucket = 0; bucket < 4; bucket++)
        {
            List<List<int>> perSubMesh = new List<List<int>>(subMeshCount);
            for (int i = 0; i < subMeshCount; i++)
            {
                perSubMesh.Add(new List<int>());
            }

            buckets.Add(perSubMesh);
        }

        for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
        {
            int[] triangles = source.GetTriangles(subMesh);
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int ia = triangles[t];
                int ib = triangles[t + 1];
                int ic = triangles[t + 2];
                Vector3 middle = (place[ia] + place[ib] + place[ic]) / 3f;
                int bucket = (middle.x >= center.x ? 1 : 0) + (middle.z >= center.z ? 2 : 0);
                List<int> target = buckets[bucket][subMesh];
                target.Add(ia);
                target.Add(ib);
                target.Add(ic);
            }
        }

        Material[] materials = renderer.sharedMaterials;
        Vector3[] sourceNormals = source.normals;
        Vector2[] sourceUv = source.uv;

        for (int bucket = 0; bucket < buckets.Count; bucket++)
        {
            List<List<int>> perSubMesh = buckets[bucket];
            if (!HasTriangles(perSubMesh))
            {
                continue;
            }

            WheelSlice slice = BuildSlice(holder.name + "_Wheel" + bucket, perSubMesh, place,
                sourceNormals, sourceUv, materials, subMeshCount);
            if (slice != null)
            {
                result.Add(slice);
            }
        }

        return result.ToArray();
    }

    private static bool HasTriangles(List<List<int>> perSubMesh)
    {
        for (int i = 0; i < perSubMesh.Count; i++)
        {
            if (perSubMesh[i].Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static WheelSlice BuildSlice(string name, List<List<int>> perSubMesh, Vector3[] place,
        Vector3[] sourceNormals, Vector2[] sourceUv, Material[] materials, int subMeshCount)
    {
        List<int> indices = new List<int>();
        for (int i = 0; i < perSubMesh.Count; i++)
        {
            indices.AddRange(perSubMesh[i]);
        }

        if (indices.Count == 0)
        {
            return null;
        }

        Dictionary<int, int> remap = new Dictionary<int, int>(indices.Count);
        List<int> unique = new List<int>(indices.Count);
        for (int i = 0; i < indices.Count; i++)
        {
            int sourceIndex = indices[i];
            if (!remap.ContainsKey(sourceIndex))
            {
                remap.Add(sourceIndex, unique.Count);
                unique.Add(sourceIndex);
            }
        }

        Vector3[] points = new Vector3[unique.Count];
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < unique.Count; i++)
        {
            points[i] = place[unique[i]];
            sum += points[i];
        }

        Vector3 pivot = sum / unique.Count;
        for (int i = 0; i < points.Length; i++)
        {
            points[i] -= pivot;
        }

        Mesh mesh = new Mesh { name = name };
        if (points.Length > 65000)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.vertices = points;

        if (sourceNormals.Length > 0)
        {
            Vector3[] normals = new Vector3[unique.Count];
            for (int i = 0; i < unique.Count; i++)
            {
                normals[i] = sourceNormals[unique[i]];
            }

            mesh.normals = normals;
        }

        if (sourceUv.Length > 0)
        {
            Vector2[] uvs = new Vector2[unique.Count];
            for (int i = 0; i < unique.Count; i++)
            {
                uvs[i] = sourceUv[unique[i]];
            }

            mesh.uv = uvs;
        }

        mesh.subMeshCount = subMeshCount;
        for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
        {
            List<int> triangles = perSubMesh[subMesh];
            int[] remapped = new int[triangles.Count];
            for (int i = 0; i < triangles.Count; i++)
            {
                remapped[i] = remap[triangles[i]];
            }

            mesh.SetTriangles(remapped, subMesh, false);
        }

        mesh.RecalculateBounds();

        return new WheelSlice { mesh = mesh, pivot = pivot, materials = materials, name = name };
    }

    /// <summary>Отдельные меши колёс по модели (null — колёса лежат одним мешем).</summary>
    private static string[] WheelNames(string assetPath)
    {
        switch (ModelKey(assetPath))
        {
            case "hatchback": return new[] { "Mesh_31", "Mesh_35", "Mesh_39", "Mesh_44" };
            case "jeep": return new[] { "Mesh_47", "Mesh_52", "Mesh_55", "Mesh_59" };
            case "pickup": return new[] { "Mesh_45", "Mesh_49", "Mesh_53", "Mesh_58" };
            case "truck1": return new[] { "Mesh_34", "Mesh_38", "Mesh_42", "Mesh_46" };
            default: return null;
        }
    }

    /// <summary>Меш, в котором запечены все колёса сразу (фургон).</summary>
    private static string SingleMeshWheels(string assetPath)
    {
        return ModelKey(assetPath) == "van-pochta" ? "Mesh_30" : null;
    }

    private static string ModelKey(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return string.Empty;
        }

        string directory = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(assetPath));
        return directory != null ? directory.ToLowerInvariant() : string.Empty;
    }
}
