using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Dramatic correct-answer explosion that works on itch.io / WebGL.
/// Uses bright URP Lit mesh shards (no ParticleSystem / VFX Graph required).
/// </summary>
public class CorrectAnswerVFX : MonoBehaviour
{
    const string SizeParam = "size";
    const string ColorParam = "New Color";
    const string ResourcesVfxName = "New VFX";
    const string ResourcesMatName = "BlueOrb";

    [Header("Explosion timing")]
    [SerializeField] float explodeDuration = 1.35f;
    [SerializeField] float holdAfter = 0.25f;
    [SerializeField] float destroyNumberAt = 0.2f;

    [Header("Explosion size")]
    [SerializeField] float coreStart = 0.4f;
    [SerializeField] float corePeak = 5.5f;
    [SerializeField] float shardTravel = 5.5f;
    [SerializeField] int shardCount = 55;
    [SerializeField] float cameraShake = 0.28f;

    VisualEffect _vfx;
    VisualEffectAsset _vfxAsset;

    Transform _core;
    Transform _shockwave;
    readonly List<Transform> _shards = new List<Transform>();
    readonly List<Vector3> _shardDirs = new List<Vector3>();
    readonly List<float> _shardSizes = new List<float>();
    readonly List<float> _shardSpeeds = new List<float>();

    Material _hotMat;
    Material _coreMat;
    Color _hotEmission = new Color(0.4f, 2.2f, 6.5f, 1f);
    Vector3 _camBasePos;
    bool _shaking;

    bool _hasSizeParam;
    bool _hasColorParam;
    Color _vfxBaseColor = new Color(0.27f, 0.70f, 3.7f, 0f);

    public static CorrectAnswerVFX Spawn(Vector3 worldPosition, VisualEffectAsset asset, Transform numberToDestroy)
    {
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 toCam = (cam.transform.position - worldPosition).normalized;
            worldPosition += toCam * 1.0f;
        }

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
        _hotMat = CreateMaterial(new Color(0.45f, 0.95f, 1f), _hotEmission);
        _coreMat = CreateMaterial(new Color(0.75f, 0.98f, 1f), _hotEmission * 1.6f);

        // Blinding core flash
        _core = MakeSphere("Core", Vector3.zero, coreStart, _coreMat);

        // Shockwave ring (flat disc of spheres)
        var wave = new GameObject("Shockwave");
        wave.transform.SetParent(transform, false);
        _shockwave = wave.transform;
        for (int i = 0; i < 16; i++)
        {
            float ang = i / 16f * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * 0.35f;
            var piece = MakeSphere("Wave", p, 0.22f, _hotMat);
            piece.SetParent(_shockwave, false);
        }

        // Flying shards
        for (int i = 0; i < shardCount; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            dir.z *= 0.45f;
            dir.Normalize();

            float size = Random.Range(0.12f, 0.55f);
            var shard = MakeSphere("Shard", dir * Random.Range(0.05f, 0.25f), size, _hotMat);
            _shards.Add(shard);
            _shardDirs.Add(dir);
            _shardSizes.Add(size);
            _shardSpeeds.Add(Random.Range(0.75f, 1.35f));
        }

        // Extra big chunks for drama
        for (int i = 0; i < 8; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            dir.z *= 0.3f;
            dir.Normalize();
            float size = Random.Range(0.45f, 0.85f);
            var chunk = MakeSphere("Chunk", dir * 0.1f, size, _coreMat);
            _shards.Add(chunk);
            _shardDirs.Add(dir);
            _shardSizes.Add(size);
            _shardSpeeds.Add(Random.Range(0.55f, 0.9f));
        }
    }

    Transform MakeSphere(string name, Vector3 localPos, float scale, Material mat)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.SetParent(transform, false);
        sphere.transform.localPosition = localPos;
        sphere.transform.localScale = Vector3.one * scale;

        var col = sphere.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        var rend = sphere.GetComponent<Renderer>();
        if (rend != null && mat != null)
            rend.sharedMaterial = mat;

        return sphere.transform;
    }

    void TryAttachVisualEffect()
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer)
            return;
        if (_vfxAsset == null)
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
                if (_hasColorParam)
                    _vfxBaseColor = _vfx.GetVector4(ColorParam);
                if (_hasSizeParam)
                    _vfx.SetFloat(SizeParam, 1.5f);
                _vfx.Play();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("VFX Graph skipped: " + e.Message);
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
            // Punchy start, lingering end
            float eased = 1f - Mathf.Pow(1f - t, 3.5f);
            ApplyExplosion(eased, t);

            if (!numberGone && elapsed >= destroyNumberAt && numberToDestroy != null)
            {
                Destroy(numberToDestroy.gameObject);
                numberGone = true;
            }

            if (_shaking && cam != null)
            {
                float shakeAmt = cameraShake * (1f - t) * (1f - t);
                cam.transform.localPosition = _camBasePos + Random.insideUnitSphere * shakeAmt;
            }

            yield return null;
        }

        ApplyExplosion(1f, 1f);

        if (_shaking && cam != null)
            cam.transform.localPosition = _camBasePos;
        _shaking = false;

        if (!numberGone && numberToDestroy != null)
            Destroy(numberToDestroy.gameObject);

        yield return new WaitForSeconds(holdAfter);
        Destroy(gameObject);
    }

    void ApplyExplosion(float eased, float linearT)
    {
        // Core: explode big, then shrink away
        if (_core != null)
        {
            float coreScale;
            if (eased < 0.35f)
                coreScale = Mathf.Lerp(coreStart, corePeak, EaseOutBack(Mathf.InverseLerp(0f, 0.35f, eased)));
            else
                coreScale = Mathf.Lerp(corePeak, 0.05f, Mathf.InverseLerp(0.35f, 1f, eased));
            _core.localScale = Vector3.one * coreScale;
        }

        // Shockwave expands fast and thins
        if (_shockwave != null)
        {
            float wave = Mathf.Lerp(0.4f, shardTravel * 1.15f, eased);
            _shockwave.localScale = new Vector3(wave, wave, wave * 0.25f);
            for (int i = 0; i < _shockwave.childCount; i++)
            {
                var c = _shockwave.GetChild(i);
                float s = Mathf.Lerp(0.35f, 0.05f, eased);
                c.localScale = Vector3.one * s;
            }
        }

        // Shards fly outward and shrink
        for (int i = 0; i < _shards.Count; i++)
        {
            if (_shards[i] == null)
                continue;

            float dist = shardTravel * _shardSpeeds[i] * eased;
            _shards[i].localPosition = _shardDirs[i] * dist;

            float sizeMul = Mathf.Lerp(1.15f, 0.05f, eased);
            // Pop larger at the start
            if (linearT < 0.15f)
                sizeMul = Mathf.Lerp(0.6f, 1.35f, linearT / 0.15f);

            _shards[i].localScale = Vector3.one * (_shardSizes[i] * sizeMul);
        }

        // Super hot flash then cool
        float glow = linearT < 0.2f
            ? Mathf.Lerp(2f, 8f, linearT / 0.2f)
            : Mathf.Lerp(8f, 0.4f, (linearT - 0.2f) / 0.8f);

        SetGlow(_hotMat, _hotEmission * glow, Color.Lerp(new Color(0.5f, 0.95f, 1f), new Color(0.2f, 0.55f, 1f), linearT));
        SetGlow(_coreMat, _hotEmission * (glow * 1.4f), Color.Lerp(Color.white, new Color(0.4f, 0.9f, 1f), linearT));

        if (_vfx != null)
        {
            if (_hasSizeParam)
                _vfx.SetFloat(SizeParam, Mathf.Lerp(1.5f, 5f, eased));
            if (_hasColorParam)
            {
                float g = Mathf.Lerp(2f, 6f, glow / 8f);
                _vfx.SetVector4(ColorParam, new Color(
                    _vfxBaseColor.r * g,
                    _vfxBaseColor.g * g,
                    _vfxBaseColor.b * g,
                    _vfxBaseColor.a));
            }
        }
    }

    static void SetGlow(Material mat, Color emission, Color baseColor)
    {
        if (mat == null)
            return;
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", emission);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", baseColor);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", baseColor);
    }

    static Material CreateMaterial(Color baseColor, Color emission)
    {
        var fromResources = Resources.Load<Material>(ResourcesMatName);
        Material mat;
        if (fromResources != null)
            mat = new Material(fromResources);
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            mat = new Material(shader);
        }

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", baseColor);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", baseColor);

        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", emission);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return mat;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
