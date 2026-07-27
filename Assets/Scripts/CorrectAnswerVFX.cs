using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Correct-answer celebration. Always shows a blue growing mesh orb (build-safe for itch/WebGL).
/// Also tries Visual Effect Graph when available.
/// </summary>
public class CorrectAnswerVFX : MonoBehaviour
{
    const string SizeParam = "size";
    const string ColorParam = "New Color";
    const string ResourcesVfxName = "New VFX";
    const string ResourcesMatName = "BlueOrb";

    [SerializeField] float growDuration = 1.15f;
    [SerializeField] float holdAfterGrow = 0.4f;
    [SerializeField] float startScale = 0.35f;
    [SerializeField] float endScale = 3.2f;

    VisualEffect _vfx;
    VisualEffectAsset _vfxAsset;
    readonly List<Transform> _orbs = new List<Transform>();
    readonly List<float> _orbBaseScales = new List<float>();
    readonly List<Vector3> _orbDirs = new List<Vector3>();
    Material _orbMat;
    Color _baseEmission = new Color(0.2f, 1.2f, 3.5f, 1f);
    bool _hasSizeParam;
    bool _hasColorParam;
    Color _vfxBaseColor = new Color(0.27f, 0.70f, 3.7f, 0f);

    /// <summary>Spawns a guaranteed-visible blue burst at the answer position.</summary>
    public static CorrectAnswerVFX Spawn(Vector3 worldPosition, VisualEffectAsset asset, Transform numberToDestroy)
    {
        // Pull slightly toward the camera so it isn't buried in a background Quad.
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 toCam = (cam.transform.position - worldPosition).normalized;
            worldPosition += toCam * 0.75f;
        }

        var go = new GameObject("CorrectAnswerEffect");
        go.transform.position = worldPosition;

        var fx = go.AddComponent<CorrectAnswerVFX>();
        fx._vfxAsset = asset != null ? asset : Resources.Load<VisualEffectAsset>(ResourcesVfxName);
        fx.BuildMeshBurst();
        fx.TryAttachVisualEffect();
        fx.StartCoroutine(fx.PlayRoutine(numberToDestroy));
        return fx;
    }

    void BuildMeshBurst()
    {
        _orbMat = CreateOrbMaterial();

        // Core orb
        AddOrb(Vector3.zero, 1f);

        // Extra dots around the core (reads like particles, no ParticleSystem needed)
        const int count = 18;
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            dir.z *= 0.35f; // flatter toward camera
            AddOrb(dir.normalized * Random.Range(0.15f, 0.55f), Random.Range(0.18f, 0.38f));
        }
    }

    void AddOrb(Vector3 localPos, float localScale)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Orb";
        sphere.transform.SetParent(transform, false);
        sphere.transform.localPosition = localPos;
        sphere.transform.localScale = Vector3.one * localScale * startScale;

        var col = sphere.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        var rend = sphere.GetComponent<Renderer>();
        if (rend != null && _orbMat != null)
            rend.sharedMaterial = _orbMat;

        _orbs.Add(sphere.transform);
        _orbBaseScales.Add(localScale);
        Vector3 dir = localPos.sqrMagnitude > 0.0001f ? localPos.normalized : Vector3.up;
        _orbDirs.Add(dir);
    }

    void TryAttachVisualEffect()
    {
        // Skip VFX Graph on WebGL — often silent-fails in itch builds.
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
                    _vfx.SetFloat(SizeParam, 1f);
                _vfx.Play();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("VFX Graph skipped: " + e.Message);
                _vfx = null;
            }
        }

        float elapsed = 0f;
        while (elapsed < growDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutQuad(Mathf.Clamp01(elapsed / growDuration));
            ApplyProgress(t);
            yield return null;
        }

        ApplyProgress(1f);
        yield return new WaitForSeconds(holdAfterGrow);

        if (numberToDestroy != null)
            Destroy(numberToDestroy.gameObject);

        Destroy(gameObject);
    }

    void ApplyProgress(float t)
    {
        float scale = Mathf.Lerp(startScale, endScale, t);
        for (int i = 0; i < _orbs.Count; i++)
        {
            if (_orbs[i] == null)
                continue;

            float baseMul = _orbBaseScales[i];
            _orbs[i].localScale = Vector3.one * (scale * baseMul);

            if (i > 0)
                _orbs[i].localPosition = _orbDirs[i] * Mathf.Lerp(0.25f, 1.4f, t);
        }

        if (_orbMat != null)
        {
            float glow = Mathf.Lerp(1f, 3.5f, t);
            Color emission = _baseEmission * glow;
            if (_orbMat.HasProperty("_EmissionColor"))
                _orbMat.SetColor("_EmissionColor", emission);
            if (_orbMat.HasProperty("_BaseColor"))
            {
                Color c = Color.Lerp(new Color(0.35f, 0.8f, 1f), new Color(0.55f, 0.95f, 1f), t);
                _orbMat.SetColor("_BaseColor", c);
            }
        }

        if (_vfx != null)
        {
            if (_hasSizeParam)
                _vfx.SetFloat(SizeParam, Mathf.Lerp(1f, 3.5f, t));
            if (_hasColorParam)
            {
                _vfx.SetVector4(ColorParam, new Color(
                    _vfxBaseColor.r * Mathf.Lerp(1f, 4f, t),
                    _vfxBaseColor.g * Mathf.Lerp(1f, 4f, t),
                    _vfxBaseColor.b * Mathf.Lerp(1f, 4f, t),
                    _vfxBaseColor.a));
            }
        }
    }

    static Material CreateOrbMaterial()
    {
        var fromResources = Resources.Load<Material>(ResourcesMatName);
        Material mat;
        if (fromResources != null)
            mat = new Material(fromResources);
        else
        {
            // Same URP Lit shader used by this project (always in builds).
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("URP/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            mat = shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        Color blue = new Color(0.35f, 0.85f, 1f, 1f);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", blue);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", blue);

        // Emission = glow
        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", new Color(0.2f, 1.2f, 3.5f));
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        return mat;
    }

    static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);
}
