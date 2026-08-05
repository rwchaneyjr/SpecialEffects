using UnityEngine;

/// <summary>
/// Raycasts from the main camera on left mouse click and notifies AnswerChoice.
/// </summary>
public class MouseAnswerPicker : MonoBehaviour
{
    [SerializeField] Camera rayCamera;
    [SerializeField] LayerMask answerMask = ~0;
    [SerializeField] float maxDistance = 200f;

    void Awake()
    {
        if (rayCamera == null)
            rayCamera = Camera.main;
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        if (rayCamera == null)
            rayCamera = Camera.main;
        if (rayCamera == null)
            return;

        Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, answerMask, QueryTriggerInteraction.Ignore))
            return;

        var choice = hit.collider.GetComponentInParent<AnswerChoice>();
        if (choice != null)
            choice.OnPicked();
    }
}
