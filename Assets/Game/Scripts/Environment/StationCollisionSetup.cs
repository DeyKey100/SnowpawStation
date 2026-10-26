using UnityEngine;

public class StationCollisionSetup : MonoBehaviour
{
    [SerializeField] private bool includeChildren = true;

    private void Awake()
    {
        MeshFilter[] meshFilters = includeChildren
            ? GetComponentsInChildren<MeshFilter>(true)
            : GetComponents<MeshFilter>();

        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"У объекта {meshFilter.name} нет Mesh для Collider.", meshFilter);
                continue;
            }

            if (meshFilter.GetComponent<Collider>() != null)
            {
                continue;
            }

            MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
        }
    }
}
