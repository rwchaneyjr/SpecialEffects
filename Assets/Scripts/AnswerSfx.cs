using UnityEngine;

/// <summary>
/// Plays correct / wrong answer sounds. Uses optional clips if assigned,
/// otherwise generates short pleasant procedural tones so SFX work with no audio assets.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AnswerSfx : MonoBehaviour
{
    [Header("Optional clips (leave empty to use built-in tones)")]
    [SerializeField] AudioClip correctClip;
    [SerializeField] AudioClip wrongClip;

    [Header("Volumes")]
    [SerializeField] [Range(0f, 1f)] float correctVolume = 0.9f;
    [SerializeField] [Range(0f, 1f)] float wrongVolume = 0.55f;

    AudioSource _source;
    AudioClip _generatedCorrect;
    AudioClip _generatedWrong;

    void Awake()
    {
        EnsureSource();
        BuildGeneratedClipsIfNeeded();
    }

    public void Configure(AudioClip correct, AudioClip wrong)
    {
        correctClip = correct;
        wrongClip = wrong;
        EnsureSource();
        BuildGeneratedClipsIfNeeded();
    }

    public void PlayCorrect()
    {
        EnsureSource();
        var clip = correctClip != null ? correctClip : _generatedCorrect;
        if (clip == null)
            return;
        _source.PlayOneShot(clip, correctVolume);
    }

    public void PlayWrong()
    {
        EnsureSource();
        var clip = wrongClip != null ? wrongClip : _generatedWrong;
        if (clip == null)
            return;
        _source.PlayOneShot(clip, wrongVolume);
    }

    void EnsureSource()
    {
        if (_source != null)
            return;

        _source = GetComponent<AudioSource>();
        if (_source == null)
            _source = gameObject.AddComponent<AudioSource>();

        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        _source.loop = false;
    }

    void BuildGeneratedClipsIfNeeded()
    {
        if (correctClip == null && _generatedCorrect == null)
            _generatedCorrect = BuildCorrectChime();
        if (wrongClip == null && _generatedWrong == null)
            _generatedWrong = BuildWrongBuzz();
    }

    /// <summary>Big rising blast (C4 → E5 → G5) for a more explosive correct hit.</summary>
    static AudioClip BuildCorrectChime()
    {
        const int sampleRate = 44100;
        float[] freqs = { 261.63f, 329.63f, 523.25f, 783.99f };
        float noteLen = 0.11f;
        float gap = 0.02f;
        int totalSamples = Mathf.CeilToInt(sampleRate * (noteLen * freqs.Length + gap * (freqs.Length - 1) + 0.2f));
        var data = new float[totalSamples];

        int cursor = 0;
        for (int n = 0; n < freqs.Length; n++)
        {
            int noteSamples = Mathf.RoundToInt(sampleRate * noteLen);
            float amp = 0.22f + n * 0.04f;
            WriteTone(data, cursor, noteSamples, sampleRate, freqs[n], amp, true);
            // Add a soft low thump under the first note
            if (n == 0)
                WriteTone(data, cursor, noteSamples, sampleRate, 90f, 0.35f, true);
            cursor += noteSamples;
            if (n < freqs.Length - 1)
                cursor += Mathf.RoundToInt(sampleRate * gap);
        }

        var clip = AudioClip.Create("CorrectBlast", totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Short descending soft thud (not harsh).</summary>
    static AudioClip BuildWrongBuzz()
    {
        const int sampleRate = 44100;
        float duration = 0.28f;
        int totalSamples = Mathf.CeilToInt(sampleRate * duration);
        var data = new float[totalSamples];

        // Slide from ~220Hz down to ~140Hz with gentle damping.
        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (float)sampleRate;
            float u = i / (float)(totalSamples - 1);
            float freq = Mathf.Lerp(220f, 140f, u);
            float env = Mathf.Exp(-4.5f * u) * (1f - 0.15f * u);
            float sample = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.32f;
            // Soften with a little triangle blend so it isn't a pure beep.
            float tri = 2f * Mathf.Abs(2f * ((freq * t) % 1f) - 1f) - 1f;
            data[i] = sample * 0.75f + tri * env * 0.08f;
        }

        var clip = AudioClip.Create("WrongBuzz", totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static void WriteTone(float[] data, int start, int count, int sampleRate, float freq, float amplitude, bool softAttack)
    {
        for (int i = 0; i < count; i++)
        {
            int idx = start + i;
            if (idx < 0 || idx >= data.Length)
                continue;

            float u = i / (float)(count - 1);
            float t = i / (float)sampleRate;

            float attack = softAttack ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.12f)) : 1f;
            float release = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((u - 0.55f) / 0.45f));
            float env = attack * release;

            // Soft sine + quiet harmonic for a nicer "bell" feel.
            float s =
                Mathf.Sin(2f * Mathf.PI * freq * t) * 0.85f +
                Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.12f;

            data[idx] += s * env * amplitude;
        }
    }
}
