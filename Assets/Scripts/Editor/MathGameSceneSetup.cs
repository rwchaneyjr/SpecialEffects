#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.VFX;
using TMPro;

/// <summary>
/// One-click scene wiring for the math clicker game.
/// Menu: Window → Math Game → Setup Scene
/// </summary>
public static class MathGameSceneSetup
{
    const string VfxAssetPath = "Assets/New VFX.vfx";
    const string CorrectVfxPrefabPath = "Assets/Prefabs/CorrectAnswerVFX.prefab";

    [MenuItem("Window/Math Game/Setup Scene")]
    [MenuItem("Tools/Math Game/Setup Scene")]
    public static void SetupScene()
    {
        EnsureCamera();
        EnsureEventSystem();
        var canvas = EnsureCanvas(out var equationText, out var feedbackText);
        var spawnArea = EnsureSpawnArea();
        var numberBuilder = EnsureNumberBuilder();
        var vfxPrefab = EnsureCorrectVfxPrefab();
        var gameManager = EnsureGameManager(equationText, feedbackText, numberBuilder, spawnArea, vfxPrefab);
        EnsureMousePicker(gameManager.gameObject);

        Selection.activeGameObject = gameManager.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(
            "Math Game scene setup complete.\n" +
            "1) Assign digit prefabs 0-9 on NumberBuilder (or leave empty to use TMP numbers).\n" +
            "2) Press Play. Click answers with the mouse.\n" +
            "3) Drop your exported number prefabs into Assets/Prefabs when ready.");
    }

    [MenuItem("Window/Math Game/Create Correct Answer VFX Prefab")]
    [MenuItem("Tools/Math Game/Create Correct Answer VFX Prefab")]
    public static void CreateVfxPrefabOnly()
    {
        var prefab = EnsureCorrectVfxPrefab();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }

    static void EnsureCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            cam = go.AddComponent<Camera>();
            go.tag = "MainCamera";
            go.AddComponent<AudioListener>();
        }

        cam.transform.position = new Vector3(0f, 1.2f, -8f);
        cam.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.22f, 0.08f, 0.08f);
    }

    static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
            return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    static Canvas EnsureCanvas(out TextMeshProUGUI equationText, out TextMeshProUGUI feedbackText)
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        equationText = FindOrCreateTmp(canvas.transform, "EquationText", new Vector2(0f, 380f), 96f, TextAlignmentOptions.Center);
        feedbackText = FindOrCreateTmp(canvas.transform, "FeedbackText", new Vector2(0f, -420f), 64f, TextAlignmentOptions.Center);
        feedbackText.color = new Color(1f, 0.85f, 0.4f);
        equationText.text = "2 + 7";
        feedbackText.text = string.Empty;
        return canvas;
    }

    static TextMeshProUGUI FindOrCreateTmp(Transform parent, string name, Vector2 anchoredPos, float fontSize, TextAlignmentOptions align)
    {
        var existing = parent.Find(name);
        TextMeshProUGUI tmp;
        if (existing != null)
        {
            tmp = existing.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
                return tmp;
        }

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900f, 160f);
        rt.anchoredPosition = anchoredPos;

        tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.enableWordWrapping = false;
        tmp.text = string.Empty;

        // Use default TMP resources if available.
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        return tmp;
    }

    static Transform EnsureSpawnArea()
    {
        var existing = GameObject.Find("SpawnArea");
        if (existing != null)
            return existing.transform;

        var go = new GameObject("SpawnArea");
        go.transform.position = new Vector3(0f, 0.5f, 0f);
        return go.transform;
    }

    static NumberVisualBuilder EnsureNumberBuilder()
    {
        var existing = Object.FindObjectOfType<NumberVisualBuilder>();
        if (existing != null)
            return existing;

        var go = new GameObject("NumberBuilder");
        return go.AddComponent<NumberVisualBuilder>();
    }

    static CorrectAnswerVFX EnsureCorrectVfxPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CorrectVfxPrefabPath);
        if (existing != null)
        {
            var c = existing.GetComponent<CorrectAnswerVFX>();
            if (c != null)
                return c;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        var temp = new GameObject("CorrectAnswerVFX");
        var ve = temp.AddComponent<VisualEffect>();
        var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(VfxAssetPath);
        if (asset != null)
            ve.visualEffectAsset = asset;

        var controller = temp.AddComponent<CorrectAnswerVFX>();

        // Wire serialized refs via SerializedObject so private fields stick on the prefab.
        var so = new SerializedObject(controller);
        so.FindProperty("visualEffect").objectReferenceValue = ve;
        so.FindProperty("vfxAsset").objectReferenceValue = asset;
        so.ApplyModifiedPropertiesWithoutUndo();

        var prefabRoot = PrefabUtility.SaveAsPrefabAsset(temp, CorrectVfxPrefabPath);
        Object.DestroyImmediate(temp);
        AssetDatabase.SaveAssets();

        return prefabRoot.GetComponent<CorrectAnswerVFX>();
    }

    static MathGameManager EnsureGameManager(
        TextMeshProUGUI equationText,
        TextMeshProUGUI feedbackText,
        NumberVisualBuilder numberBuilder,
        Transform spawnArea,
        CorrectAnswerVFX vfxPrefab)
    {
        var existing = Object.FindObjectOfType<MathGameManager>();
        GameObject go = existing != null ? existing.gameObject : new GameObject("GameManager");
        var manager = existing != null ? existing : go.AddComponent<MathGameManager>();

        var so = new SerializedObject(manager);
        so.FindProperty("equationText").objectReferenceValue = equationText;
        so.FindProperty("feedbackText").objectReferenceValue = feedbackText;
        so.FindProperty("numberBuilder").objectReferenceValue = numberBuilder;
        so.FindProperty("spawnArea").objectReferenceValue = spawnArea;
        so.FindProperty("correctVfxPrefab").objectReferenceValue = vfxPrefab;

        var vfxAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(VfxAssetPath);
        so.FindProperty("correctVfxAsset").objectReferenceValue = vfxAsset;
        so.ApplyModifiedPropertiesWithoutUndo();
        return manager;
    }

    static void EnsureMousePicker(GameObject host)
    {
        if (host.GetComponent<MouseAnswerPicker>() == null)
            host.AddComponent<MouseAnswerPicker>();
    }
}
#endif
