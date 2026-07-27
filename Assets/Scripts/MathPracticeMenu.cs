using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Start / pause menu: pick Addition, Subtraction, Times Tables, Divide, then Start.
/// Esc during play returns here.
/// </summary>
public class MathPracticeMenu : MonoBehaviour
{
    [SerializeField] MathGameManager gameManager;
    [SerializeField] GameObject menuRoot;
    [SerializeField] Toggle additionToggle;
    [SerializeField] Toggle subtractionToggle;
    [SerializeField] Toggle multiplyToggle;
    [SerializeField] Toggle divideToggle;
    Toggle[] _timesTableToggles = new Toggle[12]; // 1..12
    [SerializeField] Button startButton;
    [SerializeField] TextMeshProUGUI hintText;

    public bool IsOpen => menuRoot != null && menuRoot.activeSelf;

    public void Configure(
        MathGameManager manager,
        GameObject root,
        Toggle add,
        Toggle sub,
        Toggle mul,
        Toggle div,
        Toggle[] tableToggles,
        Button start,
        TextMeshProUGUI hint)
    {
        gameManager = manager;
        menuRoot = root;
        additionToggle = add;
        subtractionToggle = sub;
        multiplyToggle = mul;
        divideToggle = div;
        if (tableToggles != null && tableToggles.Length == 12)
            _timesTableToggles = tableToggles;
        startButton = start;
        hintText = hint;
        WireButtons();
    }

    void Awake()
    {
        WireButtons();
    }

    void WireButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartClicked);
            startButton.onClick.AddListener(OnStartClicked);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && gameManager != null && gameManager.IsPlaying)
            OpenMenu();
    }

    public void OpenMenu()
    {
        if (menuRoot != null)
            menuRoot.SetActive(true);

        if (gameManager != null)
            gameManager.PauseToMenu();
    }

    public void CloseMenu()
    {
        if (menuRoot != null)
            menuRoot.SetActive(false);
    }

    void OnStartClicked()
    {
        var ops = BuildSelectedOps();
        if (ops.Count == 0)
        {
            if (hintText != null)
                hintText.text = "Pick at least one: Addition, Subtraction, Times, or Divide";
            return;
        }

        var tables = BuildSelectedTables();

        if (hintText != null)
            hintText.text = "Esc = pause / change practice";

        CloseMenu();
        if (gameManager != null)
            gameManager.BeginPractice(ops, tables);
    }

    System.Collections.Generic.List<MathOp> BuildSelectedOps()
    {
        var ops = new System.Collections.Generic.List<MathOp>();
        if (additionToggle == null || additionToggle.isOn) ops.Add(MathOp.Add);
        if (subtractionToggle != null && subtractionToggle.isOn) ops.Add(MathOp.Subtract);
        if (multiplyToggle != null && multiplyToggle.isOn) ops.Add(MathOp.Multiply);
        if (divideToggle != null && divideToggle.isOn) ops.Add(MathOp.Divide);

        // If toggles were never wired, default to addition.
        if (ops.Count == 0 && additionToggle == null)
            ops.Add(MathOp.Add);

        return ops;
    }

    System.Collections.Generic.List<int> BuildSelectedTables()
    {
        var list = new System.Collections.Generic.List<int>();
        for (int i = 0; i < _timesTableToggles.Length; i++)
        {
            var t = _timesTableToggles[i];
            if (t != null && t.isOn)
                list.Add(i + 1); // 1..12
        }
        return list;
    }

    /// <summary>
    /// Builds a simple full-screen practice menu under an existing Canvas.
    /// </summary>
    public static MathPracticeMenu CreateRuntime(Canvas canvas, MathGameManager manager)
    {
        var host = new GameObject("PracticeMenu");
        host.transform.SetParent(canvas.transform, false);
        var menu = host.AddComponent<MathPracticeMenu>();

        var root = CreatePanel(canvas.transform, "MenuRoot");
        var title = CreateLabel(root.transform, "Title", "Choose what to practice", new Vector2(0f, 260f), 54f, Color.white);
        var hint = CreateLabel(root.transform, "Hint", "Select one or more, then Start", new Vector2(0f, 190f), 28f, new Color(1f, 0.9f, 0.7f));

        var add = CreateToggle(root.transform, "Addition", new Vector2(0f, 90f), true);
        var sub = CreateToggle(root.transform, "Subtraction", new Vector2(0f, 20f), false);
        var mul = CreateToggle(root.transform, "Times Tables", new Vector2(0f, -50f), false);
        var div = CreateToggle(root.transform, "Divide", new Vector2(0f, -120f), false);

        // Times table picker (1..12). You can select which tables to practice for Multiply/Divide.
        var tableLabel = CreateLabel(root.transform, "TablesLabel", "Tables (Multiply/Divide)", new Vector2(0f, -85f), 24f, new Color(1f, 0.9f, 0.7f));
        _ = tableLabel;

        var tableToggles = new Toggle[12];
        int cols = 2;              // 2 columns on the right side
        float startX = 165f;      // shift to the right
        float stepX = 85f;
        float startY = -70f;      // top of the grid
        float stepY = -30f;       // downwards per row
        for (int i = 0; i < 12; i++)
        {
            int row = i / cols; // 0..5
            int col = i % cols; // 0..1
            float x = startX + col * stepX;
            float y = startY + row * stepY;
            int tableNumber = i + 1;
            // Default selection: practice tables 2..12 (leave 1 off). User can change.
            bool defaultOn = tableNumber != 1;
            tableToggles[i] = CreateMiniToggle(root.transform, tableNumber.ToString(), new Vector2(x, y), defaultOn);
        }

        Debug.Log($"MathPracticeMenu: created {tableToggles.Length} table toggles.");

        var start = CreateButton(root.transform, "Start Practice", new Vector2(0f, -195f));

        // Keep title/hint references; title unused beyond creation.
        _ = title;

        menu.Configure(manager, root, add, sub, mul, div, tableToggles, start, hint);
        return menu;
    }

    static Toggle CreateMiniToggle(Transform parent, string label, Vector2 pos, bool on)
    {
        var go = new GameObject(label + "MiniToggle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Toggle));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(60f, 34f);
        rt.anchoredPosition = pos;

        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.75f, 1f, 0.92f);

        // Use label as the graphic target so it’s visible even without separate checkmark sprites.
        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18f;
        tmp.color = Color.black;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        var toggle = go.GetComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.graphic = bg;
        toggle.isOn = on;
        return toggle;
    }

    static GameObject CreatePanel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.08f, 0.05f, 0.06f, 0.92f);
        return go;
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, Vector2 pos, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900f, 80f);
        rt.anchoredPosition = pos;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    static Toggle CreateToggle(Transform parent, string label, Vector2 pos, bool on)
    {
        var go = new GameObject(label + "Toggle", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(420f, 54f);
        rt.anchoredPosition = pos;

        var bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(0.18f, 0.14f, 0.16f, 1f);

        var check = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        check.transform.SetParent(bg.transform, false);
        var checkRt = check.GetComponent<RectTransform>();
        checkRt.anchorMin = new Vector2(0f, 0.5f);
        checkRt.anchorMax = new Vector2(0f, 0.5f);
        checkRt.pivot = new Vector2(0f, 0.5f);
        checkRt.sizeDelta = new Vector2(36f, 36f);
        checkRt.anchoredPosition = new Vector2(12f, 0f);
        var checkImg = check.GetComponent<Image>();
        checkImg.color = new Color(0.35f, 0.85f, 1f, 1f);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(60f, 0f);
        labelRt.offsetMax = new Vector2(-12f, 0f);
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 34f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        var toggle = go.AddComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic = checkImg;
        toggle.isOn = on;
        return toggle;
    }

    static Button CreateButton(Transform parent, string label, Vector2 pos)
    {
        var go = new GameObject(label.Replace(" ", "") + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(360f, 70f);
        rt.anchoredPosition = pos;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.15f, 0.55f, 0.85f, 1f);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 36f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        return go.GetComponent<Button>();
    }
}

public enum MathOp
{
    Add,
    Subtract,
    Multiply,
    Divide
}
