using System;
using UnityEngine;

/// <summary>
/// Stores blueprints through the host web page: Save() sends the blueprint JSON to the page
/// (HostBridge "save" event), and the page answers by calling WebBridge.OnHostSaveResult("1" or "0").
/// If no host page is attached (e.g. the build is opened on its own), it falls back to the given storage.
/// </summary>
public class WebBlueprintStorage : IBlueprintStorage
{
    // Answer the save if the page never responds, so BlueprintSave doesn't stay stuck in IsSaving.
    private const float TimeoutSeconds = 15f;

    private static Action<bool> pending;
    private static float pendingSince;

    private readonly IBlueprintStorage fallback;

    public WebBlueprintStorage(IBlueprintStorage fallback)
    {
        this.fallback = fallback;
    }

    public void Save(BlueprintData data, Action<bool> onComplete)
    {
        if (!HostBridge.IsAttached)
        {
            if (fallback != null) fallback.Save(data, onComplete);
            else onComplete?.Invoke(false);
            return;
        }

        // A newer save replaces one still waiting for an answer
        Complete(false);

        pending = onComplete;
        pendingSince = Time.realtimeSinceStartup;
        HostBridge.Send(HostBridge.EventSave, data.ToJson(false));
    }

    /// <summary>
    /// Finishes the save in flight. Called by WebBridge when the host page answers.
    /// </summary>
    public static void Complete(bool success)
    {
        Action<bool> callback = pending;
        pending = null;
        callback?.Invoke(success);
    }

    /// <summary>
    /// Fails the save in flight if the host hasn't answered in time. Called every frame by WebBridge.
    /// </summary>
    public static void CheckTimeout()
    {
        if (pending != null && Time.realtimeSinceStartup - pendingSince > TimeoutSeconds)
        {
            Debug.LogWarning("[WebBlueprintStorage] The host page did not answer the save in time.");
            Complete(false);
        }
    }
}
