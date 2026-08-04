using TMPro;
using UnityEngine;

/// <summary>
/// Builds a world-space number visual from digit prefabs (0-9) or a TMP fallback.
/// </summary>
public class NumberVisualBuilder : MonoBehaviour
{
    [Header("Digit Prefabs (index = digit 0-9)")]
    [Tooltip("Assign your exported 0-9 number prefabs here. Leave empty to use TMP text.")]
    [SerializeField] GameObject[] digitPrefabs = new GameObject[10];

    [Header("Layout")]
    [SerializeField] float digitSpacing = 0.85f;
    [SerializeField] Vector3 digitScale = Vector3.one;

    [Header("TMP Fallback")]
    [SerializeField] float tmpFontSize = 8f;
    [SerializeField] Color tmpColor = Color.white;

    public bool HasDigitPrefabs
    {
        get
        {
            if (digitPrefabs == null || digitPrefabs.Length < 10)
                return false;
            for (int i = 0; i < 10; i++)
            {
                if (digitPrefabs[i] == null)
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Creates a number root with AnswerChoice + collider, parented under <paramref name="parent"/>.
    /// </summary>
    public AnswerChoice CreateNumber(int value, Transform parent)
    {
        var root = new GameObject($"Answer_{value}");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        if (HasDigitPrefabs)
            BuildFromDigitPrefabs(root.transform, value);
        else
            BuildFromTmp(root.transform, value);

        // Collider is added by AnswerChoice.EnsureCollider after renderers exist.
        var box = root.AddComponent<BoxCollider>();
        FitBoxCollider(box, root.transform);

        var choice = root.AddComponent<AnswerChoice>();
        return choice;
    }

    void BuildFromDigitPrefabs(Transform root, int value)
    {
        string digits = Mathf.Abs(value).ToString();
        bool negative = value < 0;
        int count = digits.Length + (negative ? 1 : 0);
        float totalWidth = (count - 1) * digitSpacing;
        float x = -totalWidth * 0.5f;

        if (negative)
        {
            // Simple minus bar if no dedicated prefab.
            var minus = GameObject.CreatePrimitive(PrimitiveType.Cube);
            minus.name = "Minus";
            minus.transform.SetParent(root, false);
            minus.transform.localPosition = new Vector3(x, 0f, 0f);
            minus.transform.localScale = new Vector3(0.45f, 0.12f, 0.12f);
            Object.Destroy(minus.GetComponent<Collider>());
            x += digitSpacing;
        }

        for (int i = 0; i < digits.Length; i++)
        {
            int d = digits[i] - '0';
            var prefab = digitPrefabs[d];
            var instance = Instantiate(prefab, root);
            instance.name = $"Digit_{d}";
            instance.transform.localPosition = new Vector3(x, 0f, 0f);
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = digitScale;

            // Child colliders confuse parent raycast sizing; remove if present.
            foreach (var col in instance.GetComponentsInChildren<Collider>())
                Object.Destroy(col);

            x += digitSpacing;
        }
    }

    void BuildFromTmp(Transform root, int value)
    {
        var textGo = new GameObject("TMP_Number");
        textGo.transform.SetParent(root, false);
        textGo.transform.localPosition = Vector3.zero;
        textGo.transform.localRotation = Quaternion.identity;

        var tmp = textGo.AddComponent<TextMeshPro>();
        tmp.text = value.ToString();
        tmp.fontSize = tmpFontSize;
        tmp.color = tmpColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.rectTransform.sizeDelta = new Vector2(4f, 2f);
    }

    static void FitBoxCollider(BoxCollider box, Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            box.center = Vector3.zero;
            box.size = new Vector3(1.2f, 1.5f, 0.6f);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        box.center = root.InverseTransformPoint(bounds.center);
        Vector3 size = root.InverseTransformVector(bounds.size);
        box.size = new Vector3(
            Mathf.Max(0.4f, Mathf.Abs(size.x)),
            Mathf.Max(0.4f, Mathf.Abs(size.y)),
            Mathf.Max(0.3f, Mathf.Abs(size.z)));
    }
}
