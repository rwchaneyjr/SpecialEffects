using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Math clicker loop:
/// - Start menu picks Addition / Subtraction / Times / Divide
/// - Shows an equation in TMP
/// - Spawns 1 correct answer + 2 decoys that float up
/// - Mouse click: correct → VFX glow/grow then disappear; wrong → "Try again"
/// - Esc opens the practice menu again
/// </summary>
public class MathGameManager : MonoBehaviour
{
    [Header("UI (TMP)")]
    [SerializeField] TextMeshProUGUI equationText;
    [SerializeField] TextMeshProUGUI feedbackText;

    [Header("Spawning")]
    [SerializeField] NumberVisualBuilder numberBuilder;
    [SerializeField] Transform spawnArea;
    [SerializeField] Vector3 spawnStartOffset = new Vector3(0f, -4f, 0f);
    [SerializeField] float answerSpacing = 2.5f;
    [SerializeField] float floatUpDuration = 1.2f;
    [SerializeField] float nextRoundDelay = 1.25f;

    [Header("VFX")]
    [Tooltip("Prefab with CorrectAnswerVFX + VisualEffect (New VFX).")]
    [SerializeField] CorrectAnswerVFX correctVfxPrefab;
    [Tooltip("Optional: assign New VFX asset if the prefab has an empty VisualEffect.")]
    [SerializeField] VisualEffectAsset correctVfxAsset;

    [Header("Problem Difficulty")]
    [SerializeField] int minOperand = 1;
    [SerializeField] int maxOperand = 9;
    [SerializeField] int timesTableMax = 12;

    [Header("Feedback")]
    [SerializeField] string tryAgainMessage = "Try again";
    [SerializeField] float tryAgainDisplaySeconds = 1.2f;
    [SerializeField] AnswerSfx answerSfx;

    readonly List<AnswerChoice> _activeAnswers = new List<AnswerChoice>();
    readonly List<MathOp> _enabledOps = new List<MathOp> { MathOp.Add };
    readonly List<int> _allowedTables = new List<int>();
    int _correctValue;
    bool _roundLocked;
    bool _isPlaying;
    Coroutine _feedbackRoutine;

    public bool IsPlaying => _isPlaying;

    /// <summary>
    /// Used by MathGameBootstrap / editor setup to wire references at runtime.
    /// </summary>
    public void Configure(
        TextMeshProUGUI equation,
        TextMeshProUGUI feedback,
        NumberVisualBuilder builder,
        Transform spawn,
        CorrectAnswerVFX vfxPrefab,
        VisualEffectAsset vfxAsset)
    {
        equationText = equation;
        feedbackText = feedback;
        numberBuilder = builder;
        spawnArea = spawn;
        correctVfxPrefab = vfxPrefab;
        correctVfxAsset = vfxAsset;
        EnsureSfx();
    }

    void Awake()
    {
        EnsureSfx();
    }

    void EnsureSfx()
    {
        if (answerSfx == null)
            answerSfx = GetComponent<AnswerSfx>();
        if (answerSfx == null)
            answerSfx = gameObject.AddComponent<AnswerSfx>();
    }

    void Start()
    {
        if (feedbackText != null)
            feedbackText.text = string.Empty;
        if (equationText != null)
            equationText.text = string.Empty;

        // Wait for the practice menu unless something already started play.
        if (!_isPlaying)
            PauseToMenu();
    }

    public void BeginPractice(List<MathOp> ops, List<int> allowedTables)
    {
        _enabledOps.Clear();
        if (ops != null)
        {
            for (int i = 0; i < ops.Count; i++)
            {
                if (!_enabledOps.Contains(ops[i]))
                    _enabledOps.Add(ops[i]);
            }
        }

        if (_enabledOps.Count == 0)
            _enabledOps.Add(MathOp.Add);

        _allowedTables.Clear();
        if (allowedTables != null && allowedTables.Count > 0)
        {
            for (int i = 0; i < allowedTables.Count; i++)
            {
                int v = allowedTables[i];
                if (v >= 1 && v <= timesTableMax && !_allowedTables.Contains(v))
                    _allowedTables.Add(v);
            }
        }

        // If nothing picked, allow all.
        if (_allowedTables.Count == 0)
        {
            for (int v = 1; v <= timesTableMax; v++)
                _allowedTables.Add(v);
        }

        _isPlaying = true;
        if (feedbackText != null)
            feedbackText.text = "PRACTICING";
        StartNewRound();
    }

    public void PauseToMenu()
    {
        _isPlaying = false;
        _roundLocked = true;
        StopAllCoroutines();
        _feedbackRoutine = null;
        ClearAnswers();

        if (equationText != null)
            equationText.text = string.Empty;
        if (feedbackText != null)
            feedbackText.text = string.Empty;
    }

    public void StartNewRound()
    {
        if (!_isPlaying)
            return;

        StopAllCoroutines();
        _feedbackRoutine = null;
        ClearAnswers();
        _roundLocked = false;

        GenerateProblem(out int a, out int b, out string opSymbol, out _correctValue);

        if (equationText != null)
            equationText.text = $"{a} {opSymbol} {b}";

        SpawnAnswers(_correctValue);
    }

    public void OnAnswerSelected(AnswerChoice choice)
    {
        if (!_isPlaying || _roundLocked || choice == null)
            return;

        if (choice.IsCorrect)
        {
            _roundLocked = true;
            SetAllInteractable(false);
            if (answerSfx != null)
                answerSfx.PlayCorrect();
            StartCoroutine(HandleCorrect(choice));
        }
        else
        {
            if (answerSfx != null)
                answerSfx.PlayWrong();
            ShowTryAgain();
        }
    }

    IEnumerator HandleCorrect(AnswerChoice choice)
    {
        if (equationText != null)
            equationText.text = string.Empty;

        if (feedbackText != null)
            feedbackText.text = "Correct!";

        for (int i = _activeAnswers.Count - 1; i >= 0; i--)
        {
            var a = _activeAnswers[i];
            if (a == null || a == choice)
                continue;
            Destroy(a.gameObject);
            _activeAnswers.RemoveAt(i);
        }

        Vector3 vfxPos = choice.transform.position;
        Transform numberTransform = choice.transform;
        _activeAnswers.Remove(choice);

        VisualEffectAsset asset = correctVfxAsset;
        if (asset == null && correctVfxPrefab != null)
        {
            var ve = correctVfxPrefab.GetComponent<UnityEngine.VFX.VisualEffect>();
            if (ve != null)
                asset = ve.visualEffectAsset;
        }

        // Spawn a fresh active effect (build-safe). Falls back to particles on WebGL / failure.
        CorrectAnswerVFX.Spawn(vfxPos, asset, numberTransform);

        while (numberTransform != null)
            yield return null;

        if (!_isPlaying)
            yield break;

        yield return new WaitForSeconds(nextRoundDelay);

        if (_isPlaying)
            StartNewRound();
    }

    void ShowTryAgain()
    {
        if (_feedbackRoutine != null)
            StopCoroutine(_feedbackRoutine);
        _feedbackRoutine = StartCoroutine(TryAgainRoutine());
    }

    IEnumerator TryAgainRoutine()
    {
        if (feedbackText != null)
            feedbackText.text = tryAgainMessage;

        yield return new WaitForSeconds(tryAgainDisplaySeconds);

        if (feedbackText != null)
            feedbackText.text = string.Empty;

        _feedbackRoutine = null;
    }

    void SpawnAnswers(int correct)
    {
        if (numberBuilder == null)
        {
            Debug.LogError("MathGameManager: NumberVisualBuilder is not assigned.");
            return;
        }

        if (spawnArea == null)
            spawnArea = transform;

        int decoyA = MakeDecoy(correct);
        int decoyB = MakeDecoy(correct, decoyA);

        var values = new List<(int value, bool correct)>
        {
            (correct, true),
            (decoyA, false),
            (decoyB, false)
        };
        Shuffle(values);

        Vector3 center = spawnArea.position;
        float startX = -answerSpacing;
        for (int i = 0; i < values.Count; i++)
        {
            var choice = numberBuilder.CreateNumber(values[i].value, spawnArea);
            Vector3 end = center + new Vector3(startX + i * answerSpacing, 0f, 0f);
            Vector3 start = end + spawnStartOffset;
            choice.Setup(this, values[i].value, values[i].correct, start, end, floatUpDuration);
            _activeAnswers.Add(choice);
        }
    }

    void GenerateProblem(out int a, out int b, out string opSymbol, out int result)
    {
        MathOp op = _enabledOps[Random.Range(0, _enabledOps.Count)];

        switch (op)
        {
            case MathOp.Subtract:
                a = Random.Range(minOperand, maxOperand + 1);
                b = Random.Range(minOperand, maxOperand + 1);
                if (b > a)
                {
                    int tmp = a;
                    a = b;
                    b = tmp;
                }
                opSymbol = "−";
                result = a - b;
                break;

            case MathOp.Multiply:
                a = _allowedTables[Random.Range(0, _allowedTables.Count)];
                b = _allowedTables[Random.Range(0, _allowedTables.Count)];
                opSymbol = "×";
                result = a * b;
                break;

            case MathOp.Divide:
                // Build whole-number division: a ÷ b = result
                b = _allowedTables[Random.Range(0, _allowedTables.Count)];
                result = Random.Range(1, timesTableMax + 1);
                a = b * result;
                opSymbol = "÷";
                break;

            case MathOp.Add:
            default:
                a = Random.Range(minOperand, maxOperand + 1);
                b = Random.Range(minOperand, maxOperand + 1);
                opSymbol = "+";
                result = a + b;
                break;
        }
    }

    int MakeDecoy(int correct, int? alsoAvoid = null)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            int offset = Random.Range(1, 6);
            if (Random.value < 0.5f)
                offset = -offset;

            int decoy = correct + offset;
            if (decoy < 0)
                continue;
            if (decoy == correct)
                continue;
            if (alsoAvoid.HasValue && decoy == alsoAvoid.Value)
                continue;
            return decoy;
        }

        int fallback = correct == 0 ? 1 : correct - 1;
        if (alsoAvoid.HasValue && fallback == alsoAvoid.Value)
            fallback = correct + 1;
        return Mathf.Max(0, fallback);
    }

    void SetAllInteractable(bool interactable)
    {
        for (int i = 0; i < _activeAnswers.Count; i++)
        {
            if (_activeAnswers[i] != null)
                _activeAnswers[i].SetInteractable(interactable);
        }
    }

    void ClearAnswers()
    {
        for (int i = 0; i < _activeAnswers.Count; i++)
        {
            if (_activeAnswers[i] != null)
                Destroy(_activeAnswers[i].gameObject);
        }

        _activeAnswers.Clear();
    }

    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
