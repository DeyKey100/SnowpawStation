#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;

public sealed class FixedCornerCameraTests
{
    private GameObject host;
    private FixedCornerCamera camera;
    private readonly Vector3 anchor = new Vector3(-5f, 7f, 5f);

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("FixedCornerCamera_Test");
        host.transform.SetPositionAndRotation(anchor, Quaternion.Euler(48f, 135f, 0f));
        camera = host.AddComponent<FixedCornerCamera>();
        InvokeLifecycle("OnEnable");
    }

    // SendMessage invokes Unity's play-mode guard in EditMode; invoke the managed method directly.
    private void InvokeLifecycle(string method) => typeof(FixedCornerCamera)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(camera, null);

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(host);

    [TestCase(100f, 100f, 6f, 4f)]
    [TestCase(-100f, -100f, -6f, -4f)]
    [TestCase(2f, -1f, 2f, -1f)]
    public void LookOffset_StaysInsideCorridor(float x, float y, float expectedX, float expectedY)
    {
        Assert.That(FixedCornerCamera.ClampLookOffset(new Vector2(x, y), 6f, 4f),
            Is.EqualTo(new Vector2(expectedX, expectedY)));
    }

    [Test]
    public void NegativeLimits_DisableBothAxes()
    {
        Assert.That(FixedCornerCamera.ClampLookOffset(Vector2.one, -1f, -2f), Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void MouseDelta_DoesNotDependOnFrameDuration()
    {
        camera.ApplyLookInput(Vector2.one, true, 0.01f);
        Vector2 first = camera.LookOffset;
        camera.ResetLook();
        camera.ApplyLookInput(Vector2.one, true, 0.1f);
        Assert.That(camera.LookOffset, Is.EqualTo(first));
    }

    [Test]
    public void StickInput_IsFrameRateIndependent()
    {
        camera.ApplyLookInput(Vector2.one, false, 0.08f);
        Vector2 first = camera.LookOffset;
        camera.ResetLook();
        camera.ApplyLookInput(Vector2.one, false, 0.04f);
        camera.ApplyLookInput(Vector2.one, false, 0.04f);
        Assert.That(Vector2.Distance(camera.LookOffset, first), Is.LessThan(0.0001f));
    }

    [Test]
    public void LateUpdate_RestoresFixedPositionAndRemovesRoll()
    {
        host.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(10f, 20f, 40f));
        InvokeLifecycle("LateUpdate");
        Assert.That(Vector3.Distance(host.transform.position, anchor), Is.LessThan(0.0001f));
        Assert.That(Quaternion.Angle(host.transform.rotation, Quaternion.Euler(48f, 135f, 0f)), Is.LessThan(0.01f));
    }

    [Test]
    public void ResetLook_ClearsOffsetWithoutMovingAnchor()
    {
        camera.ApplyLookInput(new Vector2(1000f, -1000f), true, 1f);
        camera.ResetLook();
        Assert.That(camera.LookOffset, Is.EqualTo(Vector2.zero));
        Assert.That(host.transform.position, Is.EqualTo(anchor));
    }

    [TestCase(48f)]
    [TestCase(85f)]
    [TestCase(90f)]
    public void CameraRelativeMovement_IsHorizontalAndNormalized(float pitch)
    {
        Vector3 direction = FirstPersonController.GetCameraRelativeDirection(
            Quaternion.Euler(pitch, 135f, 0f), Vector2.one);
        Assert.That(direction.y, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void ForwardAndRight_MatchScreenAxes()
    {
        Quaternion rotation = Quaternion.Euler(48f, 135f, 0f);
        Vector3 forward = FirstPersonController.GetCameraRelativeDirection(rotation, Vector2.up);
        Vector3 right = FirstPersonController.GetCameraRelativeDirection(rotation, Vector2.right);
        Assert.That(Vector3.Dot(forward, new Vector3(1f, 0f, -1f).normalized), Is.GreaterThan(0.999f));
        Assert.That(Vector3.Dot(right, new Vector3(-1f, 0f, -1f).normalized), Is.GreaterThan(0.999f));
        Assert.That(Vector3.Dot(forward, right), Is.EqualTo(0f).Within(0.0001f));
    }
}
#endif
