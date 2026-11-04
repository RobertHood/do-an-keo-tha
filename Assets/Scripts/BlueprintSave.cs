using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Captures every spawned model into a BlueprintData snapshot and writes it to the quick-save slot.
/// Connect a UI Button OnClick() -> BlueprintSave -> Save().
/// </summary>
[AddComponentMenu("Blueprint/Blueprint Save")]
public class BlueprintSave : MonoBehaviour
{
    public static BlueprintSave Instance { get; private set; }

    /// <summary>
    /// Quick-save slot inside the app's persistent data folder.
    /// </summary>
    public static string QuickSavePath => Path.Combine(Application.persistentDataPath, "quicksave." + BlueprintExport.FileExtension);

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
    /// Saves the environment to the quick-save slot. (Can be connected to a UI Button OnClick).
    /// </summary>
    public void Save()
    {
        SaveToPath(QuickSavePath);
    }

    /// <summary>
    /// Captures the environment and writes it as JSON to the given path. Returns true on success.
    /// </summary>
    public bool SaveToPath(string path)
    {
        BlueprintData data = Capture();
        if (data == null) return false;

        try
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            Debug.Log($"[BlueprintSave] Saved {data.models.Count} model(s) to: {path}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[BlueprintSave] Failed to write '{path}': {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Builds a snapshot of every DraggableModel in the SpawnedModels container.
    /// </summary>
    public BlueprintData Capture()
    {
        if (spawner == null || spawner.ModelsContainer == null)
        {
            Debug.LogWarning("[BlueprintSave] No SpawnInModel / SpawnedModels container found. Nothing to save.");
            return null;
        }

        BlueprintData data = new BlueprintData { savedAt = DateTime.Now.ToString("o") };

        foreach (Transform child in spawner.ModelsContainer)
        {
            if (!child.TryGetComponent(out DraggableModel draggable)) continue;

            if (string.IsNullOrEmpty(draggable.prefabId))
            {
                Debug.LogWarning($"[BlueprintSave] '{child.name}' has no prefabId and will be skipped.");
                continue;
            }

            data.models.Add(new BlueprintModelData
            {
                prefabId = draggable.prefabId,
                position = child.localPosition,
                rotation = child.localRotation,
                scale = child.localScale,
                isLocked = draggable.isLocked
            });
        }

        return data;
    }
}
