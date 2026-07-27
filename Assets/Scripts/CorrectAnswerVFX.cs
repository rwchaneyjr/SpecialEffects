using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.UI;

/// <summary>
/// Candy-crush-like goo pop (mesh-only, WebGL-safe).
/// Fast blob expansion + colorful droplets, then quick disappear.
/// No ParticleSystem / no VFX Graph dependency for visibility.
/// </summary>
public class CorrectAnswerVFX : MonoBehaviour
{
    const string ResourcesVfxName = "New VFX";

    [Header("Timing")]
    [SerializeField] float popDuration = 0.7f;
    [SerializeField] float holdAfter = 0.08f;
    [SerializeField] float destroyNumberAt = 0.18f;

    [Header("Size")]
    [SerializeField] float coreStart = 0.25f;
    [SerializeField] float corePeak = 5.2f;
    [SerializeField] float coreEnd = 0.01f;

    [Header("Candy Colors (change here)" )]
    [SerializeField] Color blobBase = new Color(1f, 0.25f, 0.7f, 1f);    // pink
    [SerializeField] Color blobHot = new Color(1.2f, 0.6f, 1.8f, 1f);     // glow
    [SerializeField] Color dropletBase = new Color(0.35f, 0.9f, 1f, 1f);  // cyan
    [SerializeField] Color dropletHot = new Color(0.2f, 1.8f, 2.8f, 1f);

    [Header("Bits")]
    [SerializeField] int dropletCount = 20;
    [SerializeField] float dropletRadius = 0.35f;
    [SerializeField] float dropletTravel = 4.1f;
    [SerializeField] float cameraShake = 0.15f;

    Transform _core;
    readonly List<Transform> _droplets = new List<Transform>();
    readonly List<Vector3> _dirs = new List<Vector3>();
    readonly List<float> _baseSizes = new List<float>();

    Material _blobMat;
    Material _dropletMat;

    Vector3 _camBasePos;
    bool _shaking;

    public static CorrectAnswerVFX Spawn(Vector3 worldPosition, VisualEffectAsset asset, Transform numberToDestroy)
    {
        // UI flash is WebGL-safe and ensures we always see feedback on itch.io.
        SpawnScreenFlash();

        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 toCam = (cam.transform.position - worldPosition).normalized;
            worldPosition += toCam * 0.9f;
        }

        var go = new GameObject("CorrectAnswerGooPop");
        go.transform.position = worldPosition;

        var fx = go.AddComponent<CorrectAnswerVFX>();
        fx.BuildMeshPop();
        fx.StartCoroutine(fx.PlayRoutine(numberToDestroy));
        return fx;
    }

    static void SpawnScreenFlash()
    {
        // Fullscreen Canvas overlay.
        var existing = GameObject.Find("CandyFlashCanvas");
        if (existing != null)
            GameObject.Destroy(existing);

        var canvasGo = new GameObject("CandyFlashCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        var imgGo = new GameObject("Flash");
        imgGo.transform.SetParent(canvasGo.transform, false);
        var rt = imgGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = imgGo.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = new Color(1f, 0.25f, 0.7f, 0f);

        // Drive alpha with a tiny helper coroutine.
        canvasGo.AddComponent<FlashDriver>().Init(img);
    }

    class FlashDriver : MonoBehaviour
    {
        Image _img;
        float _dur = 0.22f;

        public void Init(Image img)
        {
            _img = img;
        }

        void Start()
        {
            StartCoroutine(Run());
        }

        System.Collections.IEnumerator Run()
        {
            float t = 0f;
            while (t < _dur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / _dur);
                // quick in, slow out
                float a = (p < 0.35f) ? Mathf.Lerp(0f, 0.9f, p / 0.35f) : Mathf.Lerp(0.9f, 0f, (p - 0.35f) / 0.65f);
                if (_img != null)
                    _img.color = new Color(1f, 0.25f, 0.7f, a);
                yield return null;
            }

            if (_img != null)
                _img.color = new Color(1f, 0.25f, 0.7f, 0f);

            Destroy(gameObject);
        }
    }

    void BuildMeshPop()
    {
        _blobMat = CreateEmissiveMaterial(blobBase, blobHot);
        _dropletMat = CreateEmissiveMaterial(dropletBase, dropletHot);

        _core = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        _core.name = "CoreBlob";
        _core.SetParent(transform, false);
        _core.localPosition = Vector3.zero;
        _core.localScale = Vector3.one * coreStart;
        Destroy(_core.GetComponent<Collider>());
        _core.GetComponent<Renderer>().sharedMaterial = _blobMat;

        for (int i = 0; i < dropletCount; i++)
        {
            float ang = (i / (float)dropletCount) * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), Random.Range(-0.35f, 0.35f)).normalized;
            float size = Random.Range(0.05f, 0.18f);

            var d = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            d.name = "Droplet";
            d.SetParent(transform, false);
            d.localPosition = dir * dropletRadius;
            d.localScale = Vector3.one * size;
            Destroy(d.GetComponent<Collider>());
            d.GetComponent<Renderer>().sharedMaterial = _dropletMat;

            _droplets.Add(d);
            _dirs.Add(dir);
            _baseSizes.Add(size);
        }
    }

    IEnumerator PlayRoutine(Transform numberToDestroy)
    {
        yield return null;

        var cam = Camera.main;
        if (cam != null && cameraShake > 0f)
        {
            _camBasePos = cam.transform.localPosition;
            _shaking = true;
        }

        bool numberGone = false;
        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);

            // Blob wobble: expands, then squashes and vanishes.
            float expandT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.55f));
            float scale = Mathf.Lerp(coreStart, corePeak, expandT);

            float wobble = Mathf.Sin(t * Mathf.PI * 2f);
            float wobbleX = 1f + 0.18f * wobble;
            float wobbleY = 1f - 0.10f * wobble;
            float wobbleZ = 1f + 0.05f * Mathf.Cos(t * Mathf.PI * 2f);

            if (t < 0.55f)
            {
                _core.localScale = new Vector3(scale * wobbleX, scale * wobbleY, scale * wobbleZ);
            }
            else
            {
                float shrinkT = Mathf.InverseLerp(0.55f, 1f, t);
                float s = Mathf.Lerp(corePeak, coreEnd, Mathf.SmoothStep(0f, 1f, shrinkT));
                _core.localScale = new Vector3(s * wobbleX, s * wobbleY, s * wobbleZ);
            }

            // Droplets shoot out fast, then shrink.
            float outwardT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.7f, t));
            float inwardT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, t));

            for (int i = 0; i < _droplets.Count; i++)
            {
                if (_droplets[i] == null) continue;
                float dist = dropletTravel * outwardT;
                _droplets[i].localPosition = _dirs[i] * (dropletRadius + dist);

                float s = _baseSizes[i] * Mathf.Lerp(1.25f, 0.03f, inwardT);
                _droplets[i].localScale = Vector3.one * s;
            }

            if (_shaking && cam != null)
            {
                float shakeAmt = cameraShake * Mathf.Pow(1f - t, 2f);
                cam.transform.localPosition = _camBasePos + Random.insideUnitSphere * shakeAmt;
            }

            if (!numberGone && elapsed >= destroyNumberAt && numberToDestroy != null)
            {
                Destroy(numberToDestroy.gameObject);
                numberGone = true;
            }

            yield return null;
        }

        if (_shaking && cam != null)
            cam.transform.localPosition = _camBasePos;

        if (!numberGone && numberToDestroy != null)
            Destroy(numberToDestroy.gameObject);

        yield return new WaitForSeconds(holdAfter);
        Destroy(gameObject);
    }

    static Material CreateEmissiveMaterial(Color baseColor, Color hotEmission)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"));

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);

        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", hotEmission);

        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return mat;
    }
}
