using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Saves the blueprint in use to storage, overwriting its previous save (matched by blueprint ID).
/// The first save of a new blueprint gives it an ID; every save after that overwrites the same record.
/// Storage is local files for now (Documents/Blueprints) and can be swapped for a web database via Storage.
/// Connect a UI Button OnClick() -> BlueprintSave -> Save().
/// </summary>
[AddComponentMenu("Blueprint/Blueprint Save")]
public class BlueprintSave : MonoBehaviour
{
    public static BlueprintSave Instance { get; private set; }

    [Tooltip("SpawnInModel in the scene. If empty, found automatically.")]
    [SerializeField] private SpawnInModel spawner;

    [Tooltip("Folder name inside the user's Documents folder where blueprints are saved (local storage only).")]
    [SerializeField] private string blueprintsFolderName = "Blueprints";

    /// <summary>
    /// Where Save() stores blueprints. Defaults to local files; replace with a web implementation later.
    /// </summary>
    public IBlueprintStorage Storage { get; set; }

    /// <summary>
    /// ID of the blueprint in use, or null if it hasn't been saved yet.
    /// </summary>
    public string CurrentBlueprintId { get; private set; }

    public string CurrentBlueprintName { get; private set; } = "Untitled";

    public bool IsSaving { get; private set; }

    private string currentCreatedAt;

    public string BlueprintsFolder
    {
        get
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string root = string.IsNullOrEmpty(documents) ? Application.persistentDataPath : documents;
            return Path.Combine(root, blueprintsFolderName);
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

        if (Storage == null)
        {
            Storage = new LocalBlueprintStorage(BlueprintsFolder);
        }
    }

    /// <summary>
    /// Saves the blueprint in use, overwriting its previous save. (Can be connected to a UI Button OnClick).
    /// </summary>
    public void Save()
    {
        if (IsSaving) return; // Ignore double-clicks while a (web) save is still in flight

        BlueprintData data = Capture();
        if (data == null) return;

        IsSaving = true;
        Storage.Save(data, success =>
        {
            IsSaving = false;
            if (success)
            {
                CurrentBlueprintId = data.id;
                currentCreatedAt = data.createdAt;
            }
        });
    }

    /// <summary>
    /// Renames the blueprint in use. Takes effect on the next Save() / Export().
    /// (Can be connected to an InputField's On End Edit as a dynamic string.)
    /// </summary>
    public void SetBlueprintName(string blueprintName)
    {
        CurrentBlueprintName = string.IsNullOrWhiteSpace(blueprintName) ? "Untitled" : blueprintName.Trim();
    }

    /// <summary>
    /// Treats the scene as a new, unsaved blueprint, so the next Save() creates a new record instead of
    /// overwriting the previous one. Called after importing a file.
    /// </summary>
    public void BeginNewBlueprint(string blueprintName)
    {
        CurrentBlueprintId = null;
        currentCreatedAt = null;
        SetBlueprintName(blueprintName);
    }

    /// <summary>
    /// Builds a snapshot of every DraggableModel in the SpawnedModels container, tagged with the
    /// blueprint's ID, name and timestamps.
    /// </summary>
    public BlueprintData Capture()
    {
        if (spawner == null || spawner.ModelsContainer == null)
        {
            Debug.LogWarning("[BlueprintSave] No SpawnInModel / SpawnedModels container found. Nothing to save.");
            return null;
        }

        string now = DateTime.UtcNow.ToString("o");
        BlueprintData data = new BlueprintData
        {
            id = !string.IsNullOrEmpty(CurrentBlueprintId) ? CurrentBlueprintId : Guid.NewGuid().ToString("N"),
            name = CurrentBlueprintName,
            createdAt = !string.IsNullOrEmpty(currentCreatedAt) ? currentCreatedAt : now,
            updatedAt = now
        };

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
                isLocked = draggable.isLocked,
                selectedStore = draggable.selectedStore,
                useCustomPrice = draggable.useCustomPrice,
                customPrice = draggable.customPrice
            });
        }

        return data;
    }
}
