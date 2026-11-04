using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Shared helpers for the selection, resize and rotate gizmos.
/// </summary>
public static class GizmoUtility
{
    /// <summary>
    /// Tight bounds of the model in its own local space. Uses each renderer's mesh-space bounds, so the
    /// result doesn't inflate when the model is rotated (unlike renderer.bounds, which is a world AABB).
    /// </summary>
    public static Bounds GetLocalBounds(Transform model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        Bounds combined = new Bounds(Vector3.zero, Vector3.zero);

        foreach (Renderer r in renderers)
        {
            if (r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer) continue;

            Bounds lb = r.localBounds;
            Vector3 min = lb.min;
            Vector3 max = lb.max;

            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = new Vector3(
                    (c & 1) == 0 ? min.x : max.x,
                    (c & 2) == 0 ? min.y : max.y,
                    (c & 4) == 0 ? min.z : max.z);

                Vector3 local = model.InverseTransformPoint(r.transform.TransformPoint(corner));

                if (!hasBounds)
                {
                    combined = new Bounds(local, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(local);
                }
            }
        }

        return hasBounds ? combined : new Bounds(Vector3.zero, Vector3.one);
    }

    /// <summary>
    /// Returns the world-space size that covers the given number of screen pixels at a world position.
    /// Used to keep gizmo lines and handles the same on-screen size at any zoom level.
    /// </summary>
    public static float PixelsToWorld(UnityEngine.Camera cam, Vector3 worldPos, float pixels)
    {
        if (cam == null) return pixels * 0.01f;

        float worldPerPixel;
        if (cam.orthographic)
        {
            worldPerPixel = 2f * cam.orthographicSize / cam.pixelHeight;
        }
        else
        {
            float depth = Mathf.Max(0.01f, Vector3.Dot(worldPos - cam.transform.position, cam.transform.forward));
            worldPerPixel = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / cam.pixelHeight;
        }

        return pixels * worldPerPixel;
    }

    /// <summary>
    /// Distance in pixels from point p to the line segment a-b.
    /// </summary>
    public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSq = ab.sqrMagnitude;
        if (lengthSq < 0.0001f) return Vector2.Distance(p, a);

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
        return Vector2.Distance(p, a + ab * t);
    }

    public static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    public static Vector2 GetPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    public static bool WasPointerPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    public static bool WasPointerReleased()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
#else
        return Input.GetMouseButtonUp(0);
#endif
    }

    public static bool IsShiftHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
#else
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
    }

    public static bool WasCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    /// <summary>
    /// Material for LineRenderers. Sprites/Default respects the LineRenderer's start/end colors
    /// (URP/Unlit ignores vertex colors, which leaves lines white).
    /// </summary>
    public static Material CreateLineMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return CreateUnlitMaterial();
        return new Material(shader);
    }

    /// <summary>
    /// Material for solid meshes (handle cubes). Tint it with material.color.
    /// </summary>
    public static Material CreateUnlitMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return new Material(shader != null ? shader : Shader.Find("Hidden/Internal-Colored"));
    }
}
