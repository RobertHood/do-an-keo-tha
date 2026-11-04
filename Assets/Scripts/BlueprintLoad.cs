using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Reads a blueprint file and rebuilds the environment, replacing all currently spawned models.
/// Connect a UI Button OnClick() -> BlueprintLoad -> Load().
/// </summary>
[AddComponentMenu("Blueprint/Blueprint Load")]
public class BlueprintLoad : MonoBehaviour
{
    public static BlueprintLoad Instance { get; private set; }

    [Tooltip("SpawnInModel in the scene. If empty, found automatically.")]
    [SerializeField] private SpawnInModel spawner;

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
    /// Loads the quick-save slot. (Can be connected to a UI Button OnClick).
    /// </summary>
    public void Load()
    {
        LoadFromFile(BlueprintSave.QuickSavePath);
    }

    /// <summary>
    /// Reads a blueprint file from disk and rebuilds the environment. Returns true on success.
    /// </summary>
    public bool LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[BlueprintLoad] No blueprint found at: {path}");
            return false;
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BlueprintLoad] Failed to read '{path}': {e.Message}");
            return false;
        }

        return LoadFromJson(json);
    }

    /// <summary>
    /// Parses blueprint JSON and rebuilds the environment. Returns true on success.
    /// </summary>
    public bool LoadFromJson(string json)
    {
        BlueprintData data;
        try
        {
            data = JsonUtility.FromJson<BlueprintData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BlueprintLoad] File is not a valid blueprint: {e.Message}");
            return false;
        }

        if (data == null || data.format != BlueprintData.FormatId || data.models == null)
        {
            Debug.LogError("[BlueprintLoad] File is not a valid blueprint.");
            return false;
        }

        if (data.version > BlueprintData.CurrentVersion)
        {
            Debug.LogWarning($"[BlueprintLoad] Blueprint version {data.version} is newer than supported ({BlueprintData.CurrentVersion}). Some data may be ignored.");
        }

        Apply(data);
        return true;
    }

    /// <summary>
    /// Clears the environment and spawns every model in the blueprint at its saved transform.
    /// </summary>
    public void Apply(BlueprintData data)
    {
        if (spawner == null)
        {
            Debug.LogWarning("[BlueprintLoad] No SpawnInModel found in the scene.");
            return;
        }

        ClearEnvironment();

        int loaded = 0;
        foreach (BlueprintModelData model in data.models)
        {
            if (spawner.SpawnFromBlueprint(model) != null) loaded++;
        }

        Debug.Log($"[BlueprintLoad] Loaded {loaded}/{data.models.Count} model(s).");
    }

    /// <summary>
    /// Removes every spawned model and closes any gizmos pointing at them.
    /// </summary>
    public void ClearEnvironment()
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

        if (spawner == null || spawner.ModelsContainer == null) return;

        foreach (Transform child in spawner.ModelsContainer)
        {
            if (child.TryGetComponent<DraggableModel>(out _))
            {
                Destroy(child.gameObject);
            }
        }
    }
}
