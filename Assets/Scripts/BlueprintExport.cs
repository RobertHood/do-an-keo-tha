using System;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Exports (downloads) the blueprint in use as a .blueprint file, and imports .blueprint files into the editor.
/// - Editor: native Save / Open file dialogs, starting in the Downloads folder.
/// - WebGL: browser download and browser file picker (Assets/Plugins/WebGL/BlueprintFileIO.jslib).
/// - Desktop builds: Export writes to Downloads; Import loads the newest .blueprint in Downloads or Documents/Blueprints.
/// Connect UI Buttons OnClick() -> BlueprintExport -> Export() / Import().
/// Requires BlueprintSave in the scene.
/// </summary>
[AddComponentMenu("Blueprint/Blueprint Export")]
public class BlueprintExport : MonoBehaviour
{
    public static BlueprintExport Instance { get; private set; }

    [Tooltip("SpawnInModel in the scene. If empty, found automatically.")]
    [SerializeField] private SpawnInModel spawner;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void BlueprintDownloadFile(string fileName, string content);
    [DllImport("__Internal")] private static extern void BlueprintOpenFilePicker(string gameObjectName, string callbackMethod, string extension);
#endif

    public string DownloadsFolder
    {
        get
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(profile))
            {
                string downloads = Path.Combine(profile, "Downloads");
                if (Directory.Exists(downloads)) return downloads;
            }

            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return string.IsNullOrEmpty(documents) ? Application.persistentDataPath : documents;
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

        if (spawner == null)
        {
            spawner = FindAnyObjectByType<SpawnInModel>();
        }
    }

    /// <summary>
    /// Downloads the blueprint in use as a .blueprint file. (Can be connected to a UI Button OnClick).
    /// </summary>
    public void Export()
    {
        if (BlueprintSave.Instance == null)
        {
            Debug.LogWarning("[BlueprintExport] BlueprintSave is missing from the scene.");
            return;
        }

        BlueprintData data = BlueprintSave.Instance.Capture();
        if (data == null) return;

        string json = data.ToJson();
        string baseName = ToSafeFileName(data.name);

#if UNITY_WEBGL && !UNITY_EDITOR
        BlueprintDownloadFile(baseName + "." + BlueprintData.FileExtension, json);
        Debug.Log($"[BlueprintExport] Downloaded '{data.name}' ({data.models.Count} model(s)).");
#else
  #if UNITY_EDITOR
        string path = EditorUtility.SaveFilePanel("Export Blueprint", DownloadsFolder, baseName, BlueprintData.FileExtension);
        if (string.IsNullOrEmpty(path)) return; // Cancelled
  #else
        string path = GetUniquePath(DownloadsFolder, baseName);
  #endif
        try
        {
            File.WriteAllText(path, json);
            Debug.Log($"[BlueprintExport] Exported '{data.name}' ({data.models.Count} model(s)) to: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[BlueprintExport] Failed to write '{path}': {e.Message}");
        }
#endif
    }

    /// <summary>
    /// Picks a .blueprint file and loads it into the editor, replacing the current environment.
    /// (Can be connected to a UI Button OnClick).
    /// </summary>
    public void Import()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // The browser picker is async; the file contents come back through OnWebFilePicked
        BlueprintOpenFilePicker(gameObject.name, nameof(OnWebFilePicked), BlueprintData.FileExtension);
#else
  #if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("Import Blueprint", DownloadsFolder, BlueprintData.FileExtension);
        if (string.IsNullOrEmpty(path)) return; // Cancelled
  #else
        string blueprintsFolder = BlueprintSave.Instance != null ? BlueprintSave.Instance.BlueprintsFolder : null;
        string path = FindLatestBlueprint(DownloadsFolder, blueprintsFolder);
        if (path == null)
        {
            Debug.LogWarning($"[BlueprintExport] No .{BlueprintData.FileExtension} files found in Downloads or Documents/Blueprints.");
            return;
        }
  #endif
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BlueprintExport] Failed to read '{path}': {e.Message}");
            return;
        }

        ImportFromJson(json, Path.GetFileNameWithoutExtension(path));
#endif
    }

    /// <summary>
    /// Called from the browser (BlueprintFileIO.jslib) with the picked file's contents.
    /// </summary>
    public void OnWebFilePicked(string json)
    {
        ImportFromJson(json, null);
    }

    /// <summary>
    /// Loads blueprint JSON into the editor as a new, unsaved blueprint (the next Save() creates a new record,
    /// so importing never overwrites someone else's blueprint). Returns true on success.
    /// </summary>
    public bool ImportFromJson(string json, string fallbackName)
    {
        if (!BlueprintData.TryFromJson(json, out BlueprintData data))
        {
            Debug.LogError("[BlueprintExport] File is not a valid blueprint.");
            return false;
        }

        if (data.version > BlueprintData.CurrentVersion)
        {
            Debug.LogWarning($"[BlueprintExport] Blueprint version {data.version} is newer than supported ({BlueprintData.CurrentVersion}). Some data may be ignored.");
        }

        if (!ApplyToScene(data)) return false;

        if (BlueprintSave.Instance != null)
        {
            string blueprintName = !string.IsNullOrWhiteSpace(data.name) ? data.name : fallbackName;
            BlueprintSave.Instance.BeginNewBlueprint(blueprintName);
        }

        return true;
    }

    /// <summary>
    /// Clears the environment and spawns every model in the blueprint at its saved transform.
    /// Also the entry point for opening a blueprint fetched from the database later.
    /// </summary>
    public bool ApplyToScene(BlueprintData data)
    {
        if (spawner == null)
        {
            Debug.LogWarning("[BlueprintExport] No SpawnInModel found in the scene.");
            return false;
        }

        ClearEnvironment();

        int loaded = 0;
        foreach (BlueprintModelData model in data.models)
        {
            if (spawner.SpawnFromBlueprint(model) != null) loaded++;
        }

        Debug.Log($"[BlueprintExport] Loaded '{data.name}': {loaded}/{data.models.Count} model(s).");
        return true;
    }

    /// <summary>
    /// Removes every spawned model and closes any gizmos pointing at them.
    /// </summary>
    private void ClearEnvironment()
    {
        if (ModelRotate.Instance != null)
        {
            ModelRotate.Instance.EnableRotateMode(false);
            ModelRotate.Instance.SetTarget(null);
        }

        if (ModelMovement.Instance != null)
        {
            ModelMovement.Instance.DeselectModel();
        }

        if (spawner.ModelsContainer == null) return;

        foreach (Transform child in spawner.ModelsContainer)
        {
            if (child.TryGetComponent<DraggableModel>(out _))
            {
                Destroy(child.gameObject);
            }
        }
    }

    private static string ToSafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Blueprint";

        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return safe.Length > 0 ? safe : "Blueprint";
    }

    /// <summary>
    /// Returns folder/name.blueprint, adding " (1)", " (2)"... if that file already exists.
    /// </summary>
    private static string GetUniquePath(string folder, string name)
    {
        string ext = "." + BlueprintData.FileExtension;
        string path = Path.Combine(folder, name + ext);

        for (int i = 1; File.Exists(path); i++)
        {
            path = Path.Combine(folder, $"{name} ({i}){ext}");
        }

        return path;
    }

    /// <summary>
    /// Returns the most recently modified .blueprint file across the given folders, or null if none exist.
    /// </summary>
    private static string FindLatestBlueprint(params string[] folders)
    {
        return folders
            .Where(f => !string.IsNullOrEmpty(f) && Directory.Exists(f))
            .SelectMany(f => Directory.GetFiles(f, "*." + BlueprintData.FileExtension))
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .FirstOrDefault();
    }
}
