using System.Collections;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && INPUT_SYSTEM_PACKAGE
using UnityEngine.InputSystem;
#endif

namespace HeyHeyThere.PSXGasStationKitFree
{
    /// <summary>
    /// The demo scene's showcase: the camera circles the props and every animated prop plays its
    /// animations in turn (a creature's through its Animator). Remove it to use the scene as a plain layout.
    /// </summary>
    public class PropsDemo : MonoBehaviour
    {
        public Transform cameraRig;
        public float degreesPerSecond = 8f;
        public float pause = 1.5f;
        public string noun = "prop";  // what the demo shows one of

        Camera view;
        Vector3 origin;
        Quaternion rotation;
        float distance, pitch, initialPitch, zoom = 1f, fieldOfView;
        Vector2 pointer;
        bool dragging, automatic = true;
        Transform[] subjects;
        Vector3 overviewOrigin;
        Quaternion overviewRotation;
        float overviewDistance, overviewPitch, overviewNear;
        int index;

        void Start()
        {
            if (cameraRig != null)
            {
                view = cameraRig.GetComponentInChildren<Camera>();
                origin = cameraRig.position;
                rotation = cameraRig.rotation;
                if (view != null)
                {
                    distance = Mathf.Max(0.05f, view.transform.localPosition.magnitude);
                    pitch = initialPitch = Mathf.Asin(view.transform.localPosition.y / distance) * Mathf.Rad2Deg;
                    fieldOfView = view.fieldOfView;
                    overviewOrigin = origin;
                    overviewRotation = rotation;
                    overviewDistance = distance;
                    overviewPitch = pitch;
                    overviewNear = view.nearClipPlane;
                    var props = GameObject.Find("Props");
                    subjects = props != null ? props.transform.Cast<Transform>().ToArray() : new Transform[0];
                    Show(subjects.Length > 0 ? 0 : -1);
                }
            }
            foreach (var anim in FindObjectsByType<Animation>(FindObjectsSortMode.None))
                StartCoroutine(Play(anim));
            foreach (var animator in FindObjectsByType<Animator>(FindObjectsSortMode.None))
                if (animator.runtimeAnimatorController != null)
                    StartCoroutine(Play(animator));
        }

        void Update()
        {
            if (cameraRig == null || view == null)
                return;
            Vector2 at;
            bool orbit, pan, reset, toggle, previous, next, all;
            float scroll;
#if ENABLE_INPUT_SYSTEM && INPUT_SYSTEM_PACKAGE
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            at = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            orbit = mouse != null && mouse.leftButton.isPressed;
            pan = mouse != null && (mouse.rightButton.isPressed || mouse.middleButton.isPressed);
            scroll = mouse != null ? mouse.scroll.y.ReadValue() / 120f : 0f;
            reset = keyboard != null && keyboard.rKey.wasPressedThisFrame;
            toggle = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
            previous = keyboard != null && keyboard.leftArrowKey.wasPressedThisFrame;
            next = keyboard != null && keyboard.rightArrowKey.wasPressedThisFrame;
            all = keyboard != null && keyboard.aKey.wasPressedThisFrame;
#else
            at = Input.mousePosition;
            orbit = Input.GetMouseButton(0);
            pan = Input.GetMouseButton(1) || Input.GetMouseButton(2);
            scroll = Input.mouseScrollDelta.y;
            reset = Input.GetKeyDown(KeyCode.R);
            toggle = Input.GetKeyDown(KeyCode.Space);
            previous = Input.GetKeyDown(KeyCode.LeftArrow);
            next = Input.GetKeyDown(KeyCode.RightArrow);
            all = Input.GetKeyDown(KeyCode.A);
#endif
            if (all) Show(-1);
            else if (next && subjects.Length > 0) Show(index < 0 ? 0 : (index + 1) % subjects.Length);
            else if (previous && subjects.Length > 0) Show(index < 0 ? subjects.Length - 1 : (index + subjects.Length - 1) % subjects.Length);
            if (reset)
            {
                cameraRig.SetPositionAndRotation(origin, rotation);
                pitch = initialPitch;
                zoom = 1f;
                automatic = true;
                dragging = false;
            }
            if (toggle)
                automatic = !automatic;
            if (dragging && (orbit || pan))
            {
                var delta = at - pointer;
                if (pan)
                    cameraRig.position += (view.transform.up * -delta.y + view.transform.right * -delta.x) *
                        (distance * zoom / Mathf.Max(1, Screen.height));
                else
                {
                    cameraRig.Rotate(0f, delta.x * 0.25f, 0f, Space.World);
                    pitch = Mathf.Clamp(pitch - delta.y * 0.25f, 5f, 85f);
                }
                automatic = false;
            }
            pointer = at;
            dragging = orbit || pan;
            zoom = Mathf.Clamp(zoom * Mathf.Pow(0.8f, scroll), 0.025f, 3f);
            view.transform.localPosition = Quaternion.Euler(pitch, 0f, 0f) * new Vector3(0f, 0f, -distance * zoom);
            view.transform.LookAt(cameraRig.position);
            view.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) *
                Mathf.Max(1f, (16f / 9f) / view.aspect)) * Mathf.Rad2Deg;
            if (automatic)
                cameraRig.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
        }

        void OnGUI()
        {
            var title = subjects == null ? "" : index < 0 ? "All " + subjects.Length + " " + noun + "s" :
                (index + 1) + " / " + subjects.Length + " - " + subjects[index].name.Replace('_', ' ');
            GUI.Box(new Rect(12, 12, Mathf.Max(1, Screen.width - 24), 66), title +
                "\nLeft/right: choose " + noun + "; A: all; Space: auto orbit; R: reset" +
                "\nDrag to orbit; right-drag to pan; wheel to zoom");
        }

        void Show(int selected)
        {
            index = selected;
            rotation = overviewRotation;
            if (index < 0)
            {
                origin = overviewOrigin;
                distance = overviewDistance;
                initialPitch = overviewPitch;
                view.nearClipPlane = overviewNear;
            }
            else
            {
                var renderers = subjects[index].GetComponentsInChildren<Renderer>();
                var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(subjects[index].position, Vector3.one * 0.1f);
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                origin = bounds.center;
                var radius = Mathf.Max(0.01f, bounds.extents.magnitude);
                distance = radius / Mathf.Sin(fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;
                initialPitch = 20f;
                view.nearClipPlane = Mathf.Clamp(radius * 0.02f, 0.001f, overviewNear);
            }
            cameraRig.SetPositionAndRotation(origin, rotation);
            pitch = initialPitch;
            zoom = 1f;
            automatic = true;
            dragging = false;
        }

        IEnumerator Play(Animation anim)
        {
            yield return new WaitForSeconds(Random.Range(0f, pause));
            while (true)
                foreach (AnimationState state in anim)
                {
                    anim.Play(state.name);
                    yield return new WaitForSeconds(state.length + pause);
                }
        }

        /// <summary>A creature's controller, driven as a game would: it walks (and runs), attacks, is hit, dies and gets up.</summary>
        IEnumerator Play(Animator animator)
        {
            yield return new WaitForSeconds(Random.Range(0f, pause));
            while (true)
            {
                yield return new WaitForSeconds(pause);
                animator.SetFloat("Walk", 1f);
                yield return new WaitForSeconds(3f);
                animator.SetFloat("Walk", 0f);
                if (System.Array.Exists(animator.parameters, x => x.name == "Run"))
                {
                    animator.SetFloat("Run", 1f);
                    yield return new WaitForSeconds(2f);
                    animator.SetFloat("Run", 0f);
                }
                foreach (var trigger in new[] { "Attack", "Hit", "Die" })
                {
                    yield return new WaitForSeconds(pause);
                    animator.SetTrigger(trigger);
                    yield return new WaitForSeconds(0.25f);  // into its state
                    yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);
                }
                yield return new WaitForSeconds(pause);
                // back on its feet out of sight: a rebind in view snaps the body from the floor to standing
                var shown = animator.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                foreach (var r in shown)
                    r.enabled = false;
                animator.Rebind();
                animator.Update(0f);
                yield return new WaitForSeconds(pause);
                foreach (var r in shown)
                    r.enabled = true;
            }
        }
    }
}
