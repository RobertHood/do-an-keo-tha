using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serializable snapshot of the whole environment. Stored by BlueprintSave (via IBlueprintStorage),
/// written to .blueprint files by BlueprintExport, and rebuilt into the scene by BlueprintExport.Import().
/// The metadata fields map 1:1 to a database record when blueprints move to a web backend.
/// </summary>
[Serializable]
public class BlueprintData
{
    public const string FormatId = "Blueprint";
    public const string FileExtension = "blueprint"; // without the dot
    public const int CurrentVersion = 1;

    public string format = FormatId;
    public int version = CurrentVersion;

    public string id;                // Unique blueprint ID (GUID locally; database record ID later)
    public string name = "Untitled";
    public string createdAt;         // ISO 8601, UTC
    public string updatedAt;         // ISO 8601, UTC

    public List<BlueprintModelData> models = new List<BlueprintModelData>();

    public string ToJson(bool prettyPrint = true)
    {
        return JsonUtility.ToJson(this, prettyPrint);
    }

    /// <summary>
    /// Parses blueprint JSON. Returns false if the text isn't valid JSON or isn't a blueprint.
    /// </summary>
    public static bool TryFromJson(string json, out BlueprintData data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            data = JsonUtility.FromJson<BlueprintData>(json);
        }
        catch (ArgumentException)
        {
            data = null;
        }

        if (data == null || data.format != FormatId || data.models == null)
        {
            data = null;
            return false;
        }

        return true;
    }
}

/// <summary>
/// One placed model. Transform values are local to the SpawnedModels container.
/// </summary>
[Serializable]
public class BlueprintModelData
{
    public string prefabId;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
    public bool isLocked;

    // Pricing choice (see ModelEstPrice)
    public string selectedStore;
    public bool useCustomPrice;
    public double customPrice;
}
