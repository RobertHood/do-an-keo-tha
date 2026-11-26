using TMPro;
using UnityEngine;

/// <summary>
/// Sprites and fonts of the app's UI kit (Assets/UI), for UI that is built at runtime (land panel, compass,
/// labels in the 3D view). The asset lives at Assets/UI/Resources/SuburbsimUIKit.asset so scripts can load it
/// without being wired up in the scene.
/// </summary>
[CreateAssetMenu(fileName = "SuburbsimUIKit", menuName = "Suburbsim/UI Kit")]
public class UIKit : ScriptableObject
{
    private const string ResourcePath = "SuburbsimUIKit";

    public Sprite round8;
    public Sprite round12;
    public Sprite round16;
    public Sprite pill;
    public Sprite outline12;
    public Sprite outline16;
    public Sprite outlinePill;
    public Sprite circle;
    public Sprite shadow16;
    public Sprite landIcon;

    public TMP_FontAsset regular;
    public TMP_FontAsset semiBold;
    public TMP_FontAsset bold;

    private static UIKit instance;
    private static bool loaded;

    /// <summary>
    /// The UI kit, or null if the asset is missing (UI then falls back to plain shapes and the default font).
    /// </summary>
    public static UIKit Get()
    {
        if (!loaded)
        {
            instance = Resources.Load<UIKit>(ResourcePath);
            loaded = true;
            if (instance == null) Debug.LogWarning($"[UIKit] Resources/{ResourcePath}.asset not found. Runtime UI will use plain shapes.");
        }
        return instance;
    }
}
