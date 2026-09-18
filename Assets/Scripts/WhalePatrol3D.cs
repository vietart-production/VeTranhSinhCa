using UnityEngine;

/// <summary>
/// Calm screen-space patrol for the decorative 3D whale.
/// The prefab is authored facing along its local forward axis after the base rotation.
/// </summary>
public sealed class WhalePatrol3D : MonoBehaviour
{
    [Header("Kich thuoc")]
    [Tooltip("Kich thuoc tong the cua ca voi. Co the chinh truc tiep khi dang Play.")]
    [Min(0.1f)] public float whaleScale = 1.5f;

    [Header("Duong boi")]
    [Tooltip("Khoang cach tu tam den moi dau cua duong boi.")]
    [Min(0.5f)] public float patrolHalfWidth = 12f;
    [Tooltip("Toc do boi ngang trung binh.")]
    [Min(0.05f)] public float swimSpeed = 1.15f;
    [Tooltip("Bien do nhip nho len xuong.")]
    [Min(0f)] public float verticalBob = 0.3f;
    [Tooltip("Toc do nhip nho len xuong.")]
    [Min(0f)] public float bobSpeed = 0.65f;

    [Header("Quay dau")]
    [Tooltip("Toc do xoay khi ca voi doi huong.")]
    [Min(0.1f)] public float turnSpeed = 2.5f;
    [Tooltip("Bat neu model dang quay lung voi huong di chuyen.")]
    public bool invertFacing;

    Vector3 patrolCenter;
    Quaternion rightRotation;
    float phase;
    float lastScale = -1f;

    void Awake()
    {
        patrolCenter = transform.position;
        rightRotation = transform.rotation;
        phase = Random.Range(0f, Mathf.PI * 2f);
        ApplyScale();
    }

    void OnValidate()
    {
        whaleScale = Mathf.Max(0.1f, whaleScale);
        patrolHalfWidth = Mathf.Max(0.5f, patrolHalfWidth);
        swimSpeed = Mathf.Max(0.05f, swimSpeed);
        turnSpeed = Mathf.Max(0.1f, turnSpeed);
        ApplyScale();
    }

    void Update()
    {
        ApplyScale();

        // A sine path naturally eases at both ends instead of abruptly reversing.
        float angularSpeed = swimSpeed / Mathf.Max(0.5f, patrolHalfWidth);
        phase += angularSpeed * Time.deltaTime;

        float horizontal = Mathf.Sin(phase) * patrolHalfWidth;
        float vertical = Mathf.Sin(Time.time * bobSpeed + phase * 0.35f) * verticalBob;
        transform.position = patrolCenter + new Vector3(horizontal, vertical, 0f);

        bool movingRight = Mathf.Cos(phase) >= 0f;
        if (invertFacing)
            movingRight = !movingRight;

        Quaternion targetRotation = movingRight
            ? rightRotation
            : rightRotation * Quaternion.Euler(0f, 180f, 0f);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
    }

    void ApplyScale()
    {
        if (Mathf.Approximately(lastScale, whaleScale))
            return;

        transform.localScale = Vector3.one * whaleScale;
        lastScale = whaleScale;
    }
}
