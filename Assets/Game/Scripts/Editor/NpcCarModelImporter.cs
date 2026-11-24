#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Модели посетителей из Assets/NpcCars импортируются с включённым Read/Write:
/// без него нельзя прочитать вершины, а у фургона все четыре колеса запечены
/// в один меш — их приходится резать на отдельные узлы, чтобы колесо крутилось
/// вокруг своей оси, а не вокруг машины.
/// </summary>
public sealed class NpcCarModelImporter : AssetPostprocessor
{
    private const string NpcCarsRoot = "/NpcCars/";

    private void OnPreprocessModel()
    {
        if (!assetPath.Replace('\\', '/').Contains(NpcCarsRoot))
        {
            return;
        }

        if (assetImporter is ModelImporter importer)
        {
            importer.isReadable = true;
        }
    }
}
#endif
