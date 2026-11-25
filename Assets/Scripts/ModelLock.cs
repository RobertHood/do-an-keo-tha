using System;
using UnityEngine;

/// <summary>
/// Locks models in place so they can't be moved by mistake while arranging other models.
/// A locked model can still be clicked (it gets selected, with an amber box) but it can't be dragged,
/// rotated, resized or deleted until it is unlocked.
/// The lock is stored on DraggableModel.isLocked and saved with the blueprint.
/// Optional: add this component to the scene and connect a UI Button OnClick() -> ToggleSelectedModelLock().
/// The web page locks / unlocks models through WebBridge, which uses the static methods below.
/// </summary>
[AddComponentMenu("Interaction/Model Lock")]
public class ModelLock : MonoBehaviour
{
    /// <summary>
    /// Raised after a model is locked or unlocked.
    /// </summary>
    public static event Action<DraggableModel> LockChanged;

    /// <summary>
    /// Locks the selected model, or unlocks it if it is already locked. (Can be connected to a UI Button OnClick).
    /// </summary>
    public void ToggleSelectedModelLock()
    {
        Transform selected = ModelMovement.Instance != null ? ModelMovement.Instance.SelectedModel : null;
        if (selected == null || !selected.TryGetComponent(out DraggableModel model))
        {
            Debug.LogWarning("[ModelLock] No model selected to lock! Click on a model first.");
            UINotification.ShowError(NotificationTexts.SelectModelFirst);
            return;
        }

        SetLocked(model, !model.isLocked);

        if (!model.isLocked)
        {
            UINotification.ShowInfo(NotificationTexts.Unlocked);
        }
        else if (!UINotification.ShowTipOnce(NotificationTips.LockKey, NotificationTips.Lock))
        {
            UINotification.ShowInfo(NotificationTexts.Locked);
        }
    }

    public static bool IsLocked(Transform model)
    {
        return model != null && model.TryGetComponent(out DraggableModel draggable) && draggable.isLocked;
    }

    public static void SetLocked(DraggableModel model, bool locked)
    {
        if (model == null || model.isLocked == locked) return;

        model.isLocked = locked;

        // Close the rotate / resize gizmos so a just-locked model can't keep being edited through them
        if (locked && ModelMovement.Instance != null && ModelMovement.Instance.SelectedModel == model.transform)
        {
            if (ModelRotate.Instance != null && ModelRotate.Instance.IsRotateModeActive) ModelRotate.Instance.EnableRotateMode(false);
            if (ModelResize.Instance != null && ModelResize.Instance.IsResizeModeActive) ModelResize.Instance.EnableResizeMode(false);
        }

        Debug.Log($"[ModelLock] {(locked ? "Locked" : "Unlocked")} '{model.name}'");
        LockChanged?.Invoke(model);
    }
}
