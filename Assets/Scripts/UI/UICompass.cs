using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compass in the top-right corner of the screen. The dial turns with the camera so "B" always points North
/// (+Z in the scene), and the text under it says which way the camera is looking. Click it to face North.
/// Normally built into the scene (on the Canvas child "Compass") with Tools > Suburbsim > Build Land Panel and
/// Compass, so it can be moved and restyled in the editor. If the scene has none, one is built when Play starts.
/// </summary>
[AddComponentMenu("UI/UI Compass")]
public class UICompass : MonoBehaviour
{
    [SerializeField] private float dialSize = 88f;
    [SerializeField] private Vector2 margin = new Vector2(24f, 16f);

    [Header("Parts (filled by the build menu)")]
    [SerializeField] private RectTransform rose;
    [SerializeField] private List<RectTransform> uprightLabels = new List<RectTransform>();
    [SerializeField] private TextMeshProUGUI readout;
    [SerializeField] private Button face;

    private RectTransform runtimeRoot; // Only set when the compass was built at runtime
    private Camera cameraController;
    private float shownBearing = float.NaN;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateIfMissing()
    {
        if (FindAnyObjectByType<UICompass>(FindObjectsInactive.Include) == null)
        {
            new GameObject("UICompass").AddComponent<UICompass>();
        }
    }

    private void Start()
    {
        cameraController = FindAnyObjectByType<Camera>();
        if (rose == null || readout == null || face == null)
        {
            Canvas canvas = UIBuild.FindMainCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[UICompass] No UI Canvas found.");
                return;
            }
            runtimeRoot = UIBuild.Rect(canvas.transform, "Compass");
            Build(runtimeRoot);
        }
        face.onClick.AddListener(FaceNorth);
    }

    private void OnDestroy()
    {
        if (runtimeRoot != null) Destroy(runtimeRoot.gameObject);
    }

    /// <summary>
    /// Builds the compass under this object (a RectTransform on the Canvas). Called by the editor menu.
    /// </summary>
    public void BuildInEditor()
    {
        Build((RectTransform)transform);
    }

    private void Build(RectTransform root)
    {
        UIKit kit = UIKit.Get();
        Sprite circle = kit != null ? kit.circle : null;

        UIBuild.Place(root, Vector2.one, Vector2.one, Vector2.one, new Vector2(-margin.x, -margin.y), new Vector2(dialSize + 24f, dialSize + 34f));
        uprightLabels.Clear();

        // Dial: soft shadow, thin grey ring, white face (clickable)
        RectTransform dial = UIBuild.Rect(root, "Dial");
        UIBuild.Place(dial, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(dialSize, dialSize));
        Image shadow = UIBuild.Image(dial, "Shadow", circle, new Color(0f, 0f, 0f, 0.12f));
        UIBuild.Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -3f), new Vector2(dialSize + 6f, dialSize + 6f));
        Image ring = UIBuild.Image(dial, "Ring", circle, UIPalette.Neutral200);
        UIBuild.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(dialSize, dialSize));
        face = UIBuild.Button(dial, "Face", circle, UIPalette.White, null);
        UIBuild.Place((RectTransform)face.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(dialSize - 3f, dialSize - 3f));

        // Rose: turns with the camera
        rose = UIBuild.Rect(dial, "Rose");
        UIBuild.Place(rose, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(dialSize, dialSize));

        float radius = dialSize * 0.5f;
        for (int i = 0; i < 8; i++)
        {
            bool cardinal = i % 2 == 0;
            Image tick = UIBuild.Image(rose, "Tick " + i, null, cardinal ? UIPalette.Neutral400 : UIPalette.Neutral300);
            RectTransform t = tick.rectTransform;
            t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
            t.pivot = new Vector2(0.5f, 1f);
            t.sizeDelta = new Vector2(cardinal ? 2f : 1.5f, cardinal ? 6f : 4f);
            t.localEulerAngles = new Vector3(0f, 0f, -i * 45f);
            t.anchoredPosition = Polar(i * 45f, radius - 4f);
        }

        string[] letters = { "B", "Đ", "N", "T" };
        for (int i = 0; i < 4; i++)
        {
            bool north = i == 0;
            TextMeshProUGUI letter = UIBuild.Text(rose, letters[i], letters[i], north ? 17f : 13f,
                                                  north ? UIPalette.Red600 : UIPalette.Neutral700,
                                                  north ? UIBuild.Weight.Bold : UIBuild.Weight.SemiBold, TextAlignmentOptions.Center);
            RectTransform r = letter.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(24f, 24f);
            r.anchoredPosition = Polar(i * 90f, radius - 20f);
            uprightLabels.Add(r);
        }

        // Fixed marker at the top: where the camera is looking
        TextMeshProUGUI pointer = UIBuild.Text(dial, "Pointer", "▲", 11f, UIPalette.BrandBlack, UIBuild.Weight.Bold, TextAlignmentOptions.Center);
        UIBuild.Place(pointer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(16f, 12f));

        // Readout under the dial on a white pill
        Image pill = UIBuild.Image(root, "Readout", kit != null ? kit.pill : null, UIPalette.WithAlpha(UIPalette.White, 0.92f));
        UIBuild.Place(pill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(dialSize + 24f, 24f));
        readout = UIBuild.Text(pill.transform, "Text", "Nhìn về Bắc 0°", 12f, UIPalette.Neutral700, UIBuild.Weight.SemiBold, TextAlignmentOptions.Center);
        UIBuild.Stretch(readout.rectTransform);
    }

    private static Vector2 Polar(float bearing, float radius)
    {
        float r = bearing * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(r), Mathf.Cos(r)) * radius;
    }

    private void LateUpdate()
    {
        if (rose == null) return;
        UnityEngine.Camera cam = UnityEngine.Camera.main;
        if (cam == null) return;

        // Straight down, the camera's forward has no ground direction: use its up (the top of the screen) instead
        Vector3 forward = cam.transform.forward;
        Vector3 flat = new Vector3(forward.x, 0f, forward.z);
        if (flat.sqrMagnitude < 0.0004f) flat = new Vector3(cam.transform.up.x, 0f, cam.transform.up.z);
        float bearing = CompassDirections.FromDirection(flat);
        if (!float.IsNaN(shownBearing) && Mathf.Abs(Mathf.DeltaAngle(bearing, shownBearing)) < 0.1f) return;
        shownBearing = bearing;

        // Facing East, North is on the left: turn the rose counter-clockwise by the bearing
        rose.localEulerAngles = new Vector3(0f, 0f, bearing);
        foreach (RectTransform label in uprightLabels) label.localEulerAngles = new Vector3(0f, 0f, -bearing);
        readout.text = "Nhìn về " + CompassDirections.Format(bearing);
    }

    private void FaceNorth()
    {
        if (cameraController == null) cameraController = FindAnyObjectByType<Camera>();
        if (cameraController != null) cameraController.SetYaw(0f);
    }
}
