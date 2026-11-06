using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Where BlueprintSave stores blueprints. Callback-based so a web implementation (UnityWebRequest to a
/// blueprint database API) can be dropped in without changing BlueprintSave:
///     BlueprintSave.Instance.Storage = new WebBlueprintStorage(apiUrl, authToken);
/// </summary>
public interface IBlueprintStorage
{
    /// <summary>
    /// Creates or overwrites the blueprint with data.id. Calls onComplete(true) on success.
    /// </summary>
    void Save(BlueprintData data, Action<bool> onComplete);
}

/// <summary>
/// Stores blueprints as files named by ID: [folder]/[id].blueprint. Used until the web backend exists.
/// </summary>
public class LocalBlueprintStorage : IBlueprintStorage
{
    public string Folder { get; }

    public LocalBlueprintStorage(string folder)
    {
        Folder = folder;
    }

    public string GetPath(string id)
    {
        return Path.Combine(Folder, id + "." + BlueprintData.FileExtension);
    }

    public void Save(BlueprintData data, Action<bool> onComplete)
    {
        string path = GetPath(data.id);
        bool success = false;

        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(path, data.ToJson());
            success = true;
            Debug.Log($"[LocalBlueprintStorage] Saved '{data.name}' ({data.models.Count} model(s)) to: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalBlueprintStorage] Failed to write '{path}': {e.Message}");
        }

        onComplete?.Invoke(success);
    }
}
