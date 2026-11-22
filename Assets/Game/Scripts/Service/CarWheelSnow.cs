using UnityEngine;

/// <summary>
/// Снег из-под колёс идущей машины: редкие мелкие крупинки, вылетающие от точки
/// контакта колеса с дорогой назад и вверх. Эффект намеренно тихий — его задача
/// обозначить, что машина едет по снегу, а не устроить метель.
///
/// Частицы считаются по пройденному пути: стоящая машина не сыплет вообще,
/// медленная — редко, на крейсерской скорости — плотнее.
/// </summary>
[DisallowMultipleComponent]
public sealed class CarWheelSnow : MonoBehaviour
{
    private const string ObjectName = "WheelSnow";
    private const float ParticlesPerMeter = 11f;
    private const float MinSpeed = 0.5f;
    private const int MaxParticlesPerFrame = 6;
    private const int MaxParticles = 220;

    private static Material sharedMaterial;

    private ParticleSystem system;
    private Transform[] wheels;
    private float[] wheelRadii;
    private float pending;
    private int nextWheel;

    /// <summary>Повесить снег на корень машины и взять колёса у найденного вращателя.</summary>
    public static CarWheelSnow Attach(Transform carRoot, CarWheelSpin wheels)
    {
        if (carRoot == null || wheels == null || wheels.Wheels == null || wheels.Wheels.Length == 0)
        {
            return null;
        }

        CarWheelSnow snow = carRoot.GetComponent<CarWheelSnow>();
        if (snow == null)
        {
            snow = carRoot.gameObject.AddComponent<CarWheelSnow>();
        }

        snow.Build(carRoot, wheels);
        return snow;
    }

    /// <summary>
    /// Добавить частицы за прошедший кадр. deltaDistance — сколько машина реально
    /// проехала (со знаком), worldVelocity — её скорость в мире для направления вылета.
    /// </summary>
    public void Advance(float deltaDistance, Vector3 worldVelocity)
    {
        if (system == null || wheels == null || wheels.Length == 0)
        {
            return;
        }

        float speed = worldVelocity.magnitude;
        if (speed < MinSpeed || Mathf.Abs(deltaDistance) < 0.0001f)
        {
            pending = 0f;
            return;
        }

        // Плотность растёт со скоростью: на ползущем ходу почти ничего, на ходу заметно.
        float density = Mathf.Clamp01(speed / 4f);
        pending += Mathf.Abs(deltaDistance) * ParticlesPerMeter * density;
        int count = Mathf.Min((int)pending, MaxParticlesPerFrame);
        if (count <= 0)
        {
            return;
        }

        pending -= count;

        Vector3 backwards = speed > 0.001f ? -worldVelocity / speed : Vector3.back;
        for (int i = 0; i < count; i++)
        {
            int index = nextWheel % wheels.Length;
            nextWheel++;

            Transform wheel = wheels[index];
            if (wheel == null)
            {
                continue;
            }

            float radius = wheelRadii[index];
            Vector3 contact = wheel.position - Vector3.up * (radius * 0.92f);
            contact += Random.insideUnitSphere * (radius * 0.35f);

            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = contact,
                velocity = backwards * Random.Range(0.15f, 0.9f)
                    + Vector3.up * Random.Range(0.25f, 0.75f)
                    + Random.insideUnitSphere * 0.25f,
                startSize = Random.Range(0.045f, 0.1f),
                startLifetime = Random.Range(0.3f, 0.7f),
                startColor = new Color(1f, 1f, 1f, Random.Range(0.5f, 0.85f)),
            };

            system.Emit(emit, 1);
        }
    }

    private void Build(Transform carRoot, CarWheelSpin spin)
    {
        wheels = spin.Wheels;
        wheelRadii = new float[wheels.Length];
        for (int i = 0; i < wheels.Length; i++)
        {
            wheelRadii[i] = spin.WheelRadius;
        }

        Transform existing = carRoot.Find(ObjectName);
        if (existing != null)
        {
            system = existing.GetComponent<ParticleSystem>();
        }

        if (system == null)
        {
            GameObject holder = new GameObject(ObjectName);
            holder.transform.SetParent(carRoot, false);
            system = holder.AddComponent<ParticleSystem>();
        }

        Configure(system);
    }

    private static void Configure(ParticleSystem ps)
    {
        // Машина едет сама по своему маршруту, поэтому свет и тени частицам не нужны —
        // это самый дешёвый вариант, который ещё видно.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.1f);
        main.startColor = new Color(1f, 1f, 1f, 0.8f);
        main.gravityModifier = 0.45f;
        main.maxParticles = MaxParticles;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
        fade.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.8f, 0.15f),
                new GradientAlphaKey(0f, 1f),
            });
        fade.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule shrink = ps.sizeOverLifetime;
        shrink.enabled = true;
        shrink.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f),
            new Keyframe(0.25f, 1f),
            new Keyframe(1f, 0.5f)));

        ParticleSystemRenderer particleRenderer = ps.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            particleRenderer.sharedMaterial = ResolveMaterial();
        }

        ps.Play();
    }

    private static Material ResolveMaterial()
    {
        if (sharedMaterial != null)
        {
            return sharedMaterial;
        }

        // Порядок важен: в URP берём партикловый шейдер, но если его нет — обычные спрайты.
        string[] candidates =
        {
            "Universal Render Pipeline/Particles/Unlit",
            "Sprites/Default",
            "Particles/Standard Unlit",
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            Shader shader = Shader.Find(candidates[i]);
            if (shader == null)
            {
                continue;
            }

            Material material = new Material(shader)
            {
                name = "WheelSnow (runtime)",
                hideFlags = HideFlags.DontSave,
            };
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", Texture2D.whiteTexture);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            sharedMaterial = material;
            return sharedMaterial;
        }

        return null;
    }
}
