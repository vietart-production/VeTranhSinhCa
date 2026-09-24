using UnityEngine;

/// <summary>
/// Ep cua so build luon mo dung kich thuoc co dinh (khong fullscreen, khong bi stretch/resize),
/// de hinh gui qua Spout vao Resolume luon dung kich thuoc ro rang. Chay som nhat co the
/// (RuntimeInitializeLoadType.BeforeSceneLoad) vi Player Settings mac dinh cua Windows co the
/// bi ghi de boi do phan giai da luu tu lan chay truoc.
/// </summary>
public static class FixedWindowBootstrap
{
    public const int TargetWidth = 2700;
    public const int TargetHeight = 1200;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyFixedWindow()
    {
#if !UNITY_EDITOR
        Screen.SetResolution(TargetWidth, TargetHeight, FullScreenMode.Windowed);
#endif
    }
}
