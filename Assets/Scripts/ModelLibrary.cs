using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Where the 3D models come from.
/// - Remote (web builds): the host page sends the model catalog (WebBridge.LoadModelCatalog), the same data as
///   catalog.json in the model storage (R2) or the model database. Each model is a GLB file that is downloaded and
///   loaded with glTFast the first time it is needed, then copied for every further placement. Catalog card
///   thumbnails are downloaded too.
/// - Without the website (Play mode, or a build opened on its own) the catalog is loaded from Catalog Url.
/// Only remote models are used: if the catalog can't be loaded, the item catalog stays empty, which shows the
/// connection isn't working. The prefabs in the PriceCatalog asset are kept for the export tools and for offline
/// work in the editor (Use Local Models). Player builds leave them out (see SuburbsimModelLibraryBuild).
/// </summary>
[AddComponentMenu("Blueprint/Model Library")]
public class ModelLibrary : MonoBehaviour
{
    public static ModelLibrary Instance { get; private set; }

    [Tooltip("catalog.json to load when the host page doesn't send a catalog (Play mode, or a build opened without " +
             "the website). Relative model/thumbnail URLs are resolved against it.")]
    [SerializeField] private string catalogUrl = "https://assets.suburbsim.online/models/catalog.json";

    [Tooltip("Editor only: use the local prefabs from the PriceCatalog asset instead of the remote library (offline work).")]
    [SerializeField] private bool useLocalModels = false;

    [Tooltip("Texture filtering for loaded models (1 = off). Higher keeps ground-level textures sharp at an angle.")]
    [SerializeField] private int anisotropicLevel = 4;

#pragma warning disable 0649 // Filled by JsonUtility
    [Serializable]
    private class RemoteCatalog
    {
        public string format;
        public int version;
        public string baseUrl;
        public RemoteModel[] models;
    }

    [Serializable]
    private class RemoteModel
    {
        public string id;
        public string category;
        public string displayName;
        public string description;
        public double listedPrice;
        public PriceOffer[] offers;
        public BuildingMaterial[] materials;
        public string loai;
        public float frontYawOffset;
        public bool hasDoor;
        public Vector3 doorOffset;
        public Vector3 baseSize;
        public string modelUrl;
        public string thumbnailUrl;
    }
#pragma warning restore 0649

    private class Model
    {
        public string url;
        public GameObject template;
        public IDisposable import;
        public bool loading;
        public readonly List<Action<GameObject>> waiting = new List<Action<GameObject>>();
    }

    private readonly Dictionary<string, Model> models = new Dictionary<string, Model>(StringComparer.OrdinalIgnoreCase);
    private Transform templates;
    private bool thumbnailsChanged;

    /// <summary>Raised when a remote catalog has been applied (Catalog and ModelEstPrice.Catalog are set).</summary>
    public static event Action CatalogLoaded;

    /// <summary>Raised (at most once a frame) when catalog thumbnails finished downloading.</summary>
    public static event Action ThumbnailsChanged;

    /// <summary>True once a remote catalog is in use.</summary>
    public bool IsRemote { get; private set; }

    /// <summary>The remote catalog, or null while the local PriceCatalog asset is used.</summary>
    public PriceCatalog Catalog { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateIfMissing()
    {
        if (FindAnyObjectByType<ModelLibrary>(FindObjectsInactive.Include) == null)
        {
            new GameObject("ModelLibrary").AddComponent<ModelLibrary>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // Loaded models wait here, inactive, and are copied into the scene
        templates = new GameObject("Model Templates").transform;
        templates.SetParent(transform, false);
        templates.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (LocalModelsAllowed || IsRemote) return;

        // Nothing local: models (and their prices) only come from the remote catalog
        if (ModelEstPrice.Instance != null) ModelEstPrice.Instance.Catalog = null;

        if (HostBridge.IsAttached) return; // The website sends the catalog
        if (string.IsNullOrEmpty(catalogUrl))
        {
            Debug.LogWarning("[ModelLibrary] No Catalog Url set: the item catalog stays empty.");
            return;
        }
        StartCoroutine(FetchCatalog(catalogUrl));
    }

    private bool LocalModelsAllowed
    {
        get
        {
#if UNITY_EDITOR
            return useLocalModels;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// True while the local prefabs are in use (editor with Use Local Models, before any remote catalog arrived).
    /// </summary>
    public bool UsesLocalModels => LocalModelsAllowed && !IsRemote;

    private void LateUpdate()
    {
        if (!thumbnailsChanged) return;
        thumbnailsChanged = false;
        ThumbnailsChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (Model model in models.Values)
        {
            model.import?.Dispose();
            model.import = null;
        }
    }

    // ------------------------------------------------------------------ catalog

    /// <summary>
    /// Uses the model catalog in this JSON (format of catalog.json: { baseUrl, models: [ { id, category,
    /// displayName, modelUrl, thumbnailUrl, ... } ] }). Relative URLs are resolved against baseUrl.
    /// </summary>
    public void LoadCatalogJson(string json)
    {
        RemoteCatalog data = Parse(json);
        if (data == null)
        {
            UINotification.ShowError(NotificationTexts.ModelLibraryFailed);
            return;
        }
        Apply(data);
    }

    private IEnumerator FetchCatalog(string url)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[ModelLibrary] Could not load the catalog '{url}': {request.error}");
                UINotification.ShowError(NotificationTexts.ModelLibraryFailed);
                yield break;
            }

            RemoteCatalog data = Parse(request.downloadHandler.text);
            if (data == null)
            {
                UINotification.ShowError(NotificationTexts.ModelLibraryFailed);
                yield break;
            }
            if (string.IsNullOrEmpty(data.baseUrl)) data.baseUrl = url;
            Apply(data);
        }
    }

    private static RemoteCatalog Parse(string json)
    {
        try
        {
            RemoteCatalog data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<RemoteCatalog>(json);
            if (data != null && data.models != null) return data;
        }
        catch (ArgumentException e)
        {
            Debug.LogWarning("[ModelLibrary] Invalid catalog JSON: " + e.Message);
            return null;
        }
        Debug.LogWarning("[ModelLibrary] The catalog has no models list.");
        return null;
    }

    private void Apply(RemoteCatalog data)
    {
        PriceCatalog catalog = ScriptableObject.CreateInstance<PriceCatalog>();
        catalog.name = "Model Library";

        foreach (RemoteModel m in data.models)
        {
            if (m == null || string.IsNullOrEmpty(m.id)) continue;

            PriceCatalogEntry entry = new PriceCatalogEntry
            {
                prefabId = m.id,
                category = Enum.TryParse(m.category, true, out ModelCategory category) ? category : ModelCategory.Uncategorized,
                displayName = m.displayName,
                description = m.description,
                listedPrice = m.listedPrice,
                offers = m.offers != null ? new List<PriceOffer>(m.offers) : new List<PriceOffer>(),
                materials = m.materials != null ? new List<BuildingMaterial>(m.materials) : new List<BuildingMaterial>(),
                loai = FengShuiTypes.FromId(m.loai),
                frontYawOffset = m.frontYawOffset,
                hasDoor = m.hasDoor,
                doorOffset = m.doorOffset,
                baseSize = m.baseSize,
            };
            catalog.entries.Add(entry);

            string url = Resolve(data.baseUrl, m.modelUrl);
            if (!models.TryGetValue(m.id, out Model model) || model.url != url)
            {
                model?.import?.Dispose();
                if (model?.template != null) Destroy(model.template);
                models[m.id] = new Model { url = url };
            }

            string thumbnail = Resolve(data.baseUrl, m.thumbnailUrl);
            if (!string.IsNullOrEmpty(thumbnail)) StartCoroutine(LoadThumbnail(entry, thumbnail));
        }

        Catalog = catalog;
        IsRemote = true;
        if (ModelEstPrice.Instance != null) ModelEstPrice.Instance.Catalog = catalog;
        Debug.Log($"[ModelLibrary] Catalog loaded: {catalog.entries.Count} model(s).");
        CatalogLoaded?.Invoke();
    }

    private IEnumerator LoadThumbnail(PriceCatalogEntry entry, string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, true))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[ModelLibrary] Could not load the thumbnail '{url}': {request.error}");
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            texture.wrapMode = TextureWrapMode.Clamp;
            entry.icon = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            entry.icon.name = entry.Id;
            thumbnailsChanged = true;
        }
    }

    private static string Resolve(string baseUrl, string url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri absolute)) return absolute.AbsoluteUri;
        if (string.IsNullOrEmpty(baseUrl)) return url;
        try
        {
            return new Uri(new Uri(baseUrl, UriKind.Absolute), url).AbsoluteUri;
        }
        catch (UriFormatException)
        {
            return url;
        }
    }

    // ------------------------------------------------------------------ models

    /// <summary>
    /// True if the model is in the remote catalog.
    /// </summary>
    public bool HasRemote(string id) => IsRemote && !string.IsNullOrEmpty(id) && models.ContainsKey(id);

    /// <summary>
    /// False while the model still has to be downloaded first.
    /// </summary>
    public bool IsReady(string id)
    {
        return !HasRemote(id) || models[id].template != null;
    }

    /// <summary>
    /// Gives the prefab (local) or the loaded template (remote) to copy for this model ID: right away if it is
    /// ready, otherwise once it has been downloaded. Gives null if the model is unknown or could not be loaded.
    /// </summary>
    public void Get(string id, Action<GameObject> done)
    {
        if (done == null) return;

        if (HasRemote(id))
        {
            Model model = models[id];
            if (model.template != null)
            {
                done(model.template);
                return;
            }
            model.waiting.Add(done);
            if (!model.loading) LoadModel(id, model);
            return;
        }

        if (!LocalModelsAllowed)
        {
            done(null); // Not in the remote catalog
            return;
        }
        SpawnInModel spawner = FindAnyObjectByType<SpawnInModel>();
        done(spawner != null ? spawner.FindPrefab(id) : null);
    }

    private async void LoadModel(string id, Model model)
    {
        model.loading = true;
        GLTFast.GltfImport gltf = new GLTFast.GltfImport();
        GameObject root = null;
        bool ok = false;

        try
        {
            GLTFast.ImportSettings settings = new GLTFast.ImportSettings
            {
                GenerateMipMaps = true,
                DefaultMinFilterMode = GLTFast.Schema.Sampler.MinFilterMode.LinearMipmapLinear,
                AnisotropicFilterLevel = Mathf.Max(1, anisotropicLevel),
            };
            ok = await gltf.Load(model.url, settings);
            if (ok && this != null)
            {
                root = new GameObject(id);
                root.transform.SetParent(templates, false);
                ok = await gltf.InstantiateMainSceneAsync(root.transform);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            ok = false;
        }

        if (this == null)
        {
            gltf.Dispose(); // Play mode ended while loading
            return;
        }

        model.loading = false;
        if (ok && root != null)
        {
            model.template = root;
            model.import = gltf;
        }
        else
        {
            Debug.LogWarning($"[ModelLibrary] Could not load model '{id}' from '{model.url}'.");
            if (root != null) Destroy(root);
            gltf.Dispose();
            root = null;
        }

        Action<GameObject>[] callbacks = model.waiting.ToArray();
        model.waiting.Clear();
        foreach (Action<GameObject> callback in callbacks)
        {
            try
            {
                callback(root);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
