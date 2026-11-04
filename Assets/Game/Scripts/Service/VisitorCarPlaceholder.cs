using UnityEngine;

/// <summary>
/// Заглушка посетителя: вытянутый куб на месте будущей машины.
/// Появляется у той колонки, к которой встал посетитель, и исчезает сразу после
/// заправки. Коллайдера нет намеренно: это визуальная заглушка, она не должна
/// толкать игрока и мешать ему подойти к колонке.
/// </summary>
[DisallowMultipleComponent]
public sealed class VisitorCarPlaceholder : MonoBehaviour
{
    // Кузов: длина вдоль Z (машина стоит боком к островку колонок).
    private const float BodyLength = 4.40f;
    private const float BodyWidth = 1.85f;
    private const float BodyHeight = 1.45f;

    /// <summary>Насколько машина стоит в стороне от колонки, метры.</summary>
    private const float SideOffset = 2.00f;

    /// <summary>Верх снежного пола: на нём стоят и колонки, и машина.</summary>
    private const float GroundY = 0.50f;

    private static readonly Color BodyColor = new Color(0.30f, 0.34f, 0.40f);

    private Material bodyMaterial;

    /// <summary>
    /// Поставить заглушку у колонки. side — в какую сторону по X от колонки
    /// ставить машину (−1 налево, +1 направо), чтобы она не встала на проезде.
    /// </summary>
    public static VisitorCarPlaceholder Spawn(Vector3 pumpPosition, float side)
    {
        GameObject car = GameObject.CreatePrimitive(PrimitiveType.Cube);
        car.name = "VisitorCar";
        car.transform.SetPositionAndRotation(
            new Vector3(pumpPosition.x + side * SideOffset, GroundY + BodyHeight * 0.5f, pumpPosition.z),
            Quaternion.identity);

        // Коллайдер не нужен: заглушка не должна толкать игрока и мешать подойти.
        Collider collider = car.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }

        VisitorCarPlaceholder placeholder = car.AddComponent<VisitorCarPlaceholder>();
        placeholder.BuildBody(car.GetComponent<MeshRenderer>());
        return placeholder;
    }

    private void BuildBody(MeshRenderer renderer)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
        {
            bodyMaterial = new Material(shader);
            if (bodyMaterial.HasProperty("_BaseColor"))
            {
                bodyMaterial.SetColor("_BaseColor", BodyColor);
            }

            if (bodyMaterial.HasProperty("_Color"))
            {
                bodyMaterial.SetColor("_Color", BodyColor);
            }
        }

        if (renderer != null && bodyMaterial != null)
        {
            renderer.sharedMaterial = bodyMaterial;
        }

        // Вытянутый куб: длина по Z, ширина по X.
        transform.localScale = new Vector3(BodyWidth, BodyHeight, BodyLength);
    }

    private void OnDestroy()
    {
        if (bodyMaterial != null)
        {
            Destroy(bodyMaterial);
            bodyMaterial = null;
        }
    }
}
