using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VFX;

/// <summary>
/// Polished correct-answer explosion for itch.io / WebGL:
/// bright core, light flash, cyan comet trails, shockwave rings, camera shake.
/// </summary>
public class CorrectAnswerVFX : MonoBehaviour
{
    const string SizeParam = "size";
    const string ColorParam = "New Color";
    const string ResourcesVfxName = "New VFX";
    const string ResourcesMatName = "BlueOrb";

    [SerializeField] float explodeDuration = 1.45f;
    [SerializeField] float holdAfter = 0.2f;
    [SerializeField] float destroyNumberAt = 0.18f;
    [SerializeField] float corePeak = 4.8f;
    [SerializeField] float shardTravel = 6.2f;
    [SerializeField] int cometCount = 42;
    [SerializeField] float cameraShake = 0.22f;

    VisualEffect _vfx;
    VisualEffectAsset _vfxAsset;

    Transform _core;
    Transform _innerGlow;
    Transform _shockwave;
    Transform _shockwave2;
    Light _flashLight;
    Image _screenFlash;
    Canvas _flashCanvas;

    readonly List<Transform> _comets = new List<Transform>();
    readonly List<Vector3> _dirs = new List<Vector3>();
    readonly List<float> _sizes = new List<float>();
    readonly List<float> _speeds = new List<float>();
    readonly List<TrailRenderer> _trails = new List<TrailRenderer>();

    Material _cyanMat;
    Material _whiteMat;
    Material _deepMat;
    Material _trailMat;

    Vector3 _camBasePos;
    bool _shaking;
    bool _hasSizeParam;
    bool _hasColorParam;
    Color _vfxBaseColor = new Color(0.27f, 0.70f, 3.7f, 0f);

    public static CorrectAnswerVFX Spawn(Vector3 worldPosition, VisualEffectAsset asset, Transform numberToDestroy)
    {
        var cam = Camera.main;
        if (cam != null)
            worldPosition += (cam.transform.position - worldPosition).normalized * 1.1f;

        var go = new GameObject("CorrectAnswerExplosion");
        go.transform.position = worldPosition;

        var fx = go.AddComponent<CorrectAnswerVFX>();
        fx._vfxAsset = asset != null ? asset : Resources.Load<VisualEffectAsset>(ResourcesVfxName);
        fx.BuildExplosion();
        fx.TryAttachVisualEffect();
        fx.StartCoroutine(fx.PlayRoutine(numberToDestroy));
        return fx;
    }

    void BuildExplosion()
    {
        _cyanMat = CreateLit(new Color(0.35f, 0.9f, 1f), new Color(0.3f, 2f, 6f));
        _whiteMat = CreateLit(new Color(0.95f, 0.98f, 1f), new Color(1.5f, 2.5f, 4f));
        _deepMat = CreateLit(new Color(0.15f, 0.45f, 1f), new Color(0.1f, 0.8f, 4f));
        _trailMat = CreateSpritesMat(new Color(0.4f, 0.95f, 1f, 0.85f));

        _core = MakeSphere("Core", Vector3.zero, 0.35f, _whiteMat);
        _innerGlow = MakeSphere("InnerGlow", Vector3.zero, 0.55f, _cyanMat);

        _shockwave = BuildRing("Shockwave", 20, 0.4f, 0.2f, _cyanMat);
        _shockwave2 = BuildRing("Shockwave2", 14, 0.55f, 0.16f, _deepMat);

        for (int i = 0; i < cometCount; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            dir.z *= 0.4f;
            dir.Normalize();

            bool big = i < 10;
            float size = big ? Random.Range(0.28f, 0.55f) : Random.Range(0.08f, 0.22f);
            Material mat = big ? _whiteMat : (Random.value > 0.45f ? _cyanMat : _deepMat);

            // Elongated “comet” body
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Comet";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = dir * 0.08f;
            body.transform.localScale = new Vector3(size * 0.55f, size * 0.55f, size * 1.8f);
            body.transform.rotation = Quaternion.LookRotation(dir);

            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = body.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = mat;

            var trail = body.AddComponent<TrailRenderer>();
            trail.time = big ? 0.45f : 0.28f;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = size * (big ? 1.1f : 0.7f);
            trail.material = _trailMat;
            trail.emitting = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.3f, 0.85f, 1f), 0.35f),
                    new GradientColorKey(new Color(0.1f, 0.35f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.55f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                });
            trail.colorGradient = grad;

            _comets.Add(body.transform);
            _dirs.Add(dir);
            _sizes.Add(size);
            _speeds.Add(Random.Range(0.7f, 1.4f));
            _trails.Add(trail);
        }

        // Point light flash (reads well in URP builds)
        var lightGo = new GameObject("FlashLight");
        lightGo.transform.SetParent(transform, false);
        _flashLight = lightGo.AddComponent<Light>();
        _flashLight.type = LightType.Point;
        _flashLight.color = new Color(0.55f, 0.9f, 1f);
        _flashLight.intensity = 0f;
        _flashLight.range = 14f;
        _flashLight.shadows = LightShadows.None;

        BuildScreenFlash();
    }

    Transform BuildRing(string name, int count, float radius, float pieceScale, Material mat)
    {
        var root = new GameObject(name);
        root.transform.SetParent(transform, false);
        for (int i = 0; i < count; i++)
        {
            float ang = i / (float)count * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * radius;
            var piece = MakeSphere("RingBit", p, pieceScale, mat);
            piece.SetParent(root.transform, false);
        }
        return root.transform;
    }

    void BuildScreenFlash()
    {
        var canvasGo = new GameObject("ExplosionFlashCanvas");
        _flashCanvas = canvasGo.AddComponent<Canvas>();
        _flashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _flashCanvas.sortingOrder = 1000;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        var imgGo = new GameObject("Flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imgGo.transform.SetParent(canvasGo.transform, false);
        var rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _screenFlash = imgGo.GetComponent<Image>();
        _screenFlash.color = new Color(0.55f, 0.9f, 1f, 0f);
        _screenFlash.raycastTarget = false;
    }

    Transform MakeSphere(string name, Vector3 localPos, float scale, Material mat)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.SetParent(transform, false);
        sphere.transform.localPosition = localPos;
        sphere.transform.localScale = Vector3.one * scale;
        var col = sphere.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var rend = sphere.GetComponent<Renderer>();
        if (rend != null && mat != null) rend.sharedMaterial = mat;
        return sphere.transform;
    }

    void TryAttachVisualEffect()
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer || _vfxAsset == null)
            return;
        _vfx = gameObject.AddComponent<VisualEffect>();
        _vfx.visualEffectAsset = _vfxAsset;
    }

    IEnumerator PlayRoutine(Transform numberToDestroy)
    {
        yield return null;

        if (_vfx != null)
        {
            try
            {
                _vfx.Reinit();
                _hasSizeParam = _vfx.HasFloat(SizeParam);
                _hasColorParam = _vfx.HasVector4(ColorParam);
                if (_hasColorParam) _vfxBaseColor = _vfx.GetVector4(ColorParam);
                if (_hasSizeParam) _vfx.SetFloat(SizeParam, 2f);
                _vfx.Play();
            }
            catch
            {
                _vfx = null;
            }
        }

        var cam = Camera.main;
        if (cam != null && cameraShake > 0f)
        {
            _camBasePos = cam.transform.localPosition;
            _shaking = true;
        }

        bool numberGone = false;
        float elapsed = 0f;
        while (elapsed < explodeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / explodeDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3.2f);
            ApplyExplosion(eased, t);

            if (!numberGone && elapsed >= destroyNumberAt && numberToDestroy != null)
            {
                Destroy(numberToDestroy.gameObject);
                numberGone = true;
            }

            if (_shaking && cam != null)
            {
                float shakeAmt = cameraShake * Mathf.Exp(-3.5f * t);
                cam.transform.localPosition = _camBasePos + Random.insideUnitSphere * shakeAmt;
            }

            yield return null;
        }

        if (_shaking && cam != null)
            cam.transform.localPosition = _camBasePos;

        if (!numberGone && numberToDestroy != null)
            Destroy(numberToDestroy.gameObject);

        yield return new WaitForSeconds(holdAfter);

        if (_flashCanvas != null)
            Destroy(_flashCanvas.gameObject);
        Destroy(gameObject);
    }

    void ApplyExplosion(float eased, float t)
    {
        // Core bloom then collapse
        if (_core != null)
        {
            float s = t < 0.28f
                ? Mathf.Lerp(0.3f, corePeak, EaseOutBack(t / 0.28f))
                : Mathf.Lerp(corePeak, 0.02f, (t - 0.28f) / 0.72f);
            _core.localScale = Vector3.one * s;
        }

        if (_innerGlow != null)
        {
            float s = t < 0.35f
                ? Mathf.Lerp(0.5f, corePeak * 1.15f, t / 0.35f)
                : Mathf.Lerp(corePeak * 1.15f, 0.02f, (t - 0.35f) / 0.65f);
            _innerGlow.localScale = Vector3.one * s;
        }

        AnimateRing(_shockwave, eased, 0.5f, shardTravel * 1.2f, 0.28f, 0.04f);
        // Second ring delayed
        float e2 = Mathf.Clamp01((eased - 0.12f) / 0.88f);
        AnimateRing(_shockwave2, e2, 0.6f, shardTravel * 1.45f, 0.22f, 0.03f);

        for (int i = 0; i < _comets.Count; i++)
        {
            if (_comets[i] == null) continue;

            float dist = shardTravel * _speeds[i] * eased;
            Vector3 dir = _dirs[i];
            _comets[i].localPosition = dir * dist;
            _comets[i].rotation = Quaternion.LookRotation(dir);

            float sizeMul = t < 0.12f
                ? Mathf.Lerp(0.5f, 1.25f, t / 0.12f)
                : Mathf.Lerp(1.25f, 0.04f, (t - 0.12f) / 0.88f);

            float size = _sizes[i] * sizeMul;
            _comets[i].localScale = new Vector3(size * 0.5f, size * 0.5f, size * Mathf.Lerp(1.2f, 2.4f, eased));

            if (_trails[i] != null)
                _trails[i].widthMultiplier = size * 0.9f;
        }

        // Lighting + screen flash
        float flash = t < 0.15f
            ? Mathf.Lerp(0f, 1f, t / 0.15f)
            : Mathf.Lerp(1f, 0f, (t - 0.15f) / 0.55f);
        flash = Mathf.Clamp01(flash);

        if (_flashLight != null)
        {
            _flashLight.intensity = flash * 18f;
            _flashLight.range = Mathf.Lerp(8f, 16f, flash);
        }

        if (_screenFlash != null)
            _screenFlash.color = new Color(0.6f, 0.92f, 1f, flash * 0.42f);

        float glow = t < 0.18f ? Mathf.Lerp(1.5f, 9f, t / 0.18f) : Mathf.Lerp(9f, 0.3f, (t - 0.18f) / 0.82f);
        SetGlow(_cyanMat, new Color(0.3f, 2f, 6f) * glow, Color.Lerp(new Color(0.5f, 0.95f, 1f), new Color(0.2f, 0.5f, 1f), t));
        SetGlow(_whiteMat, new Color(2f, 3f, 5f) * glow, Color.Lerp(Color.white, new Color(0.5f, 0.9f, 1f), t));
        SetGlow(_deepMat, new Color(0.15f, 1f, 4f) * glow, Color.Lerp(new Color(0.3f, 0.6f, 1f), new Color(0.05f, 0.2f, 0.8f), t));

        if (_vfx != null)
        {
            if (_hasSizeParam) _vfx.SetFloat(SizeParam, Mathf.Lerp(2f, 5.5f, eased));
            if (_hasColorParam)
            {
                float g = Mathf.Lerp(2f, 7f, flash);
                _vfx.SetVector4(ColorParam, new Color(_vfxBaseColor.r * g, _vfxBaseColor.g * g, _vfxBaseColor.b * g, _vfxBaseColor.a));
            }
        }
    }

    static void AnimateRing(Transform ring, float eased, float start, float end, float pieceStart, float pieceEnd)
    {
        if (ring == null) return;
        float wave = Mathf.Lerp(start, end, eased);
        ring.localScale = new Vector3(wave, wave, wave * 0.2f);
        for (int i = 0; i < ring.childCount; i++)
            ring.GetChild(i).localScale = Vector3.one * Mathf.Lerp(pieceStart, pieceEnd, eased);
    }

    static void SetGlow(Material mat, Color emission, Color baseColor)
    {
        if (mat == null) return;
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
    }

    static Material CreateLit(Color baseColor, Color emission)
    {
        var src = Resources.Load<Material>(ResourcesMatName);
        Material mat = src != null
            ? new Material(src)
            : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return mat;
    }

    static Material CreateSpritesMat(Color color)
    {
        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
        var mat = new Material(shader);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        return mat;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    void OnDestroy()
    {
        if (_flashCanvas != null)
            Destroy(_flashCanvas.gameObject);

        if (_shaking)
        {
            var cam = Camera.main;
            if (cam != null)
                cam.transform.localPosition = _camBasePos;
        }
    }
}
