using UnityEngine;

/// <summary>
/// Vay-huong (facing flip) va nghieng-theo-huong-boi (tilt) dung chung cho moi
/// script chuyen dong cua ca: truoc day DOTweenFishAnim, SeahorseHoverAnim va
/// JellyfishDriftAnim moi noi tu viet lai cung 1 cong thuc voi bien the hoi
/// khac nhau - sua o 1 noi de kiem tra lai roi quen mat 2 noi con lai la
/// nguyen nhan chinh gay ra hang loat bug lech nhau giua cac loai trong phien
/// lam viec truoc. Gio moi noi goi chung qua day, chi con 1 cho de sua.
/// </summary>
public static class FishFacing
{
    public static void Apply(Transform transform, int direction, bool spriteFacesRight)
    {
        float baseScale = Mathf.Abs(transform.localScale.x);
        float signedX = direction > 0
            ? (spriteFacesRight ? baseScale : -baseScale)
            : (spriteFacesRight ? -baseScale : baseScale);
        transform.localScale = new Vector3(signedX, transform.localScale.y, transform.localScale.z);
    }
}

/// <summary>
/// Giu trang thai goc-nghieng-da-lam-muot rieng cho tung con ca (1 instance
/// moi con, khong static). Truyen downwardTiltMultiplier=1, minimumDownwardTilt=0
/// de tat han phan "chui manh hon khi lan xuong" cho loai khong can no (vd
/// Seahorse/Jellyfish) - cong thuc luc do tu rut gon thanh y het truoc khi gop.
/// </summary>
public sealed class FishTiltRotation
{
    float currentAngle;

    public void Reset()
    {
        currentAngle = 0f;
    }

    // Tra ve goc muc tieu THO (truoc khi lam muot) de noi goi con dung lam
    // viec khac neu can (vd DOTweenFishAnim dung no de tang bien do vay duoi
    // luc re gap). extraOffsetAngle: cong them khong lam muot (vd PlaySpin()
    // cua DOTweenFishAnim, hoac headTiltOffset rieng cua Seahorse).
    public float Apply(
        Transform transform,
        Vector3 movement,
        int facingDirection,
        float maxTiltAngle,
        float tiltSmoothSpeed,
        float downwardTiltMultiplier,
        float minimumDownwardTilt,
        float extraOffsetAngle,
        float deltaTime)
    {
        if (movement.sqrMagnitude <= 0.0000001f)
            return currentAngle;

        float pathAngle = Mathf.Atan2(movement.y, Mathf.Abs(movement.x)) * Mathf.Rad2Deg;
        if (movement.y < -0.0001f)
        {
            float downwardAngle = Mathf.Abs(pathAngle) * downwardTiltMultiplier;
            pathAngle = -Mathf.Max(downwardAngle, minimumDownwardTilt);
        }

        float angle = Mathf.Clamp(pathAngle * facingDirection, -maxTiltAngle, maxTiltAngle);
        float responseSpeed = movement.y < 0f ? tiltSmoothSpeed * 1.5f : tiltSmoothSpeed;
        // Cham deltaTime (vd Editor giat frame do compile/GC/tai asset) lam
        // "smoothing" tien sat 1 chi trong 1 frame do - currentAngle nhay
        // thang toi gan het muc tieu, nhin y het bi set gia tri tuc thi. Gioi
        // han deltaTime dau vao de 1 frame lag khong the "an" ca nhieu frame
        // lam muot cung luc - day la nguyen nhan chinh gay "luc muot luc khung".
        float clampedDeltaTime = Mathf.Min(deltaTime, 1f / 30f);
        float smoothing = 1f - Mathf.Exp(-responseSpeed * clampedDeltaTime);
        currentAngle = Mathf.LerpAngle(currentAngle, angle, smoothing);
        transform.rotation = Quaternion.Euler(0f, 0f, currentAngle + extraOffsetAngle);
        return angle;
    }
}
