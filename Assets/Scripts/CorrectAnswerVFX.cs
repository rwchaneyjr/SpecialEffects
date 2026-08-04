using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Plays the correct-answer Visual Effect Graph: grows particle size and intensifies glow,
/// then destroys the VFX and the linked number.
/// Exposed VFX parameters used: "size", "New Color" (optional: "SpawnRate", "trailRate").
/// </summary>
public class CorrectAnswerVFX : MonoBehaviour
{
    const string SizeParam = "size";
    const string ColorParam = "New Color";

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

    Color _baseColor = new Color(0.27f, 0.70f, 3.7f, 0f);
    bool _hasColorParam;
    bool _hasSizeParam;

    void Awake()
    {
        if (visualEffect == null)
            visualEffect = GetComponent<VisualEffect>();
        if (fallbackParticles == null)
            fallbackParticles = GetComponent<ParticleSystem>();
    }

    /// <summary>
    /// Spawns/plays the effect at <paramref name="worldPosition"/>, grows size + glow, then destroys
    /// the effect object and <paramref name="numberToDestroy"/>.
    /// </summary>
    public void PlayAt(Vector3 worldPosition, Transform numberToDestroy)
    {
        transform.position = worldPosition;
        gameObject.SetActive(true);
        StartCoroutine(PlayRoutine(numberToDestroy));
    }

    IEnumerator PlayRoutine(Transform numberToDestroy)
    {
        PrepareVisualEffect();

        float elapsed = 0f;
        while (elapsed < growDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / growDuration);
            float eased = EaseOutQuad(t);
            ApplyProgress(eased);
            yield return null;
        }

        ApplyProgress(1f);
        yield return new WaitForSeconds(holdAfterGrow);

        if (numberToDestroy != null)
            Destroy(numberToDestroy.gameObject);

        Destroy(gameObject);
    }

    void PrepareVisualEffect()
    {
        if (visualEffect != null)
        {
            if (vfxAsset != null && visualEffect.visualEffectAsset == null)
                visualEffect.visualEffectAsset = vfxAsset;

            _hasSizeParam = visualEffect.HasFloat(SizeParam);
            _hasColorParam = visualEffect.HasVector4(ColorParam);

            if (_hasColorParam)
                _baseColor = visualEffect.GetVector4(ColorParam);

            if (_hasSizeParam)
                visualEffect.SetFloat(SizeParam, startSize);
            if (_hasColorParam)
                visualEffect.SetVector4(ColorParam, ScaleHdr(_baseColor, startGlow));

            visualEffect.Play();
            return;
        }

        if (fallbackParticles != null)
        {
            var main = fallbackParticles.main;
            main.startSizeMultiplier = startSize;
            fallbackParticles.Play(true);
        }
    }

    void ApplyProgress(float t)
    {
        float size = Mathf.Lerp(startSize, endSize, t);
        float glow = Mathf.Lerp(startGlow, endGlow, t);

        if (visualEffect != null)
        {
            if (_hasSizeParam)
                visualEffect.SetFloat(SizeParam, size);
            if (_hasColorParam)
                visualEffect.SetVector4(ColorParam, ScaleHdr(_baseColor, glow));
            return;
        }

        if (fallbackParticles != null)
        {
            var main = fallbackParticles.main;
            main.startSizeMultiplier = size;
        }
    }

    static Color ScaleHdr(Color c, float multiplier)
    {
        return new Color(c.r * multiplier, c.g * multiplier, c.b * multiplier, c.a);
    }

    static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);
}
