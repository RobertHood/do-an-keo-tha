#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Sends events from Unity to the web page hosting the WebGL build (the Suburbsim React app).
/// Each event is dispatched on window as a CustomEvent named "suburbsim:unity" with
/// detail = { type, payload } (see Assets/Plugins/WebGL/HostBridge.jslib).
/// Outside a WebGL build, or when no host page is listening, every call is a no-op.
/// </summary>
public static class HostBridge
{
    // Event types sent to the host page
    public const string EventReady = "ready";     // Unity finished loading and WebBridge accepts commands
    public const string EventLoaded = "loaded";   // A blueprint was applied to the scene. payload: { ok, loaded, total, error }
    public const string EventSave = "save";       // Save requested. payload: blueprint JSON. Host must answer WebBridge.OnHostSaveResult
    public const string EventState = "state";     // Editor state changed. payload: { blueprintName, itemCount, totalPrice, isSaving }
    public const string EventItems = "items";     // Placed items, prices, locks or selection changed. payload: { selectedId, totalPrice, items[] }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int SuburbsimHostIsAttached();
    [DllImport("__Internal")] private static extern void SuburbsimHostDispatch(string type, string payload);
#endif

    /// <summary>
    /// True when the page set window.__suburbsimHost = true before loading the build, i.e. a host
    /// is listening for events and will store saved blueprints.
    /// </summary>
    public static bool IsAttached
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return SuburbsimHostIsAttached() == 1;
#else
            return false;
#endif
        }
    }

    public static void Send(string type, string payload = "")
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!IsAttached) return;
        SuburbsimHostDispatch(type, payload ?? "");
#endif
    }
}
