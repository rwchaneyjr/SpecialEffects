using System.Collections;
using UnityEngine;

/// <summary>
/// A clickable answer that floats up into view. Attach to the root of each answer object.
/// Requires a Collider on this object or a child for mouse raycasts.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class AnswerChoice : MonoBehaviour
{
    public int Value { get; private set; }
    public bool IsCorrect { get; private set; }
    public bool IsInteractable { get; private set; } = true;

    MathGameManager _game;
    Vector3 _startPos;
    Vector3 _endPos;
    float _floatDuration;
    Coroutine _floatRoutine;

    public void Setup(MathGameManager game, int value, bool isCorrect, Vector3 startPos, Vector3 endPos, float floatDuration)
    {
        _game = game;
        Value = value;
        IsCorrect = isCorrect;
        _startPos = startPos;
        _endPos = endPos;
        _floatDuration = Mathf.Max(0.01f, floatDuration);

        transform.position = _startPos;
        EnsureCollider();

        if (_floatRoutine != null)
            StopCoroutine(_floatRoutine);
        _floatRoutine = StartCoroutine(FloatUp());
    }

    public void SetInteractable(bool interactable)
    {
        IsInteractable = interactable;
    }

    public void OnPicked()
    {
        if (!IsInteractable || _game == null)
            return;

        _game.OnAnswerSelected(this);
    }

    IEnumerator FloatUp()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / _floatDuration;
            float eased = EaseOutCubic(Mathf.Clamp01(t));
            transform.position = Vector3.LerpUnclamped(_startPos, _endPos, eased);
            yield return null;
        }

        transform.position = _endPos;
        _floatRoutine = null;
    }

    static float EaseOutCubic(float x)
    {
        float inv = 1f - x;
        return 1f - inv * inv * inv;
    }

    void EnsureCollider()
    {
        var col = GetComponent<Collider>();
        if (col != null)
            return;

        // Prefer fitting a box to child renderers so 3D digit prefabs are clickable.
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = Vector3.one;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        var boxCol = gameObject.AddComponent<BoxCollider>();
        boxCol.center = transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = transform.InverseTransformVector(bounds.size);
        boxCol.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
    }
}
