using UnityEngine;

public class AutoRotate : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("1•bŠÔ‚ ‚½‚è‚Ì‰ñ“]Šp“x (“x)")]
    [SerializeField] private Vector3 _rotationSpeed = new Vector3(0, 5.0f, 0);

    public bool IsRotating { get; set; } = true;

    private void Update()
    {
        if (!IsRotating) return;

        transform.Rotate(_rotationSpeed * Time.deltaTime, Space.Self);
    }
}