using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Correct-answer effect: prefers Visual Effect Graph, falls back to a blue ParticleSystem
/// (needed for many itch.io / WebGL builds where VFX Graph does not play).
/// </summary>
public class CorrectAnswerVFX : MonoBehaviour
{
    const string SizeParam = "size";
    const string ColorParam = "New Color";
    const string ResourcesVfxName = "New VFX";

    [Header("VFX")]
    [SerializeField] VisualEffect visualEffect;
    [SerializeField] VisualEffectAsset vfxAsset;

    [Header("Growth & Glow")]
    [SerializeField] float startSize = 1f;
    [SerializeField] float endSize = 3.5f;
    [SerializeField] float startGlow = 1f;
    [SerializeField] float endGlow = 4f;
    [SerializeField] float growDuration = 1.1f;
    [SerializeField] float holdAfterGrow = 0.35f;

    [Header("Fallback (Particle System)")]
    [SerializeField] ParticleSystem fallbackParticles;
    [SerializeField] bool forceParticleFallback;

    Color _baseColor = new Color(0.27f, 0.70f, 3.7f, 0f);
    bool _hasColorParam;
    bool _hasSizeParam;
    bool _usingParticles;

    void Awake()
    {
        if (visualEffect == null)
            visualEffect = GetComponent<VisualEffect>();
        if (fallbackParticles == null)
            fallbackParticles = GetComponent<ParticleSystem>();
    }

    /// <summary>
    /// Spawns a fresh active effect instance (safe for player builds).
    /// </summary>
    public static CorrectAnswerVFX Spawn(Vector3 worldPosition, VisualEffectAsset asset, Transform numberToDestroy)
    {
        var go = new GameObject("CorrectAnswerEffect");
        go.transform.position = worldPosition;

        var controller = go.AddComponent<CorrectAnswerVFX>();
        controller.vfxAsset = asset != null ? asset : Resources.Load<VisualEffectAsset>(ResourcesVfxName);

        bool preferParticles =
            Application.platform == RuntimePlatform.WebGLPlayer ||
            controller.vfxAsset == null;

        if (!preferParticles)
        {
            var ve = go.AddComponent<VisualEffect>();
            ve.visualEffectAsset = controller.vfxAsset;
            controller.visualEffect = ve;
        }

        // Always build particles so we can fall back if VFX fails to start.
        controller.fallbackParticles = BuildBlueOrbParticles(go);
        controller.forceParticleFallback = preferParticles;
        controller.PlayAt(worldPosition, numberToDestroy);
        return controller;
    }

    public void SetVfxAsset(VisualEffectAsset asset)
    {
        vfxAsset = asset;
        if (visualEffect != null && asset != null)
            visualEffect.visualEffectAsset = asset;
    }

    public void PlayAt(Vector3 worldPosition, Transform numberToDestroy)
    {
        transform.position = worldPosition;
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        StartCoroutine(PlayRoutine(numberToDestroy));
    }

    IEnumerator PlayRoutine(Transform numberToDestroy)
    {
        // One frame so VisualEffect can initialize after activation / asset assign.
        yield return null;

        PrepareEffect();

        float elapsed = 0f;
        while (elapsed < growDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / growDuration);
            ApplyProgress(EaseOutQuad(t));
            yield return null;
        }

        ApplyProgress(1f);
        yield return new WaitForSeconds(holdAfterGrow);

        if (numberToDestroy != null)
            Destroy(numberToDestroy.gameObject);

        Destroy(gameObject);
    }

    void PrepareEffect()
    {
        if (fallbackParticles == null)
            fallbackParticles = BuildBlueOrbParticles(gameObject);

        bool tryVfx = !forceParticleFallback && Application.platform != RuntimePlatform.WebGLPlayer;
        if (tryVfx)
            tryVfx = TryStartVisualEffect();

        _usingParticles = !tryVfx;
        if (_usingParticles)
            StartParticles();
    }

    bool TryStartVisualEffect()
    {
        if (visualEffect == null)
            visualEffect = GetComponent<VisualEffect>();

        if (vfxAsset == null)
            vfxAsset = Resources.Load<VisualEffectAsset>(ResourcesVfxName);

        if (visualEffect == null)
        {
            if (vfxAsset == null)
                return false;
            visualEffect = gameObject.AddComponent<VisualEffect>();
        }

        if (visualEffect.visualEffectAsset == null)
        {
            if (vfxAsset == null)
                return false;
            visualEffect.visualEffectAsset = vfxAsset;
        }

        try
        {
            visualEffect.enabled = true;
            visualEffect.Reinit();

            _hasSizeParam = visualEffect.HasFloat(SizeParam);
            _hasColorParam = visualEffect.HasVector4(ColorParam);

            if (_hasColorParam)
                _baseColor = visualEffect.GetVector4(ColorParam);

            if (_hasSizeParam)
                visualEffect.SetFloat(SizeParam, startSize);
            if (_hasColorParam)
                visualEffect.SetVector4(ColorParam, ScaleHdr(_baseColor, startGlow));

            visualEffect.Play();

            // If the system still has no asset after Play, treat as failure.
            if (visualEffect.visualEffectAsset == null)
                return false;

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Visual Effect failed in build; using particle fallback. " + e.Message);
            return false;
        }
    }

    void StartParticles()
    {
        if (fallbackParticles == null)
            return;

        if (visualEffect != null)
            visualEffect.enabled = false;

        var main = fallbackParticles.main;
        main.startSizeMultiplier = startSize;
        fallbackParticles.Play(true);
    }

    void ApplyProgress(float t)
    {
        float size = Mathf.Lerp(startSize, endSize, t);
        float glow = Mathf.Lerp(startGlow, endGlow, t);

        if (!_usingParticles && visualEffect != null && visualEffect.enabled)
        {
            if (_hasSizeParam)
                visualEffect.SetFloat(SizeParam, size);
            if (_hasColorParam)
                visualEffect.SetVector4(ColorParam, ScaleHdr(_baseColor, glow));
        }

        if (fallbackParticles != null && (_usingParticles || forceParticleFallback))
        {
            var main = fallbackParticles.main;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.08f, size * 0.18f);

            var shape = fallbackParticles.shape;
            shape.radius = Mathf.Lerp(0.35f, 1.4f, t);
        }
    }

    static Color ScaleHdr(Color c, float multiplier)
    {
        return new Color(c.r * multiplier, c.g * multiplier, c.b * multiplier, c.a);
    }

    static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);

    /// <summary>Blue glowing orb made with the built-in ParticleSystem (build-safe).</summary>
    public static ParticleSystem BuildBlueOrbParticles(GameObject host)
    {
        var existing = host.GetComponent<ParticleSystem>();
        if (existing != null)
            return existing;

        var ps = host.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 2f;
        main.startLifetime = 0.9f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new Color(0.35f, 0.75f, 1f, 1f);
        main.maxParticles = 250;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = 80f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.45f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.45f, 0.85f, 1f), 0f),
                new GradientColorKey(new Color(0.2f, 0.45f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(0.8f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = grad;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f));

        var renderer = host.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        Material mat = Resources.Load<Material>("BlueOrbParticle");
        if (mat == null)
        {
            string[] shaderNames =
            {
                "Universal Render Pipeline/Particles/Unlit",
                "Particles/Standard Unlit",
                "Particles/Unlit",
                "Legacy Shaders/Particles/Additive",
                "Mobile/Particles/Additive"
            };
            for (int i = 0; i < shaderNames.Length; i++)
            {
                var shader = Shader.Find(shaderNames[i]);
                if (shader != null && shader.isSupported)
                {
                    mat = new Material(shader);
                    var blue = new Color(0.4f, 0.85f, 1f, 1f);
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", blue);
                    if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", blue);
                    if (mat.HasProperty("_TintColor"))
                        mat.SetColor("_TintColor", blue);
                    break;
                }
            }
        }

        if (mat == null)
        {
            var builtin = Resources.GetBuiltinResource<Material>("Default-Particle.mat");
            if (builtin != null)
                mat = new Material(builtin);
        }

        if (mat != null)
            renderer.material = mat;

        return ps;
    }
}
