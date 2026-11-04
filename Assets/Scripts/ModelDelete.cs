using UnityEngine;

/// <summary>
/// Deletes the currently selected model from the scene.
/// Connect a UI Button OnClick() -> ModelDelete -> DeleteSelectedModel().
/// </summary>
[AddComponentMenu("Interaction/Model Delete")]
public class ModelDelete : MonoBehaviour
{
    public static ModelDelete Instance { get; private set; }

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
    /// Removes the selected model from the environment. (Can be connected to a UI Button OnClick).
    /// </summary>
    public void DeleteSelectedModel()
    {
        if (ModelMovement.Instance == null) return;

        Transform model = ModelMovement.Instance.SelectedModel;
        if (model == null)
        {
            Debug.LogWarning("[ModelDelete] No model selected to delete! Click on a model first.");
            return;
        }

        // Close gizmos so they don't keep pointing at the deleted model
        if (ModelRotate.Instance != null)
        {
            ModelRotate.Instance.EnableRotateMode(false);
            ModelRotate.Instance.SetTarget(null);
        }

        // Hides the selection box and closes Resize mode
        ModelMovement.Instance.DeselectModel();

        Debug.Log($"[ModelDelete] Deleted '{model.name}'");
        Destroy(model.gameObject);
    }
}
