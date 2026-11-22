using UnityEngine;

/// <summary>
/// Ссылки на модели машин-посетителей, чтобы они попадали в собранную игру.
///
/// Ассет лежит в Resources и держит прямые ссылки на fbx. Игра грузит только
/// каталог, а модели приезжают вместе с ним: Assets/NpcCars сам по себе в билд
/// не попадает, поэтому без каталога в билде оставалась одна заглушка.
/// </summary>
public sealed class NpcCarCatalog : ScriptableObject
{
    [System.Serializable]
    public sealed class Entry
    {
        [Tooltip("Путь к fbx — по нему каталог ищется из кода.")]
        public string path;

        [Tooltip("Сама модель.")]
        public GameObject prefab;
    }

    [Tooltip("Собранные модели. Заполняется через Tools > Snowpaw > Собрать каталог машин.")]
    public Entry[] entries = new Entry[0];

    public GameObject Find(string path)
    {
        if (string.IsNullOrEmpty(path) || entries == null)
        {
            return null;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].path == path)
            {
                return entries[i].prefab;
            }
        }

        return null;
    }
}
