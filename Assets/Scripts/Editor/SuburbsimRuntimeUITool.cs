using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the UI that would otherwise be created when Play starts into the open scene, so it can be seen and
/// arranged in the editor:
/// - the land panel (inside the Canvas child "Land Settings") and the "Thửa đất" toolbar button (UILandBoundary),
/// - the compass in the top-right corner (Canvas child "Compass", UICompass).
/// Parts that already exist are left alone; delete one and run the menu again to rebuild it.
/// Editor: Tools > Suburbsim > Build Land Panel and Compass (then save the scene)
/// </summary>
public static class SuburbsimRuntimeUITool
{
    [MenuItem("Tools/Suburbsim/Build Land Panel and Compass")]
    public static void BuildFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Suburbsim UI", "Stop Play mode first: changes made while playing are not saved.", "OK");
            return;
        }

        Canvas canvas = UIBuild.FindMainCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Suburbsim UI", "No UI Canvas found in the open scene.", "OK");
            return;
        }

        HashSet<Transform> before = new HashSet<Transform>(canvas.GetComponentsInChildren<Transform>(true));
        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Build Land Panel and Compass");
        List<string> report = new List<string>();

        // Land panel + toolbar button
        UILandBoundary land = Object.FindAnyObjectByType<UILandBoundary>(FindObjectsInactive.Include);
        if (land == null)
        {
            GameObject holder = new GameObject("UILandBoundary", typeof(RectTransform));
            holder.layer = 5;
            holder.transform.SetParent(canvas.transform, false);
            land = holder.AddComponent<UILandBoundary>();
        }

        SerializedObject landSo = new SerializedObject(land);
        bool landBuilt = landSo.FindProperty("summary").objectReferenceValue != null;
        if (landBuilt)
        {
            report.Add("- Land panel: already in the scene (delete the panel's \"Card\", \"Content\"... children and clear UILandBoundary's parts to rebuild).");
        }
        else
        {
            land.BuildInEditor(canvas);
            EditorUtility.SetDirty(land);
            report.Add("- Land panel: built inside \"Land Settings\" and the \"Thửa đất\" button added to the toolbar.");
        }

        // Compass
        UICompass compass = Object.FindAnyObjectByType<UICompass>(FindObjectsInactive.Include);
        if (compass != null && !(compass.transform is RectTransform))
        {
            // A plain holder from an older setup: replace it with one on the Canvas
            Undo.DestroyObjectImmediate(compass.gameObject);
            compass = null;
        }
        if (compass == null)
        {
            GameObject root = new GameObject("Compass", typeof(RectTransform));
            root.layer = 5;
            root.transform.SetParent(canvas.transform, false);
            compass = root.AddComponent<UICompass>();
            compass.BuildInEditor();
            EditorUtility.SetDirty(compass);
            report.Add("- Compass: built in the top-right corner (Canvas child \"Compass\").");
        }
        else
        {
            report.Add("- Compass: already in the scene (delete \"Compass\" to rebuild).");
        }

        // Undo for everything that was created
        foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true).Where(t => !before.Contains(t)))
        {
            if (t.parent == null || before.Contains(t.parent)) Undo.RegisterCreatedObjectUndo(t.gameObject, "Build Land Panel and Compass");
        }

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        report.Add("");
        report.Add("Move and restyle them as you like, then save the scene (Ctrl+S). The land panel is hidden when Play starts and opens with the \"Thửa đất\" button.");
        EditorUtility.DisplayDialog("Suburbsim UI", string.Join("\n", report), "OK");
    }
}
