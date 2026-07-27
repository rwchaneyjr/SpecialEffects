using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.VFX;

/// <summary>
/// Auto-wires the math clicker when you press Play, even if the scene was never set up.
/// Shows a practice menu first (Addition / Subtraction / Times / Divide).
/// </summary>
public static class MathGameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoStart()
    {
        var existingManager = Object.FindObjectOfType<MathGameManager>();
        if (existingManager != null)
        {
            EnsureMenuFor(existingManager);
            EnsureVfxAsset(existingManager);
            return;
        }

        Debug.Log("Math Game: auto-setup. Choose practice mode, then Start.");

        EnsureCamera();
        EnsureEventSystem();

        Canvas canvas = CreateUi(out TextMeshProUGUI equationText, out TextMeshProUGUI feedbackText);

        var spawnGo = new GameObject("SpawnArea");
        spawnGo.transform.position = new Vector3(0f, 0.5f, 0f);

        var builderGo = new GameObject("NumberBuilder");
        var builder = builderGo.AddComponent<NumberVisualBuilder>();

        VisualEffectAsset vfxAsset = ResolveVfxAsset();

        // Keep scene demo VFX from covering gameplay; asset stays available via Resources.
        var sceneVfx = Object.FindObjectOfType<VisualEffect>();
        if (sceneVfx != null)
            sceneVfx.gameObject.SetActive(false);

        // Push background Quad back so celebration orbs aren't hidden inside it.
        var quad = GameObject.Find("Quad");
        if (quad != null)
        {
            var p = quad.transform.position;
            quad.transform.position = new Vector3(p.x, p.y, 2.5f);
        }

        var gmGo = new GameObject("GameManager");
        var manager = gmGo.AddComponent<MathGameManager>();
        gmGo.AddComponent<AnswerSfx>();
        gmGo.AddComponent<MouseAnswerPicker>();
        manager.Configure(equationText, feedbackText, builder, spawnGo.transform, null, vfxAsset);

        MathPracticeMenu.CreateRuntime(canvas, manager);
    }

    static void EnsureMenuFor(MathGameManager manager)
    {
        var quad = GameObject.Find("Quad");
        if (quad != null)
        {
            var p = quad.transform.position;
            if (p.z < 1.5f)
                quad.transform.position = new Vector3(p.x, p.y, 2.5f);
        }

        if (Object.FindObjectOfType<MathPracticeMenu>() != null)
            return;

        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            CreateUi(out _, out _);
            canvas = Object.FindObjectOfType<Canvas>();
        }

        if (canvas != null)
            MathPracticeMenu.CreateRuntime(canvas, manager);
    }

    static void EnsureVfxAsset(MathGameManager manager)
    {
        // Manager already configured in scene; still make sure Resources asset is resolvable.
        ResolveVfxAsset();
    }

    static VisualEffectAsset ResolveVfxAsset()
    {
        var fromResources = Resources.Load<VisualEffectAsset>("New VFX");
        if (fromResources != null)
            return fromResources;

        var sceneVfx = Object.FindObjectOfType<VisualEffect>();
        if (sceneVfx != null && sceneVfx.visualEffectAsset != null)
            return sceneVfx.visualEffectAsset;

        Debug.LogWarning("Math Game: could not find 'New VFX' in Resources. Particle fallback will be used.");
        return null;
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

    static Canvas CreateUi(out TextMeshProUGUI equationText, out TextMeshProUGUI feedbackText)
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        equationText = CreateTmp(canvasGo.transform, "EquationText", new Vector2(0f, 380f), 96f, Color.white);
        feedbackText = CreateTmp(canvasGo.transform, "FeedbackText", new Vector2(0f, -420f), 64f, new Color(1f, 0.85f, 0.4f));
        equationText.text = string.Empty;
        feedbackText.text = string.Empty;
        return canvas;
    }

    static TextMeshProUGUI CreateTmp(Transform parent, string name, Vector2 pos, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900f, 160f);
        rt.anchoredPosition = pos;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }
}
