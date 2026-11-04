using System;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Exports the environment to a standalone .blueprint file and imports it back.
/// - In the Editor: opens native Save / Open file dialogs.
/// - In builds: exports to Documents/Blueprints with a timestamped name; Import loads the most recent export.
/// Connect UI Buttons OnClick() -> BlueprintExport -> Export() / Import().
/// Requires BlueprintSave and BlueprintLoad in the scene.
/// </summary>
[AddComponentMenu("Blueprint/Blueprint Export")]
public class BlueprintExport : MonoBehaviour
{
    public static BlueprintExport Instance { get; private set; }

    /// <summary>
    /// File extension for exported blueprints (without the dot).
    /// </summary>
    public const string FileExtension = "blueprint";

    [Tooltip("Folder name inside the user's Documents folder where exports are written.")]
    [SerializeField] private string exportFolderName = "Blueprints";

    public string ExportFolder
    {
        get
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string root = string.IsNullOrEmpty(documents) ? Application.persistentDataPath : documents;
            return Path.Combine(root, exportFolderName);
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }
    }

    /// <summary>
    /// Exports the environment to a .blueprint file. (Can be connected to a UI Button OnClick).
    /// </summary>
    public void Export()
    {
        Directory.CreateDirectory(ExportFolder);
        string defaultName = "Blueprint_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

#if UNITY_EDITOR
        string path = EditorUtility.SaveFilePanel("Export Blueprint", ExportFolder, defaultName, FileExtension);
        if (string.IsNullOrEmpty(path)) return; // Cancelled
#else
        string path = Path.Combine(ExportFolder, defaultName + "." + FileExtension);
#endif

        ExportToPath(path);
    }

    /// <summary>
    /// Imports a .blueprint file, replacing the current environment. (Can be connected to a UI Button OnClick).
    /// </summary>
    public void Import()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("Import Blueprint", ExportFolder, FileExtension);
        if (string.IsNullOrEmpty(path)) return; // Cancelled
#else
        string path = FindLatestExport();
        if (path == null)
        {
            Debug.LogWarning($"[BlueprintExport] No .{FileExtension} files found in: {ExportFolder}");
            return;
        }
#endif

        ImportFromPath(path);
    }

    /// <summary>
    /// Writes the environment to the given file path. Returns true on success.
    /// </summary>
    public bool ExportToPath(string path)
    {
        if (BlueprintSave.Instance == null)
        {
            Debug.LogWarning("[BlueprintExport] BlueprintSave is missing from the scene.");
            return false;
        }

        return BlueprintSave.Instance.SaveToPath(path);
    }

    /// <summary>
    /// Loads a blueprint from the given file path. Returns true on success.
    /// </summary>
    public bool ImportFromPath(string path)
    {
        if (BlueprintLoad.Instance == null)
        {
            Debug.LogWarning("[BlueprintExport] BlueprintLoad is missing from the scene.");
            return false;
        }

        return BlueprintLoad.Instance.LoadFromFile(path);
    }

    /// <summary>
    /// Returns the most recently modified .blueprint file in the export folder, or null if none exist.
    /// </summary>
    public string FindLatestExport()
    {
        if (!Directory.Exists(ExportFolder)) return null;

        return Directory.GetFiles(ExportFolder, "*." + FileExtension)
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .FirstOrDefault();
    }
}
