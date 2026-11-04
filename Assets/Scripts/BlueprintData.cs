using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serializable snapshot of the whole environment. Written to disk as JSON by BlueprintSave / BlueprintExport
/// and rebuilt into the scene by BlueprintLoad.
/// </summary>
[System.Serializable]
public class BlueprintData
{
    public const string FormatId = "Blueprint";
    public const int CurrentVersion = 1;

    public string format = FormatId;
    public int version = CurrentVersion;
    public string savedAt;
    public List<BlueprintModelData> models = new List<BlueprintModelData>();
}

/// <summary>
/// One placed model. Transform values are local to the SpawnedModels container.
/// </summary>
[System.Serializable]
public class BlueprintModelData
{
    public string prefabId;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
    public bool isLocked;
}
