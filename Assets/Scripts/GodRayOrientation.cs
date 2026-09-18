using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class GodRayOrientation : MonoBehaviour
{
    [Header("Huong tia sang")]
    [Tooltip("Huong chieu trong world space. Y am = chieu tu mat nuoc xuong day bien.")]
    public Vector3 rayDirection = new Vector3(0.18f, -1f, 0f);

    [Tooltip("Bat: dung Ray Direction. Tat: co the xoay Transform thu cong.")]
    public bool autoOrient = true;

    [Tooltip("Giu particle trong local space de ca chum tia xoay theo huong da chon.")]
    public bool forceLocalSimulation = true;

    [Tooltip("Ep ray thanh billboard doi dien camera. Can bat cho scene 2.5D hien tai.")]
    public bool faceCamera = true;

    void OnEnable()
    {
        ApplyOrientation();
    }

    void OnValidate()
    {
        ApplyOrientation();
    }

    [ContextMenu("Apply God Ray Orientation")]
    public void ApplyOrientation()
    {
        ParticleSystem particleSystem = GetComponent<ParticleSystem>();
        ParticleSystemRenderer particleRenderer = GetComponent<ParticleSystemRenderer>();
        if (forceLocalSimulation && particleSystem != null)
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
        }

        // Prefab goc dung Horizontal Billboard, nen Unity luon ep cac tia nam
        // tren mat phang XZ va rotation cua Transform khong the dung tia len.
        if (faceCamera && particleRenderer != null)
        {
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;
        }

        Vector2 screenDirection = new Vector2(rayDirection.x, rayDirection.y);
        if (!autoOrient || particleSystem == null || screenDirection.sqrMagnitude < 0.000001f)
            return;

        // Billboard luon doi dien camera; vi vay huong tia phai duoc dat bang
        // startRotation quanh truc camera, khong phai Quaternion.LookRotation.
        float screenAngle = Mathf.Atan2(-screenDirection.x, -screenDirection.y);
        ParticleSystem.MainModule orientedMain = particleSystem.main;
        orientedMain.startRotation = screenAngle;
    }

    void OnDrawGizmosSelected()
    {
        if (rayDirection.sqrMagnitude < 0.000001f)
            return;

        Vector3 origin = transform.position;
        Vector3 direction = new Vector3(rayDirection.x, rayDirection.y, 0f).normalized;
        float length = 3f;
        Vector3 end = origin + direction * length;

        Gizmos.color = new Color(1f, 0.9f, 0.35f, 1f);
        Gizmos.DrawLine(origin, end);

        Vector3 side = Vector3.Cross(direction, Vector3.forward).normalized;
        if (side.sqrMagnitude < 0.000001f)
            side = Vector3.right;

        Gizmos.DrawLine(end, end - direction * 0.45f + side * 0.22f);
        Gizmos.DrawLine(end, end - direction * 0.45f - side * 0.22f);
    }
}
