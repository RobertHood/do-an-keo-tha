using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The commands the host web page (Suburbsim React app) can send to the WebGL build, through
/// unityInstance.SendMessage("WebBridge", "&lt;Method&gt;", "&lt;string&gt;"):
/// - LoadBlueprint(json)         Replace the scene with this blueprint (keeps its id, so saves overwrite it)
/// - SetBlueprintName(name)      Rename the blueprint in use
/// - Save("")                    Save now (same as the Save button)
/// - OnHostSaveResult("1"/"0")   The page's answer to a "save" event
/// - SelectModel(id)             Select a placed model (id = the "id" of an item in the "items" event)
/// - DeselectModel("")           Clear the selection
/// - SetModelLocked(json)        { "id": 3, "locked": true }
/// - SelectOffer(json)           { "id": 3, "store": "Store name" } (use that store's price)
/// - SetCustomPrice(json)        { "id": 3, "price": 150000 }
/// - ClearCustomPrice(id)        Go back to market prices
/// It also sends the page the editor state ("state") and the price panel data ("items") whenever they change.
/// Created automatically after the first scene loads; no scene setup needed.
/// </summary>
[AddComponentMenu("Blueprint/Web Bridge")]
public class WebBridge : MonoBehaviour
{
    public const string ObjectName = "WebBridge";

    private const string PriceSourceCustom = "custom";
    private const string PriceSourceStore = "store";
    private const string PriceSourceListed = "listed";
    private const string PriceSourceNone = "none";

    public static WebBridge Instance { get; private set; }

    private string lastStateJson;
    private string lastItemsJson;
    private bool itemsDirty = true;
    private Transform lastSelected;
    private ModelEstPrice subscribedPrices;

    [Serializable]
    private class LoadResult
    {
        public bool ok;
        public int loaded;
        public int total;
        public string error;
    }

    [Serializable]
    private class EditorState
    {
        public string blueprintName;
        public int itemCount;
        public double totalPrice;
        public bool isSaving;
    }

    [Serializable]
    private class ItemInfo
    {
        public int id;
        public string prefabId;
        public string name;
        public string description;
        public bool inCatalog;
        public bool hasPrice;
        public double price;
        public string priceSource;
        public string store;
        public double listedPrice;
        public PriceOffer[] offers;
        public bool useCustomPrice;
        public double customPrice;
        public bool isLocked;
    }

    [Serializable]
    private class ItemsState
    {
        public int selectedId;
        public double totalPrice;
        public ItemInfo[] items;
    }

    [Serializable]
    private class LockCommand
    {
        public int id;
        public bool locked;
    }

    [Serializable]
    private class OfferCommand
    {
        public int id;
        public string store;
    }

    [Serializable]
    private class CustomPriceCommand
    {
        public int id;
        public double price;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateInstance()
    {
        if (Instance != null || FindAnyObjectByType<WebBridge>() != null) return;

        GameObject go = new GameObject(ObjectName);
        go.AddComponent<WebBridge>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        gameObject.name = ObjectName; // SendMessage finds the bridge by this name

#if UNITY_WEBGL && !UNITY_EDITOR
        // Let the page's own inputs (project name, forms) receive key presses. The canvas still gets
        // WASD / shortcuts while it has focus.
        WebGLInput.captureAllKeyboardInput = false;
#endif
    }

    private void OnEnable()
    {
        ModelLock.LockChanged += OnLockChanged;
    }

    private void OnDisable()
    {
        ModelLock.LockChanged -= OnLockChanged;
        if (subscribedPrices != null) subscribedPrices.Changed -= MarkItemsDirty;
        subscribedPrices = null;
    }

    private void Start()
    {
        HostBridge.Send(HostBridge.EventReady);
    }

    private void LateUpdate()
    {
        WebBlueprintStorage.CheckTimeout();
        SubscribeToPrices();
        SendStateIfChanged();
        SendItemsIfChanged();
    }

    /// <summary>
    /// Replaces the scene with the given blueprint and makes it the blueprint in use.
    /// </summary>
    public void LoadBlueprint(string json)
    {
        LoadResult result = new LoadResult();

        if (!BlueprintData.TryFromJson(json, out BlueprintData data))
        {
            result.error = "invalid-blueprint";
        }
        else if (BlueprintExport.Instance == null || BlueprintSave.Instance == null)
        {
            result.error = "missing-components";
            Debug.LogError("[WebBridge] BlueprintExport and BlueprintSave must be in the scene.");
        }
        else
        {
            result.total = data.models.Count;
            bool started = BlueprintExport.Instance.ApplyToScene(data, loaded =>
            {
                // Models may still have to be downloaded, so the result is sent once all of them are in
                result.ok = true;
                result.loaded = loaded;
                HostBridge.Send(HostBridge.EventLoaded, JsonUtility.ToJson(result));
            });
            if (started)
            {
                BlueprintSave.Instance.OpenExisting(data);
                return;
            }
            result.error = "apply-failed";
        }

        HostBridge.Send(HostBridge.EventLoaded, JsonUtility.ToJson(result));
    }

    /// <summary>
    /// Uses the model catalog sent by the host page (catalog.json from the model storage or the model database,
    /// with baseUrl set to where relative model/thumbnail URLs are resolved). Must arrive before LoadBlueprint.
    /// </summary>
    public void LoadModelCatalog(string json)
    {
        if (ModelLibrary.Instance != null) ModelLibrary.Instance.LoadCatalogJson(json);
    }

    public void SetBlueprintName(string blueprintName)
    {
        if (BlueprintSave.Instance != null) BlueprintSave.Instance.SetBlueprintName(blueprintName);
    }

    public void Save(string unused)
    {
        if (BlueprintSave.Instance != null) BlueprintSave.Instance.Save();
    }

    public void OnHostSaveResult(string result)
    {
        WebBlueprintStorage.Complete(result == "1" || string.Equals(result, "true", StringComparison.OrdinalIgnoreCase));
    }

    public void SelectModel(string id)
    {
        DraggableModel model = FindModel(id);
        if (model == null || ModelMovement.Instance == null) return;
        ModelMovement.Instance.SelectModel(model.transform);
    }

    public void DeselectModel(string unused)
    {
        if (ModelMovement.Instance != null) ModelMovement.Instance.DeselectModel();
    }

    public void SetModelLocked(string json)
    {
        LockCommand command = Parse<LockCommand>(json);
        if (command == null) return;
        ModelLock.SetLocked(FindModel(command.id), command.locked);
    }

    public void SelectOffer(string json)
    {
        OfferCommand command = Parse<OfferCommand>(json);
        if (command == null || ModelEstPrice.Instance == null) return;
        ModelEstPrice.Instance.SelectOffer(FindModel(command.id), command.store);
    }

    public void SetCustomPrice(string json)
    {
        CustomPriceCommand command = Parse<CustomPriceCommand>(json);
        if (command == null || ModelEstPrice.Instance == null) return;
        ModelEstPrice.Instance.SetCustomPrice(FindModel(command.id), command.price);
    }

    public void ClearCustomPrice(string id)
    {
        if (ModelEstPrice.Instance == null) return;
        ModelEstPrice.Instance.ClearCustomPrice(FindModel(id));
    }

    private void SubscribeToPrices()
    {
        ModelEstPrice prices = ModelEstPrice.Instance;
        if (prices == subscribedPrices) return;

        if (subscribedPrices != null) subscribedPrices.Changed -= MarkItemsDirty;
        subscribedPrices = prices;
        if (subscribedPrices != null) subscribedPrices.Changed += MarkItemsDirty;
        itemsDirty = true;
    }

    private void MarkItemsDirty() => itemsDirty = true;

    private void OnLockChanged(DraggableModel model) => itemsDirty = true;

    private void SendStateIfChanged()
    {
        if (!HostBridge.IsAttached) return;

        EditorState state = new EditorState
        {
            blueprintName = BlueprintSave.Instance != null ? BlueprintSave.Instance.CurrentBlueprintName : null,
            itemCount = ModelEstPrice.Instance != null ? ModelEstPrice.Instance.ItemCount : 0,
            totalPrice = ModelEstPrice.Instance != null ? ModelEstPrice.Instance.TotalPrice : 0,
            isSaving = BlueprintSave.Instance != null && BlueprintSave.Instance.IsSaving
        };

        string json = JsonUtility.ToJson(state);
        if (json == lastStateJson) return;

        lastStateJson = json;
        HostBridge.Send(HostBridge.EventState, json);
    }

    private void SendItemsIfChanged()
    {
        if (!HostBridge.IsAttached) return;

        Transform selected = ModelMovement.Instance != null ? ModelMovement.Instance.SelectedModel : null;
        if (selected != lastSelected)
        {
            lastSelected = selected;
            itemsDirty = true;
        }

        if (!itemsDirty) return;
        itemsDirty = false;

        string json = JsonUtility.ToJson(BuildItemsState(selected));
        if (json == lastItemsJson) return;

        lastItemsJson = json;
        HostBridge.Send(HostBridge.EventItems, json);
    }

    private static ItemsState BuildItemsState(Transform selected)
    {
        ModelEstPrice prices = ModelEstPrice.Instance;
        DraggableModel selectedModel = selected != null ? selected.GetComponent<DraggableModel>() : null;

        ItemsState state = new ItemsState
        {
            selectedId = selectedModel != null ? selectedModel.RuntimeId : 0,
            totalPrice = prices != null ? prices.TotalPrice : 0,
            items = Array.Empty<ItemInfo>()
        };
        if (prices == null) return state;

        List<ItemInfo> items = new List<ItemInfo>(prices.Items.Count);
        foreach (DraggableModel model in prices.Items)
        {
            if (model == null) continue;

            PriceCatalogEntry entry = prices.FindEntry(model);
            PriceOffer chosen = entry != null ? prices.GetChosenOffer(model, entry) : null;
            bool hasPrice = prices.TryGetPrice(model, out double price);

            string source = PriceSourceNone;
            if (model.useCustomPrice) source = PriceSourceCustom;
            else if (chosen != null) source = PriceSourceStore;
            else if (entry != null) source = PriceSourceListed;

            items.Add(new ItemInfo
            {
                id = model.RuntimeId,
                prefabId = model.prefabId,
                name = ModelEstPrice.GetDisplayName(model, entry),
                description = entry != null ? entry.description : "",
                inCatalog = entry != null,
                hasPrice = hasPrice,
                price = price,
                priceSource = source,
                store = chosen != null && !model.useCustomPrice ? chosen.storeName : "",
                listedPrice = entry != null ? entry.listedPrice : 0,
                offers = ModelEstPrice.GetSortedOffers(entry).ToArray(),
                useCustomPrice = model.useCustomPrice,
                customPrice = model.customPrice,
                isLocked = model.isLocked
            });
        }

        state.items = items.ToArray();
        return state;
    }

    private static DraggableModel FindModel(string id)
    {
        return int.TryParse(id, out int runtimeId) ? FindModel(runtimeId) : null;
    }

    private static DraggableModel FindModel(int runtimeId)
    {
        if (ModelEstPrice.Instance == null) return null;

        foreach (DraggableModel model in ModelEstPrice.Instance.Items)
        {
            if (model != null && model.RuntimeId == runtimeId) return model;
        }
        return null;
    }

    private static T Parse<T>(string json) where T : class
    {
        try
        {
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<T>(json);
        }
        catch (ArgumentException)
        {
            Debug.LogWarning("[WebBridge] Invalid command JSON: " + json);
            return null;
        }
    }
}
