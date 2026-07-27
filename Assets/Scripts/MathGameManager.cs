using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Math clicker loop:
/// - Shows an equation in TMP (e.g. "2+7")
/// - Spawns 1 correct answer + 2 decoys that float up
/// - Mouse click: correct → VFX glow/grow then disappear; wrong → "Try again"
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
    [SerializeField] bool allowSubtraction = true;

    [Header("Feedback")]
    [SerializeField] string tryAgainMessage = "Try again";
    [SerializeField] float tryAgainDisplaySeconds = 1.2f;

    readonly List<AnswerChoice> _activeAnswers = new List<AnswerChoice>();
    int _correctValue;
    bool _roundLocked;
    Coroutine _feedbackRoutine;

    void Start()
    {
        if (feedbackText != null)
            feedbackText.text = string.Empty;

        StartNewRound();
    }

    public void StartNewRound()
    {
        StopAllCoroutines();
        _feedbackRoutine = null;
        ClearAnswers();
        _roundLocked = false;

        if (feedbackText != null)
            feedbackText.text = string.Empty;

        GenerateProblem(out int a, out int b, out char op, out _correctValue);

        if (equationText != null)
            equationText.text = $"{a} {op} {b}";

        SpawnAnswers(_correctValue);
    }

    public void OnAnswerSelected(AnswerChoice choice)
    {
        if (_roundLocked || choice == null)
            return;

        if (choice.IsCorrect)
        {
            _roundLocked = true;
            SetAllInteractable(false);
            StartCoroutine(HandleCorrect(choice));
        }
        else
        {
            ShowTryAgain();
        }
    }

    IEnumerator HandleCorrect(AnswerChoice choice)
    {
        if (equationText != null)
            equationText.text = string.Empty;

        // Remove decoys immediately so only the correct number + VFX remain.
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

        if (correctVfxPrefab != null)
        {
            var vfx = Instantiate(correctVfxPrefab, vfxPos, Quaternion.identity);
            if (correctVfxAsset != null)
            {
                var ve = vfx.GetComponent<VisualEffect>();
                if (ve != null && ve.visualEffectAsset == null)
                    ve.visualEffectAsset = correctVfxAsset;
            }

            vfx.PlayAt(vfxPos, numberTransform);

            // Wait until the VFX script destroys the number, then a short beat before the next round.
            while (numberTransform != null)
                yield return null;
        }
        else
        {
            // No VFX assigned — still disappear the number after a short beat.
            yield return new WaitForSeconds(0.6f);
            if (numberTransform != null)
                Destroy(numberTransform.gameObject);
        }

        yield return new WaitForSeconds(nextRoundDelay);
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

    void GenerateProblem(out int a, out int b, out char op, out int result)
    {
        a = Random.Range(minOperand, maxOperand + 1);
        b = Random.Range(minOperand, maxOperand + 1);

        bool subtract = allowSubtraction && Random.value < 0.45f;
        if (subtract)
        {
            // Keep non-negative results for digit prefabs.
            if (b > a)
            {
                int tmp = a;
                a = b;
                b = tmp;
            }

            op = '-';
            result = a - b;
        }
        else
        {
            op = '+';
            result = a + b;
        }
    }

    int MakeDecoy(int correct, int? alsoAvoid = null)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            int offset = Random.Range(1, 5);
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

        // Guaranteed fallback.
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
