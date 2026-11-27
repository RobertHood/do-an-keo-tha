using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The notification box: short messages for the user, with an icon that matches the kind of message.
/// - ShowInfo(msg):       a helpful message (info icon), hides itself after a few seconds.
/// - ShowError(msg):      an error, e.g. a function used without selecting a model (danger icon).
/// - ShowPrompt(msg):     a step-by-step instruction (info icon) that stays until ClearPrompt() / Hide(),
///                        e.g. "choose the first model". Timed messages shown meanwhile return to it afterwards.
/// - ShowTipOnce(key, msg): explains a function the first time it is used (remembered between visits).
/// Setup: put this on the notification box and assign the icon Image, the message text and the two sprites.
/// The box can start active or inactive; it hides itself until there is something to show.
/// All methods are static and safe to call when no box is in the scene (the message is just logged).
/// </summary>
[AddComponentMenu("UI/UI Notification")]
public class UINotification : MonoBehaviour
{
    public enum Kind { Info, Error }

    private const string TipPrefKeyPrefix = "UINotification.TipSeen.";

    public static UINotification Instance { get; private set; }

    [Header("References")]
    [Tooltip("The object shown / hidden. If empty, this GameObject.")]
    [SerializeField] private GameObject box;

    [Tooltip("The notification's icon. Its sprite switches between the info and error sprites.")]
    [SerializeField] private Image iconImage;

    [Tooltip("Optional. A shape behind the icon, tinted per kind (light blue for info, light red for errors).")]
    [SerializeField] private Image iconBackground;

    [SerializeField] private TMP_Text messageText;

    [Tooltip("Optional. Shows the info / error title.")]
    [SerializeField] private TMP_Text titleText;

    [Tooltip("Optional. Closes the notification.")]
    [SerializeField] private Button closeButton;

    [Header("Icons")]
    [Tooltip("Helper / instruction messages (info-outline).")]
    [SerializeField] private Sprite infoSprite;

    [Tooltip("Error messages (danger).")]
    [SerializeField] private Sprite errorSprite;

    [SerializeField] private Color infoIconBackground = new Color(0.92f, 0.96f, 1f, 1f);
    [SerializeField] private Color errorIconBackground = new Color(1f, 0.95f, 0.95f, 1f);

    [Header("Titles (only used if Title Text is assigned)")]
    [SerializeField] private string infoTitle = "Hướng dẫn";
    [SerializeField] private string errorTitle = "Chưa thực hiện được";

    [Header("Timing")]
    [SerializeField] private float infoDuration = 4f;
    [SerializeField] private float errorDuration = 3.5f;

    [Tooltip("Extra seconds per 10 characters, so longer messages stay up long enough to read.")]
    [SerializeField] private float extraSecondsPer10Chars = 0.25f;

    [SerializeField] private float fadeDuration = 0.15f;

    [Header("Timer Bar")]
    [Tooltip("Optional. Shrinks from full to empty while a timed message is shown. Uses Fill Amount if the Image type " +
             "is Filled, otherwise scales the bar's width (set its pivot X to 0 so it shrinks towards the left). " +
             "If empty and 'Create Timer Bar' is on, a thin bar is created under the box.")]
    [SerializeField] private Image timerBar;

    [SerializeField] private bool createTimerBar = true;

    [Tooltip("Height of the created bar, in UI units.")]
    [SerializeField] private float timerBarHeight = 4f;

    [Tooltip("Gap between the box and the created bar. 0 = flush with the bottom edge, negative = inside the box.")]
    [SerializeField] private float timerBarGap = 0f;

    [Tooltip("Space left and right of the created bar.")]
    [SerializeField] private float timerBarInset = 0f;

    [Tooltip("Optional sprite for the created bar (e.g. a pill shape, drawn Sliced).")]
    [SerializeField] private Sprite timerBarSprite;

    [SerializeField] private bool tintTimerBarByKind = true;
    [Tooltip("Default matches the blue info-outline sprite.")]
    [SerializeField] private Color infoTimerColor = new Color(0f, 0.4f, 0.9f, 1f);

    [Tooltip("Default matches the red danger sprite.")]
    [SerializeField] private Color errorTimerColor = new Color(0.9f, 0.05f, 0.05f, 1f);

    [Header("First-time tips")]
    [SerializeField] private bool showFirstTimeTips = true;

    [Tooltip("Remember shown tips between visits. If off, tips show once per session.")]
    [SerializeField] private bool rememberTips = true;

    private CanvasGroup canvasGroup;
    private string promptMessage;
    private float hideAt = -1f;
    private float shownDuration;
    private float targetAlpha;
    private bool initialized;
    private readonly System.Collections.Generic.HashSet<string> sessionTips = new System.Collections.Generic.HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void FindInstance()
    {
        if (Instance != null) return;

        // Also finds a box that starts inactive (its Awake doesn't run until it is shown)
        UINotification found = FindAnyObjectByType<UINotification>(FindObjectsInactive.Include);
        if (found != null) found.Initialize();
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Initialize()
    {
        if (initialized) return;

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[UINotification] More than one notification box in the scene. Using the first one.");
            return;
        }

        initialized = true;
        Instance = this;

        if (box == null) box = gameObject;
        if (closeButton == null) closeButton = box.GetComponentInChildren<Button>(true);
        if (iconImage == null) iconImage = FindIcon();
        if (titleText == null) titleText = FindText(t => t.name.IndexOf("title", System.StringComparison.OrdinalIgnoreCase) >= 0);
        if (messageText == null) messageText = FindText(t => t != titleText);

        canvasGroup = box.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = box.AddComponent<CanvasGroup>();

        if (closeButton != null) closeButton.onClick.AddListener(() => Hide());

        if (timerBar == null && createTimerBar) timerBar = CreateTimerBar();
        SetTimerBar(-1f);

        canvasGroup.alpha = 0f;
        targetAlpha = 0f;
        box.SetActive(false);
    }

    private void Update()
    {
        float step = fadeDuration > 0f ? Time.unscaledDeltaTime / fadeDuration : 1f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, step);

        if (hideAt > 0f && shownDuration > 0f)
        {
            SetTimerBar(Mathf.Clamp01((hideAt - Time.unscaledTime) / shownDuration));
        }

        if (hideAt > 0f && Time.unscaledTime >= hideAt)
        {
            hideAt = -1f;
            if (!string.IsNullOrEmpty(promptMessage))
            {
                Display(Kind.Info, promptMessage, -1f); // Back to the step the user is on
            }
            else
            {
                targetAlpha = 0f;
            }
        }

        if (targetAlpha <= 0f && canvasGroup.alpha <= 0f && box.activeSelf)
        {
            box.SetActive(false);
        }
    }

    public static void ShowInfo(string message) => Show(Kind.Info, message);

    public static void ShowError(string message) => Show(Kind.Error, message);

    public static void Show(Kind kind, string message)
    {
        if (!TryGetInstance(kind, message)) return;
        Instance.Display(kind, message, Instance.GetDuration(kind, message));
    }

    /// <summary>
    /// Shows an instruction that stays on screen until ClearPrompt() or Hide().
    /// </summary>
    public static void ShowPrompt(string message)
    {
        if (!TryGetInstance(Kind.Info, message)) return;
        Instance.promptMessage = message;
        Instance.Display(Kind.Info, message, -1f);
    }

    /// <summary>
    /// Removes the current instruction. A timed message on screen stays until its time is up.
    /// </summary>
    public static void ClearPrompt()
    {
        if (Instance == null) return;
        bool showingPrompt = Instance.hideAt < 0f;
        Instance.promptMessage = null;
        if (showingPrompt) Instance.targetAlpha = 0f;
    }

    /// <summary>
    /// Shows a tip the first time it is requested for this key. Returns true if it was shown.
    /// </summary>
    public static bool ShowTipOnce(string key, string message)
    {
        if (Instance == null || !Instance.showFirstTimeTips || string.IsNullOrEmpty(key)) return false;
        if (Instance.HasSeenTip(key)) return false;

        Instance.MarkTipSeen(key);
        ShowInfo(message);
        return true;
    }

    public static void Hide()
    {
        if (Instance == null) return;
        Instance.promptMessage = null;
        Instance.hideAt = -1f;
        Instance.targetAlpha = 0f;
    }

    /// <summary>
    /// Shows every first-time tip again. (Can be connected to a UI Button OnClick, e.g. a "help" button.)
    /// </summary>
    [ContextMenu("Reset First-Time Tips")]
    public void ResetTips()
    {
        sessionTips.Clear();
        foreach (string key in NotificationTips.AllKeys)
        {
            PlayerPrefs.DeleteKey(TipPrefKeyPrefix + key);
        }
        PlayerPrefs.Save();
    }

    private void Display(Kind kind, string message, float duration)
    {
        if (iconImage != null)
        {
            Sprite sprite = kind == Kind.Error ? errorSprite : infoSprite;
            if (sprite != null) iconImage.sprite = sprite;
        }

        if (messageText != null) messageText.text = message;
        if (titleText != null) titleText.text = kind == Kind.Error ? errorTitle : infoTitle;

        if (iconBackground != null)
        {
            iconBackground.color = kind == Kind.Error ? errorIconBackground : infoIconBackground;
        }

        if (timerBar != null && tintTimerBarByKind)
        {
            timerBar.color = kind == Kind.Error ? errorTimerColor : infoTimerColor;
        }

        box.SetActive(true);
        targetAlpha = 1f;
        shownDuration = duration;
        hideAt = duration > 0f ? Time.unscaledTime + duration : -1f;
        SetTimerBar(duration > 0f ? 1f : -1f); // Step-by-step prompts have no timer
    }

    /// <summary>
    /// 1 = full, 0 = empty, below 0 = hidden.
    /// </summary>
    private void SetTimerBar(float amount)
    {
        if (timerBar == null) return;

        bool visible = amount >= 0f;
        if (timerBar.gameObject.activeSelf != visible) timerBar.gameObject.SetActive(visible);
        if (!visible) return;

        if (timerBar.type == Image.Type.Filled)
        {
            timerBar.fillAmount = amount;
        }
        else
        {
            Vector3 scale = timerBar.rectTransform.localScale;
            scale.x = amount;
            timerBar.rectTransform.localScale = scale;
        }
    }

    /// <summary>
    /// A thin bar along the bottom edge of the box, outside any layout group, shrinking towards the left.
    /// </summary>
    private Image CreateTimerBar()
    {
        if (!(box.transform is RectTransform)) return null;

        GameObject barObj = new GameObject("_TimerBar", typeof(RectTransform));
        barObj.transform.SetParent(box.transform, false);
        barObj.transform.SetAsLastSibling();

        LayoutElement layout = barObj.AddComponent<LayoutElement>();
        layout.ignoreLayout = true; // Don't let the box's layout group move it

        RectTransform rect = (RectTransform)barObj.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = new Vector2(timerBarInset, 0f);
        rect.offsetMax = new Vector2(-timerBarInset, 0f);
        rect.anchoredPosition = new Vector2(timerBarInset, -timerBarGap);
        rect.sizeDelta = new Vector2(-2f * timerBarInset, timerBarHeight);

        Image bar = barObj.AddComponent<Image>();
        bar.color = infoTimerColor;
        bar.raycastTarget = false;
        if (timerBarSprite != null)
        {
            bar.sprite = timerBarSprite;
            bar.type = Image.Type.Sliced;
        }
        return bar;
    }

    private float GetDuration(Kind kind, string message)
    {
        float baseDuration = kind == Kind.Error ? errorDuration : infoDuration;
        int length = message != null ? message.Length : 0;
        return baseDuration + extraSecondsPer10Chars * (length / 10f);
    }

    private bool HasSeenTip(string key)
    {
        if (sessionTips.Contains(key)) return true;
        return rememberTips && PlayerPrefs.GetInt(TipPrefKeyPrefix + key, 0) == 1;
    }

    private void MarkTipSeen(string key)
    {
        sessionTips.Add(key);
        if (!rememberTips) return;
        PlayerPrefs.SetInt(TipPrefKeyPrefix + key, 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// The first child Image that isn't the box's own background.
    /// </summary>
    private Image FindIcon()
    {
        foreach (Image image in box.GetComponentsInChildren<Image>(true))
        {
            // Names starting with "_" are decoration added by the UI theme tool (card, shadow, outline)
            if (image.gameObject != box && image != timerBar && image != iconBackground && !image.name.StartsWith("_") &&
                (closeButton == null || !image.transform.IsChildOf(closeButton.transform)))
            {
                return image;
            }
        }
        return null;
    }

    /// <summary>
    /// The first text under the box that matches, skipping the close button's label.
    /// </summary>
    private TMP_Text FindText(System.Predicate<TMP_Text> match)
    {
        foreach (TMP_Text text in box.GetComponentsInChildren<TMP_Text>(true))
        {
            if (closeButton != null && text.transform.IsChildOf(closeButton.transform)) continue;
            if (match(text)) return text;
        }
        return null;
    }

    private static bool TryGetInstance(Kind kind, string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        if (Instance != null) return true;

        Debug.Log($"[UINotification] ({kind}) {message}");
        return false;
    }
}

/// <summary>
/// Vietnamese texts shown by the notification box.
/// </summary>
public static class NotificationTexts
{
    public const string SelectModelFirst = "Bạn cần chọn một mô hình để thực hiện được thao tác này.";
    public const string ModelLocked = "Mô hình này đang bị khóa. Hãy mở khóa để thực hiện thao tác này.";

    public const string MeasurePickFirst = "Chọn mô hình thứ nhất để đo khoảng cách. Nhấn Esc để hủy.";
    public const string MeasurePickSecond = "Chọn mô hình thứ hai để đo khoảng cách.";
    public const string MeasurePickDifferent = "Hãy chọn một mô hình khác với mô hình thứ nhất.";
    public const string MeasureResult = "Khoảng cách đo đạc được là {0}.";
    public const string MeasureCancelled = "Đã hủy đo khoảng cách.";

    public const string Locked = "Đã khóa mô hình.";
    public const string Unlocked = "Đã mở khóa mô hình.";
    public const string Deleted = "Đã xóa mô hình.";
    public const string Saved = "Đã lưu bản thiết kế.";
    public const string SaveFailed = "Lưu bản thiết kế thất bại. Vui lòng thử lại.";
    public const string NothingToSave = "Không lưu được: không tìm thấy khu vực chứa mô hình.";
    public const string Exported = "Đã tải bản thiết kế xuống máy.";
    public const string ExportFailed = "Xuất bản thiết kế thất bại.";
    public const string Imported = "Đã mở bản thiết kế \"{0}\".";
    public const string ImportInvalid = "Tệp đã chọn không phải bản thiết kế hợp lệ.";
    public const string ImportNotFound = "Không tìm thấy tệp bản thiết kế nào trong Downloads hoặc Documents/Blueprints.";

    public const string LandOutside = "Bạn đang đặt \"{0}\" ra ngoài phần đất được phân bổ. Hãy kéo mô hình vào bên trong ranh giới thửa đất.";
    public const string LandOutsideMany = "Có {0} mô hình đang nằm ngoài phần đất được phân bổ. Hãy kéo chúng vào bên trong ranh giới thửa đất.";
    public const string LandChangedOutside = "Thửa đất đã thay đổi: {0} mô hình đang nằm ngoài ranh giới mới.";
    public const string LandAdjusted = "Một số kích thước đã được điều chỉnh: {0}.";
    public const string LandInvalidNumber = "Vui lòng nhập một số hợp lệ (ví dụ: 12 hoặc 12,5).";
    public const string CompassNorth = "Đã xoay camera về hướng Bắc.";

    public const string ModelLoading = "Đang tải mô hình \"{0}\"...";
    public const string ModelLoadFailed = "Không tải được mô hình \"{0}\". Vui lòng kiểm tra kết nối mạng và thử lại.";
    public const string ModelLibraryFailed = "Không tải được thư viện mô hình. Vui lòng tải lại trang.";
    public const string BlueprintStillLoading = "Bản thiết kế vẫn đang được tải, vui lòng đợi trong giây lát.";
}

/// <summary>
/// First-time tips for each toolbar function (key + Vietnamese text).
/// </summary>
public static class NotificationTips
{
    public const string ResizeKey = "resize";
    public const string Resize = "Kéo các nút ở góc khung để thay đổi kích cỡ mô hình. Giữ Shift để thay đổi theo từng bước 10%, nhấn Esc để hủy.";

    public const string RotateKey = "rotate";
    public const string Rotate = "Kéo vòng tròn quanh mô hình để xoay. Giữ Shift để xoay theo từng bước 15°, nhấn Esc để hủy.";

    public const string DeleteKey = "delete";
    public const string Delete = "Đã xóa mô hình khỏi khu đất. Mô hình đang khóa sẽ không thể bị xóa.";

    public const string LockKey = "lock";
    public const string Lock = "Đã khóa mô hình: mô hình sẽ không bị kéo, xoay, đổi kích cỡ hay xóa nhầm. Nhấn nút khóa lần nữa để mở khóa.";

    public const string SaveKey = "save";
    public const string Save = "Đã lưu bản thiết kế. Hãy lưu thường xuyên để không mất các thay đổi.";

    public const string ExportKey = "export";
    public const string Export = "Bản thiết kế đã được tải xuống dưới dạng tệp .blueprint. Dùng \"Mở BTK khác\" để mở lại tệp này.";

    public const string ImportKey = "import";
    public const string Import = "Chọn một tệp .blueprint đã xuất trước đó để mở lại bản thiết kế. Bản thiết kế hiện tại sẽ được thay thế.";

    public const string LandKey = "land";
    public const string Land = "Chọn hình dạng thửa đất và nhập kích thước gần đúng (mét). Cạnh màu cam là mặt tiền (giáp đường): chọn hướng mặt tiền để kiểm tra phong thủy. Mô hình đặt ra ngoài ranh giới sẽ nhấp nháy đỏ.";

    public const string DirectionKey = "direction";
    public const string Direction = "Mũi tên trên mặt đất chỉ hướng mặt trước của mô hình (với nhà là hướng cửa chính). Dùng \"Đổi chiều\" để xoay mô hình; la bàn ở góc phải cho biết hướng Bắc.";

    public const string ChooseHouseKey = "choose-house";
    public const string ChooseHouse = "Hãy bắt đầu bằng việc chọn một mẫu nhà trong mục \"Nhà ở\", sau đó thêm cây xanh, hàng rào và các vật trang trí xung quanh.";

    public static readonly string[] AllKeys = { ResizeKey, RotateKey, DeleteKey, LockKey, SaveKey, ExportKey, ImportKey, ChooseHouseKey, LandKey, DirectionKey };
}
