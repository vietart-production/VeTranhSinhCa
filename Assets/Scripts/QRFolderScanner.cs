using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using ZXing;
using ZXing.Common;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class QRFolderScanner : MonoBehaviour
{
    [Serializable]
    public class FishTemplate
    {
        public string qrId;
        public GameObject prefab;

        // alphaTemplate/scanCrop KHONG con duoc dung de tao texture (xem BuildFishTexture) -
        // pipeline moi tu do 2 duong ke ngang tren trang va xoa nen trang thay vi ep theo
        // khung/mask rieng cho tung loai. Giu lai field de khong vo du lieu da luu trong scene.
        public Texture2D alphaTemplate;

        [Tooltip("Bật nếu model ở scale X dương quay đầu sang phải.")]
        public bool spriteFacesRight = true;

        public Rect scanCrop = new Rect(0.18f, 0.12f, 0.62f, 0.76f);

        [Tooltip("Xoay texture sau khi tạo (0/90/180/270 độ), dùng khi tranh in " +
                 "nằm ngang trên giấy (để vẽ to hơn, dễ tô hơn) nhưng con cá cần " +
                 "đứng theo hướng khác trong game, ví dụ cá ngựa vẽ nằm ngang " +
                 "nhưng vẫn đứng dọc khi bơi. Việc canh crop/scan vẫn diễn ra " +
                 "theo đúng hướng nằm ngang trên giấy; chỉ ảnh kết quả cuối cùng " +
                 "mới bị xoay.")]
        public int textureRotationDegrees;
    }

    [Header("Input")]
    // Nam ngoai Assets/ de Unity AssetDatabase khong tu dong import file scan
    // (tung gay xung dot File.Move voi tien trinh import nen cua Unity).
    public string folderPath = "Executive_folder";
    public string sourceFolderPath = @"D:\Images";
    public bool scanOnStart = false;
    public bool includeSourceSubfolders = true;
    [Min(1)] public int maxQueueSize = 10;
    [Min(0f)] public float minimumFileAgeSeconds = 2f;
    public bool keepProcessedFilesForTesting = false;
    [Tooltip("Tự động thả cá ngay khi xử lý xong, không cần bấm P.")]
    public bool autoReleaseFish = true;
    [Tooltip("Thư mục lưu lại ảnh đã quét thay vì xoá, để dễ kiểm tra/test lại.")]
    public string scannedFolderPath = "Scanned_Folder";
    [Tooltip("Thư mục chuyển ảnh scan LỖI (không đọc được QR, QR bị che, chưa có template...) tới, " +
             "thay vì để chúng kẹt lại trong Executive_folder mãi mãi. Ảnh lỗi bị kẹt lại sẽ tiếp tục " +
             "bị tính vào executionImageCount, làm cạn dần availableSlots (xem maxQueueSize) và cuối " +
             "cùng làm hệ thống ngừng nhận ảnh mới dù hàng chờ cá còn trống.")]
    public string failedFolderPath = "Failed_Folder";
    [Tooltip("Tự động quét/nhập ảnh mới mỗi X giây, không cần bấm phím I.")]
    [Min(0.5f)] public float autoImportIntervalSeconds = 1f;

    [Header("Kích thước cá")]
    [Tooltip("Áp dụng cho cả cá mặc định lẫn cá do scan tạo ra (không ảnh hưởng " +
        "cá voi trang trí WhalePatrol3D - loại đó không đi qua pipeline này).")]
    [Min(0.1f)] public float fishSizeMultiplier = 2f;

    [Header("Default fish")]
    [Tooltip("Tuong doi so voi StreamingAssets (Application.streamingAssetsPath) - " +
        "KHONG phai duong dan tuong doi Assets/ nhu truoc, vi Assets/default_fish " +
        "khong ton tai trong ban build (chi la thu muc nguon trong Editor). " +
        "StreamingAssets duoc Unity dong goi kem theo build, doc duoc bang File I/O " +
        "y het trong Editor lan build.")]
    public string defaultFishFolderPath = "default_fish";
    public bool spawnDefaultFishOnStart = true;
    [Tooltip("Tỉ lệ số cá mặc định được spawn (1 = tất cả, 0.5 = giảm nửa). " +
        "Đàn cá con (ca_con) luôn được giữ nguyên, không bị cắt giảm theo tỉ lệ này.")]
    [Range(0.1f, 1f)] public float defaultFishSpawnRatio = 1f;
    [Tooltip("0 = tồn tại suốt scene.")]
    [Min(0f)] public float defaultFishLifetimeSeconds = 0f;
    [Tooltip("Viewport area used to distribute default fish without stacking at startup.")]
    public Rect defaultFishViewportArea = new Rect(0.12f, 0.2f, 0.76f, 0.62f);
    [Tooltip("Z gần/xa của đàn cá mặc định. Cả hai giá trị phải nằm trước Background.")]
    public Vector2 defaultFishDepthRange = new Vector2(-7.2f, -2.3f);
    [Tooltip("Scale ở lớp gần và lớp xa.")]
    public Vector2 defaultFishDepthScaleRange = new Vector2(0.82f, 0.48f);
    [Tooltip("Hệ số tốc độ ở lớp gần và lớp xa.")]
    public Vector2 defaultFishDepthSpeedRange = new Vector2(1f, 0.72f);
    [Tooltip("Chiều cao mỗi làn bơi, tính theo tỉ lệ chiều cao Swim Bounds.")]
    [Range(0.05f, 0.35f)] public float defaultFishLaneHeight = 0.12f;

    [Header("Fish emission")]
    [Tooltip("Emission cua ca mac dinh. 0 = tat.")]
    [Range(0f, 2f)] public float defaultFishEmissionIntensity = 0.3f;
    [ColorUsage(false, true)] public Color defaultFishEmissionTint = Color.white;
    [Tooltip("Emission cua ca nguoi choi duoc tao tu anh scan. 0 = tat.")]
    [Range(0f, 2f)] public float playerFishEmissionIntensity = 0.45f;
    [ColorUsage(false, true)] public Color playerFishEmissionTint = Color.white;

    [Header("Default whale UV seam fix")]
    [Tooltip("Chi ap dung cho ca_voi trong default_fish; khong anh huong ca_heo.")]
    public TextureWrapMode defaultWhaleTextureWrapMode = TextureWrapMode.Mirror;
    [Range(0f, 0.03f)] public float defaultWhaleUvInset = 0.004f;

    [Header("Default small fish UV alignment")]
    [Tooltip("Chi ap dung cho ca_con trong default_fish.")]
    public Vector2 defaultSmallFishUvScale = new Vector2(1.267f, 1.08f);
    public Vector2 defaultSmallFishUvOffset = new Vector2(-0.085f, -0.059f);

    [Header("Default starfish")]
    [Tooltip("He so thu nho sao bien trong luong default_fish.")]
    [Range(0.2f, 1f)] public float defaultStarfishScaleMultiplier = 0.5f;

    [Header("Fish templates")]
    public List<FishTemplate> fishTemplates = new List<FishTemplate>();
    [Min(256)] public int outputWidth = 2048;

    [Header("Cắt theo đường kẻ ngang")]
    [Tooltip("Ảnh scan có 2 đường kẻ đen ngang trang (dưới header QR/tiêu đề, và trên footer " +
             "logo tài trợ) - tranh của khách nằm giữa 2 đường đó. Tự dò 2 đường này theo pixel " +
             "thay vì dùng toạ độ scanCrop cố định, miễn ảnh chụp luôn cùng size/canh lề.")]
    [Range(40, 200)] public int dividerLineDarkThreshold = 120;
    [Tooltip("Tỉ lệ tối thiểu số pixel tối liên tục theo chiều ngang (bỏ qua viền 2 bên) để " +
             "coi 1 hàng là đường kẻ ngang.")]
    [Range(0.5f, 1f)] public float dividerLineMinCoverage = 0.85f;

    [Header("Xoá nền trắng")]
    [Tooltip("Dưới ngưỡng này (độ sáng 0-255): luôn giữ lại (nét đen/vùng tối). " +
             "Đo thực tế trên ảnh mẫu: nền giấy KHÔNG sáng gần 255 mà dao động quanh 190-220 " +
             "tuỳ ánh sáng lúc chụp, nên ngưỡng phải thấp hơn trực giác.")]
    [Range(0, 255)] public int backgroundLuminanceStart = 150;
    [Tooltip("Trên ngưỡng này (độ sáng) VÀ bão hoà màu thấp: coi là nền, xoá trong suốt.")]
    [Range(0, 255)] public int backgroundLuminanceFull = 175;
    [Tooltip("Dưới ngưỡng bão hoà màu này: có thể là nền (xám/trắng).")]
    [Range(0, 100)] public int backgroundSaturationKeep = 20;
    [Tooltip("Trên ngưỡng bão hoà màu này: chắc chắn là màu tô (kể cả màu sáng như vàng), luôn giữ lại.")]
    [Range(0, 100)] public int backgroundSaturationFull = 34;

    [Header("Performance")]
    [Tooltip("Ngân sách xử lý pixel tối đa mỗi frame khi nhấn I.")]
    [Range(1f, 12f)] public float processingFrameBudgetMs = 4f;

    [Header("Spawn")]
    public BoxCollider2D swimBounds;
    public Transform spawnParent;
    [Min(1f)] public float lifetimeSeconds = 300f;
    [Min(0.2f)] public float dropDurationSeconds = 1.5f;
    [Min(0f)] public float dropHeight = 2f;

    [Header("Water drop effect")]
    [Range(0.1f, 0.55f)] public float dropAirPhaseRatio = 0.28f;
    [Range(0.1f, 0.4f)] public float dropSettlePhaseRatio = 0.2f;
    [Range(0f, 0.8f)] public float dropSinkOvershoot = 0.22f;
    [Range(0f, 25f)] public float dropImpactTilt = 9f;

    private readonly Queue<GameObject> pendingFish = new Queue<GameObject>();
    // Ca do nguoi choi quet QR tao ra (dang cho hoac da tha boi), KHONG bao gom ca mac
    // dinh/background - dung cho phim R (RemovePlayerFish) de don rieng loai nay.
    private readonly List<GameObject> playerFish = new List<GameObject>();
    private readonly HashSet<string> failedExecutionFiles =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> importedSourceSignatures =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private string importLedgerPath;
    private Coroutine activeImportRoutine;

    private sealed class ImportedSourceFile
    {
        public string sourcePath;
        public string signature;
    }

    private sealed class SourceCopyBatch
    {
        public readonly List<ImportedSourceFile> imported = new List<ImportedSourceFile>();
        public readonly List<string> warnings = new List<string>();
    }

    void Start()
    {
        // ponytail: mac dinh 200 sequence khong du cho ~80+ ca (moi con tu
        // lap ScheduleNextTwist), tang truoc khi spawn de tranh warning.
        DOTween.SetTweensCapacity(500, 50);

        // Executive_folder/Scanned_Folder phai luon ton tai canh file .exe (ResolveFolderPath
        // tro ve thu muc CHUA .exe trong ban build) ngay tu dau, KHONG phu thuoc viec
        // sourceFolderPath (D:\Images, chi co tren may dev) co ton tai hay khong - truoc day
        // FillExecutionFolderIncrementally() la noi DUY NHAT tao Executive_folder, nhung no
        // thoat som neu thieu D:\Images nen thu muc khong bao gio duoc tao tren may that,
        // khien ImportImagesIncrementally() bao loi "Khong tim thay thu muc" moi giay (
        // InvokeRepeating ben duoi) vinh vien.
        Directory.CreateDirectory(ResolveFolderPath(folderPath));
        Directory.CreateDirectory(ResolveFolderPath(scannedFolderPath));

        LoadImportLedger();

        if (spawnDefaultFishOnStart)
            SpawnDefaultFish();

        if (scanOnStart)
            ScanImagesInFolder();

        InvokeRepeating(nameof(ImportImagesNow), autoImportIntervalSeconds, autoImportIntervalSeconds);
    }

    [ContextMenu("Import Images From Source Now")]
    public void ImportImagesNow()
    {
        if (activeImportRoutine != null)
        {
            Debug.Log("[QR Import] Đang xử lý lần import trước; không tạo tác vụ trùng.");
            return;
        }

        activeImportRoutine = StartCoroutine(ImportImagesIncrementally());
    }

    IEnumerator ImportImagesIncrementally()
    {
        yield return FillExecutionFolderIncrementally();
        yield return null;

        string resolvedFolder = ResolveFolderPath(folderPath);
        if (!Directory.Exists(resolvedFolder))
        {
            Debug.LogError("[QR] Không tìm thấy thư mục: " + resolvedFolder);
            activeImportRoutine = null;
            yield break;
        }

        string[] files = Directory.GetFiles(resolvedFolder);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < files.Length; i++)
        {
            if (pendingFish.Count >= Mathf.Max(1, maxQueueSize))
                break;

            string file = files[i];
            if (!IsSupportedImage(file) || failedExecutionFiles.Contains(file))
                continue;

            yield return ProcessQueuedImageIncrementally(file);
            yield return null;
        }

        Debug.Log($"<color=yellow>[QR Import]</color> Đã xử lý theo yêu cầu. " +
                  $"Hàng chờ hiện có {pendingFish.Count} cá.");
        activeImportRoutine = null;
    }

    IEnumerator ProcessQueuedImageIncrementally(string file)
    {
        Task<byte[]> readTask = Task.Run(() => File.ReadAllBytes(file));
        while (!readTask.IsCompleted)
            yield return null;

        if (readTask.IsFaulted || readTask.IsCanceled)
        {
            MarkIncrementalFailure(file, readTask.Exception);
            yield break;
        }

        Texture2D scanTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!scanTexture.LoadImage(readTask.Result, false))
        {
            MarkIncrementalFailure(file, "Unity không đọc được ảnh.");
            Destroy(scanTexture);
            yield break;
        }

        Color32[] scanPixels = scanTexture.GetPixels32();
        int scanWidth = scanTexture.width;
        int scanHeight = scanTexture.height;
        Task<string> decodeTask = Task.Run(
            () => DecodeQrPixels(scanPixels, scanWidth, scanHeight));

        while (!decodeTask.IsCompleted)
            yield return null;

        if (decodeTask.IsFaulted || decodeTask.IsCanceled)
        {
            MarkIncrementalFailure(file, decodeTask.Exception);
            Destroy(scanTexture);
            yield break;
        }

        string qrId = decodeTask.Result;
        FishTemplate template = string.IsNullOrWhiteSpace(qrId)
            ? null
            : FindTemplate(qrId.Trim());

        if (string.IsNullOrWhiteSpace(qrId) || template == null || template.prefab == null)
        {
            MarkIncrementalFailure(file, "Không đọc được QR hoặc chưa cấu hình template.");
            Destroy(scanTexture);
            yield break;
        }

        Debug.Log($"[QR] Payload '{qrId.Trim()}' -> prefab '{template.prefab.name}'");

        Texture2D outputTexture = null;
        yield return BuildFishTextureIncrementally(
            scanTexture,
            template,
            generated => outputTexture = generated);

        if (outputTexture == null)
        {
            MarkIncrementalFailure(file, "Không tạo được texture cá.");
            Destroy(scanTexture);
            yield break;
        }

        outputTexture.name = "PlayerFish_" + qrId + "_" + Path.GetFileNameWithoutExtension(file);
        GameObject fish = CreateFish(template, outputTexture, true);
        if (fish == null)
        {
            MarkIncrementalFailure(file, "Không thể gán texture vào prefab " + template.prefab.name);
            Destroy(outputTexture);
            Destroy(scanTexture);
            yield break;
        }

        outputTexture = null;
        pendingFish.Enqueue(fish);
        Debug.Log($"<color=green>[QR]</color> Đã xếp '{fish.name}' vào hàng chờ " +
                  $"({pendingFish.Count}/{Mathf.Max(1, maxQueueSize)}) từ {Path.GetFileName(file)}");

        if (autoReleaseFish)
            ReleaseNextFish();

        ArchiveScannedFile(file);

        Destroy(scanTexture);
    }

    void MarkIncrementalFailure(string file, object error)
    {
        failedExecutionFiles.Add(file);
        Debug.LogError($"[QR] Xử lý thất bại '{Path.GetFileName(file)}': {error}");
        ArchiveFailedFile(file);
    }

    [ContextMenu("Scan Folder Now")]
    public void ScanImagesInFolder()
    {
        string resolvedFolder = ResolveFolderPath(folderPath);
        if (!Directory.Exists(resolvedFolder))
        {
            Debug.LogError("[QR] Không tìm thấy thư mục: " + resolvedFolder);
            return;
        }

        string[] files = Directory.GetFiles(resolvedFolder);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        foreach (string file in files)
        {
            if (pendingFish.Count >= Mathf.Max(1, maxQueueSize))
                break;

            string extension = Path.GetExtension(file).ToLowerInvariant();
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                continue;

            if (!failedExecutionFiles.Contains(file))
                ProcessImage(file, true, true, false);
        }
    }

    [ContextMenu("Spawn Default Fish Now")]
    public void SpawnDefaultFish()
    {
        string resolvedFolder = ResolveStreamingAssetsFolder(defaultFishFolderPath);
        if (!Directory.Exists(resolvedFolder))
        {
            Debug.LogWarning("[Default Fish] Không tìm thấy thư mục: " + resolvedFolder);
            return;
        }

        string[] files = Array.FindAll(Directory.GetFiles(resolvedFolder), IsSupportedImage);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        // Giu nguyen bo dau moi X file de giam so luong deu, tru "dan ca con"
        // (ca_con) luon duoc spawn du - dan cai chi co 1 anh, khong phai loai
        // dang lam ho qua tai can cat bot.
        int keepEveryNth = Mathf.Max(1, Mathf.RoundToInt(1f / Mathf.Max(0.01f, defaultFishSpawnRatio)));

        int spawnedCount = 0;
        for (int index = 0; index < files.Length; index++)
        {
            bool isSmallFishFile = Path.GetFileNameWithoutExtension(files[index])
                .ToLowerInvariant().StartsWith("ca_con");
            if (!isSmallFishFile && index % keepEveryNth != 0)
                continue;

            Vector2 viewportPosition = GetDefaultFishViewportPosition(index, files.Length);
            // Golden-ratio distribution keeps neighbouring files/species out of the same depth layer.
            float depth01 = Mathf.Repeat(index * 0.61803398875f, 1f);
            float spawnZ = Mathf.Lerp(defaultFishDepthRange.x, defaultFishDepthRange.y, depth01);
            if (ProcessImage(files[index], false, false, true, viewportPosition, spawnZ, depth01))
                spawnedCount++;
        }

        Debug.Log($"<color=cyan>[Default Fish]</color> Đã spawn {spawnedCount} cá mặc định.");
    }

    bool ProcessImage(
        string file,
        bool enqueue,
        bool deleteAfterProcessing,
        bool allowFileNameFallback,
        Vector2? fixedViewportPosition = null,
        float? fixedDepth = null,
        float depth01 = 0f)
    {
        Texture2D scanTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Texture2D outputTexture = null;

        try
        {
            if (!scanTexture.LoadImage(File.ReadAllBytes(file), false))
                throw new InvalidOperationException("Unity không đọc được ảnh.");

            // Files in default_fish are final UV textures, not full A4 scans.
            bool useReadyTexture = allowFileNameFallback && !enqueue;
            string qrId = null;
            FishTemplate template = null;

            if (useReadyTexture)
            {
                template = FindTemplateFromFileName(Path.GetFileNameWithoutExtension(file));
                if (template != null)
                    qrId = template.qrId;
            }
            else
            {
                try
            {
                qrId = DecodeQr(scanTexture);
            }
            catch (Exception) when (allowFileNameFallback)
            {
                // Ảnh cá mặc định có thể đã được crop bỏ QR; khi đó dùng mã trong tên file.
            }
            }

            if (!useReadyTexture)
            {
                template = string.IsNullOrWhiteSpace(qrId)
                    ? null
                    : FindTemplate(qrId.Trim());
            }

            if (template == null && allowFileNameFallback)
            {
                template = FindTemplateFromFileName(Path.GetFileNameWithoutExtension(file));
                if (template != null)
                    qrId = template.qrId;
            }

            if (string.IsNullOrWhiteSpace(qrId))
                throw new InvalidOperationException(
                    "Không đọc được QR và tên file không chứa mã loại cá.");

            if (template == null || template.prefab == null)
                throw new InvalidOperationException("Chưa cấu hình template cho QR: " + qrId);

            Debug.Log($"[QR] Payload '{qrId.Trim()}' -> prefab '{template.prefab.name}'");

            if (useReadyTexture)
            {
                scanTexture.wrapMode = IsStandardWhale(template)
                    ? defaultWhaleTextureWrapMode
                    : TextureWrapMode.Repeat;
                scanTexture.filterMode = FilterMode.Bilinear;
                outputTexture = scanTexture;
                scanTexture = null;
            }
            else
            {
                outputTexture = BuildFishTexture(scanTexture, template);
                if (outputTexture == null)
                    throw new InvalidOperationException(
                        "Không tìm thấy đường kẻ ngang hoặc nét vẽ trong ảnh scan.");
            }

            outputTexture.name = "PlayerFish_" + qrId + "_" + Path.GetFileNameWithoutExtension(file);

            GameObject fish = CreateFish(
                template, outputTexture, enqueue, fixedViewportPosition, fixedDepth, depth01);
            if (fish == null)
                throw new InvalidOperationException("Không thể gán texture vào prefab " + template.prefab.name);

            outputTexture = null; // RuntimeFishTextureOwner chịu trách nhiệm giải phóng.

            if (enqueue)
            {
                pendingFish.Enqueue(fish);
                Debug.Log($"<color=green>[QR]</color> Đã xếp '{fish.name}' vào hàng chờ " +
                          $"({pendingFish.Count}/{Mathf.Max(1, maxQueueSize)}) từ {Path.GetFileName(file)}");

                if (autoReleaseFish)
                    ReleaseNextFish();
            }
            else
            {
                fish.SetActive(true);
                Debug.Log($"<color=cyan>[Default Fish]</color> Đã spawn '{fish.name}' " +
                          $"từ {Path.GetFileName(file)}");
            }

            if (deleteAfterProcessing)
                ArchiveScannedFile(file);

            return true;
        }
        catch (Exception exception)
        {
            failedExecutionFiles.Add(file);
            Debug.LogError($"[QR] Xử lý thất bại '{Path.GetFileName(file)}': {exception.Message}");
            // Chi don file neu day la anh scan dung mot lan (Executive_folder) - KHONG dung cho
            // default_fish (deleteAfterProcessing=false o do), vi do la asset noi dung co dinh
            // trong StreamingAssets, khong phai anh scan can don di.
            if (deleteAfterProcessing)
                ArchiveFailedFile(file);
            return false;
        }
        finally
        {
            if (scanTexture != null)
                Destroy(scanTexture);
            if (outputTexture != null)
                Destroy(outputTexture);
        }
    }

    Vector2 GetDefaultFishViewportPosition(int index, int count)
    {
        int safeCount = Mathf.Max(1, count);
        float aspect = Camera.main != null ? Mathf.Max(1f, Camera.main.aspect) : 16f / 9f;
        // Do not let a wide display turn the school into one or two crowded horizontal rows.
        int columns = Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Sqrt(safeCount * aspect)),
            1,
            3);
        int rows = Mathf.Max(1, Mathf.CeilToInt(safeCount / (float)columns));
        int column = index % columns;
        int row = index / columns;

        float u = (column + 0.5f) / columns;
        float v = (row + 0.5f) / rows;
        // Slightly stagger fish sharing a row so their long silhouettes do not trace one line.
        if (columns > 1)
            v += (column / (columns - 1f) - 0.5f) * 0.08f;
        v = Mathf.Clamp01(v);
        return new Vector2(
            defaultFishViewportArea.xMin + u * defaultFishViewportArea.width,
            defaultFishViewportArea.yMin + v * defaultFishViewportArea.height);
    }

    string DecodeQr(Texture2D texture)
    {
        return DecodeQrPixels(texture.GetPixels32(), texture.width, texture.height);
    }

    static string DecodeQrPixels(Color32[] pixels, int width, int height)
    {
        byte[] rgb = new byte[width * height * 3];
        int destination = 0;

        // ZXing nhận ảnh theo thứ tự trên xuống; Unity lưu pixel từ dưới lên.
        for (int y = height - 1; y >= 0; y--)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                Color32 pixel = pixels[row + x];
                rgb[destination++] = pixel.r;
                rgb[destination++] = pixel.g;
                rgb[destination++] = pixel.b;
            }
        }

        var reader = new MultiFormatReader
        {
            Hints = new Dictionary<DecodeHintType, object>
            {
                { DecodeHintType.TRY_HARDER, true },
                { DecodeHintType.POSSIBLE_FORMATS, new List<BarcodeFormat> { BarcodeFormat.QR_CODE } }
            }
        };

        var source = new RGBLuminanceSource(rgb, width, height,
            RGBLuminanceSource.BitmapFormat.RGB24);
        Result result = reader.decode(new BinaryBitmap(new HybridBinarizer(source)));
        return result != null ? result.Text : null;
    }

    // ── Dò 2 đường kẻ ngang + xoá nền trắng (thay cho khung/alpha-template) ────

    /// <summary>
    /// Dò 2 đường kẻ đen ngang trang (dưới header, trên footer) bằng cách tìm hàng pixel có
    /// tỉ lệ tối liên tục theo chiều ngang vượt <see cref="dividerLineMinCoverage"/>. Toạ độ
    /// trả về là "top-down" (0 = hàng trên cùng khi NGƯỜI xem ảnh), khác quy ước texture Unity
    /// (Y=0 ở dưới) - quy đổi khi đọc pixel thực tế.
    /// </summary>
    bool TryFindHorizontalDividerLines(
        Color32[] pixels, int width, int height, out int topLineYTop, out int bottomLineYTop)
    {
        int marginX = Mathf.RoundToInt(width * 0.03f);
        int sampleWidth = Mathf.Max(1, width - marginX * 2);
        int minDarkCount = Mathf.RoundToInt(sampleWidth * dividerLineMinCoverage);
        int threshold = dividerLineDarkThreshold;

        bool RowIsDivider(int yTop)
        {
            int yBottom = height - 1 - yTop;
            int row = yBottom * width;
            int darkCount = 0;
            for (int x = marginX; x < width - marginX; x++)
            {
                Color32 c = pixels[row + x];
                int luminance = (299 * c.r + 587 * c.g + 114 * c.b) / 1000;
                if (luminance < threshold) darkCount++;
            }
            return darkCount >= minDarkCount;
        }

        topLineYTop = -1;
        for (int yTop = 0; yTop < height; yTop++)
            if (RowIsDivider(yTop)) { topLineYTop = yTop; break; }

        bottomLineYTop = -1;
        for (int yTop = height - 1; yTop >= 0; yTop--)
            if (RowIsDivider(yTop)) { bottomLineYTop = yTop; break; }

        return topLineYTop >= 0 && bottomLineYTop >= 0 && bottomLineYTop > topLineYTop;
    }

    /// <summary>
    /// Alpha cho 1 pixel scan: 255 nếu chắc chắn là nét vẽ/màu tô, 0 nếu chắc chắn là nền
    /// trắng/xám của giấy, nội suy mềm ở vùng biên để tránh viền răng cưa cứng. Hiệu chỉnh
    /// bằng số liệu đo thực tế trên ảnh mẫu: nền giấy dao động quanh luminance ~190-220
    /// (KHÔNG sáng gần 255 do ánh sáng lúc chụp) với bão hoà màu thấp (~10-20); nét/màu tô
    /// hoặc tối (luminance thấp, kể cả nét đen) hoặc bão hoà màu cao (kể cả khi sáng, ví dụ
    /// màu vàng) - phải thoả CẢ HAI điều kiện "đủ sáng" và "đủ nhạt màu" mới bị coi là nền.
    /// </summary>
    byte ComputeArtworkAlpha(Color32 c)
    {
        int luminance = (299 * c.r + 587 * c.g + 114 * c.b) / 1000;
        int max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        int min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        int saturation = max - min;

        float lumT = Mathf.InverseLerp(backgroundLuminanceStart, backgroundLuminanceFull, luminance);
        float satT = Mathf.InverseLerp(backgroundSaturationFull, backgroundSaturationKeep, saturation);
        float backgroundScore = Mathf.Min(lumT, satT);
        return (byte)Mathf.Clamp(Mathf.RoundToInt((1f - backgroundScore) * 255f), 0, 255);
    }

    Texture2D BuildFishTexture(Texture2D scan, FishTemplate template)
    {
        Color32[] scanPixels = scan.GetPixels32();
        int scanWidth = scan.width;
        int scanHeight = scan.height;

        if (!TryFindHorizontalDividerLines(scanPixels, scanWidth, scanHeight,
                out int topLineYTop, out int bottomLineYTop))
        {
            Debug.LogWarning($"[FishTexture] Không tìm thấy 2 đường kẻ ngang cho " +
                              $"'{template.qrId}'; dùng toàn bộ ảnh.");
            topLineYTop = 0;
            bottomLineYTop = scanHeight - 1;
        }

        // Bỏ qua chính 2 đường kẻ + 1 khoảng đệm nhỏ, và biên trái/phải để tránh dính mép trang.
        int inset = Mathf.RoundToInt(scanHeight * 0.006f) + 4;
        int cropTopY = Mathf.Clamp(topLineYTop + inset, 0, scanHeight - 1);
        int cropBottomY = Mathf.Clamp(bottomLineYTop - inset, 0, scanHeight - 1);
        if (cropBottomY <= cropTopY) { cropTopY = 0; cropBottomY = scanHeight - 1; }

        int marginX = Mathf.RoundToInt(scanWidth * 0.02f);
        int cropLeftX = marginX;
        int cropWidth = Mathf.Max(1, scanWidth - marginX * 2);
        int cropHeight = cropBottomY - cropTopY + 1;

        // Buoc 1: tinh alpha ca vung crop, tim luon bounding box noi dung KHONG trong suot -
        // "lay tam lam ca": trim sat vao net ve thay vi giu nguyen ca vung giua 2 duong ke.
        byte[] alpha = new byte[cropWidth * cropHeight];
        int minX = cropWidth, minY = cropHeight, maxX = -1, maxY = -1;

        for (int cy = 0; cy < cropHeight; cy++)
        {
            int scanYTop = cropTopY + cy;
            int scanYBottom = scanHeight - 1 - scanYTop;
            int scanRow = scanYBottom * scanWidth;

            for (int cx = 0; cx < cropWidth; cx++)
            {
                Color32 color = scanPixels[scanRow + cropLeftX + cx];
                byte a = ComputeArtworkAlpha(color);
                alpha[cy * cropWidth + cx] = a;
                if (a > 16)
                {
                    if (cx < minX) minX = cx;
                    if (cx > maxX) maxX = cx;
                    if (cy < minY) minY = cy;
                    if (cy > maxY) maxY = cy;
                }
            }
        }

        if (maxX < minX || maxY < minY)
        {
            Debug.LogWarning($"[FishTexture] Không tìm thấy nét vẽ nào cho '{template.qrId}'.");
            return null;
        }

        int pad = Mathf.Max(2, Mathf.RoundToInt(Mathf.Min(cropWidth, cropHeight) * 0.01f));
        minX = Mathf.Max(0, minX - pad);
        minY = Mathf.Max(0, minY - pad);
        maxX = Mathf.Min(cropWidth - 1, maxX + pad);
        maxY = Mathf.Min(cropHeight - 1, maxY + pad);
        int trimmedWidth = maxX - minX + 1;
        int trimmedHeight = maxY - minY + 1;

        // Buoc 2: thu gon ve toi da outputWidth theo dung ty le that cua net ve (khong ep theo
        // template nao ca - "dung ca buc tranh" dung nghia).
        float scale = Mathf.Min(1f, Mathf.Max(64, outputWidth) / (float)Mathf.Max(trimmedWidth, trimmedHeight));
        int finalWidth = Mathf.Max(1, Mathf.RoundToInt(trimmedWidth * scale));
        int finalHeight = Mathf.Max(1, Mathf.RoundToInt(trimmedHeight * scale));

        Texture2D output = new Texture2D(finalWidth, finalHeight, TextureFormat.RGBA32, false);
        try
        {
            // Các material rig hiện tại lật UV bằng texture scale X = -1.
            // Repeat giữ phép lật này hoạt động; Clamp sẽ kẹp toàn bộ UV âm vào
            // cột alpha 0 ở mép texture và khiến cá hoàn toàn vô hình.
            output.wrapMode = TextureWrapMode.Repeat;
            output.filterMode = FilterMode.Bilinear;

            Color32[] result = new Color32[finalWidth * finalHeight];
            int coloredPixelCount = 0;
            int opaquePixelCount = 0;

            for (int fy = 0; fy < finalHeight; fy++)
            {
                float v = fy / (float)Mathf.Max(1, finalHeight - 1);
                // fy=0 (hang dau ghi vao result) se la hang DUOI CUNG cua texture Unity, nen
                // lay tu DAY vung trim khi v=0 - dung quy uoc y-flip nhu pipeline cu.
                int cy = Mathf.Clamp(Mathf.RoundToInt(minY + (1f - v) * (maxY - minY)), 0, cropHeight - 1);
                int scanYTop = cropTopY + cy;
                int scanYBottom = scanHeight - 1 - scanYTop;
                int scanRow = scanYBottom * scanWidth;

                for (int fx = 0; fx < finalWidth; fx++)
                {
                    float u = fx / (float)Mathf.Max(1, finalWidth - 1);
                    int cx = Mathf.Clamp(Mathf.RoundToInt(minX + u * (maxX - minX)), 0, cropWidth - 1);

                    Color32 color = scanPixels[scanRow + cropLeftX + cx];
                    color.a = alpha[cy * cropWidth + cx];
                    result[fy * finalWidth + fx] = color;

                    if (color.a > 127)
                    {
                        opaquePixelCount++;
                        int maximum = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
                        int minimum = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
                        if (maximum - minimum > 20 && maximum < 250)
                            coloredPixelCount++;
                    }
                }
            }

            output.SetPixels32(result);
            output.Apply(false, false);

            float coloredPercent = opaquePixelCount > 0
                ? coloredPixelCount * 100f / opaquePixelCount
                : 0f;
            Debug.Log($"[FishTexture] '{template.qrId}': đường kẻ tại y={topLineYTop}/{bottomLineYTop} " +
                      $"(cao {scanHeight}) -> vùng vẽ {trimmedWidth}x{trimmedHeight} -> " +
                      $"texture {output.width}x{output.height}; màu trong nét vẽ: {coloredPercent:F1}%");
            return output;
        }
        catch
        {
            Destroy(output);
            throw;
        }
    }

    IEnumerator BuildFishTextureIncrementally(
        Texture2D scan,
        FishTemplate template,
        Action<Texture2D> completed)
    {
        Color32[] scanPixels = scan.GetPixels32();
        int scanWidth = scan.width;
        int scanHeight = scan.height;

        if (!TryFindHorizontalDividerLines(scanPixels, scanWidth, scanHeight,
                out int topLineYTop, out int bottomLineYTop))
        {
            Debug.LogWarning($"[FishTexture] Không tìm thấy 2 đường kẻ ngang cho " +
                              $"'{template.qrId}'; dùng toàn bộ ảnh.");
            topLineYTop = 0;
            bottomLineYTop = scanHeight - 1;
        }

        int inset = Mathf.RoundToInt(scanHeight * 0.006f) + 4;
        int cropTopY = Mathf.Clamp(topLineYTop + inset, 0, scanHeight - 1);
        int cropBottomY = Mathf.Clamp(bottomLineYTop - inset, 0, scanHeight - 1);
        if (cropBottomY <= cropTopY) { cropTopY = 0; cropBottomY = scanHeight - 1; }

        int marginX = Mathf.RoundToInt(scanWidth * 0.02f);
        int cropLeftX = marginX;
        int cropWidth = Mathf.Max(1, scanWidth - marginX * 2);
        int cropHeight = cropBottomY - cropTopY + 1;

        byte[] alpha = new byte[cropWidth * cropHeight];
        int minX = cropWidth, minY = cropHeight, maxX = -1, maxY = -1;

        var frameTimer = System.Diagnostics.Stopwatch.StartNew();
        double budget = Math.Max(1d, processingFrameBudgetMs);

        for (int cy = 0; cy < cropHeight; cy++)
        {
            int scanYTop = cropTopY + cy;
            int scanYBottom = scanHeight - 1 - scanYTop;
            int scanRow = scanYBottom * scanWidth;

            for (int cx = 0; cx < cropWidth; cx++)
            {
                Color32 color = scanPixels[scanRow + cropLeftX + cx];
                byte a = ComputeArtworkAlpha(color);
                alpha[cy * cropWidth + cx] = a;
                if (a > 16)
                {
                    if (cx < minX) minX = cx;
                    if (cx > maxX) maxX = cx;
                    if (cy < minY) minY = cy;
                    if (cy > maxY) maxY = cy;
                }
            }

            if (frameTimer.Elapsed.TotalMilliseconds >= budget)
            {
                frameTimer.Restart();
                yield return null;
            }
        }

        if (maxX < minX || maxY < minY)
        {
            Debug.LogWarning($"[FishTexture] Không tìm thấy nét vẽ nào cho '{template.qrId}'.");
            completed(null);
            yield break;
        }

        int pad = Mathf.Max(2, Mathf.RoundToInt(Mathf.Min(cropWidth, cropHeight) * 0.01f));
        minX = Mathf.Max(0, minX - pad);
        minY = Mathf.Max(0, minY - pad);
        maxX = Mathf.Min(cropWidth - 1, maxX + pad);
        maxY = Mathf.Min(cropHeight - 1, maxY + pad);
        int trimmedWidth = maxX - minX + 1;
        int trimmedHeight = maxY - minY + 1;

        float scale = Mathf.Min(1f, Mathf.Max(64, outputWidth) / (float)Mathf.Max(trimmedWidth, trimmedHeight));
        int finalWidth = Mathf.Max(1, Mathf.RoundToInt(trimmedWidth * scale));
        int finalHeight = Mathf.Max(1, Mathf.RoundToInt(trimmedHeight * scale));

        Texture2D output = null;
        bool ownershipTransferred = false;

        try
        {
            output = new Texture2D(
                finalWidth, finalHeight, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            Color32[] resultPixels = new Color32[finalWidth * finalHeight];
            int coloredPixelCount = 0;
            int opaquePixelCount = 0;
            frameTimer.Restart();

            for (int fy = 0; fy < finalHeight; fy++)
            {
                float v = fy / (float)Mathf.Max(1, finalHeight - 1);
                int cy = Mathf.Clamp(Mathf.RoundToInt(minY + (1f - v) * (maxY - minY)), 0, cropHeight - 1);
                int scanYTop2 = cropTopY + cy;
                int scanYBottom2 = scanHeight - 1 - scanYTop2;
                int scanRow2 = scanYBottom2 * scanWidth;

                for (int fx = 0; fx < finalWidth; fx++)
                {
                    float u = fx / (float)Mathf.Max(1, finalWidth - 1);
                    int cx = Mathf.Clamp(Mathf.RoundToInt(minX + u * (maxX - minX)), 0, cropWidth - 1);

                    Color32 color = scanPixels[scanRow2 + cropLeftX + cx];
                    color.a = alpha[cy * cropWidth + cx];
                    resultPixels[fy * finalWidth + fx] = color;

                    if (color.a > 127)
                    {
                        opaquePixelCount++;
                        int maximum = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
                        int minimum = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
                        if (maximum - minimum > 20 && maximum < 250)
                            coloredPixelCount++;
                    }
                }

                if (frameTimer.Elapsed.TotalMilliseconds >= budget)
                {
                    frameTimer.Restart();
                    yield return null;
                }
            }

            output.SetPixels32(resultPixels);
            output.Apply(false, false);

            float coloredPercent = opaquePixelCount > 0
                ? coloredPixelCount * 100f / opaquePixelCount
                : 0f;
            Debug.Log($"[FishTexture] '{template.qrId}': đường kẻ tại y={topLineYTop}/{bottomLineYTop} " +
                      $"(cao {scanHeight}) -> vùng vẽ {trimmedWidth}x{trimmedHeight} -> " +
                      $"texture {output.width}x{output.height}; màu trong nét vẽ: {coloredPercent:F1}%");

            completed(output);
            ownershipTransferred = true;
        }
        finally
        {
            if (!ownershipTransferred && output != null)
                Destroy(output);
        }
    }

    GameObject CreateFish(
        FishTemplate template,
        Texture2D texture,
        bool prepareDrop,
        Vector2? fixedViewportPosition = null,
        float? fixedDepth = null,
        float depth01 = 0f)
    {
        Vector3 landingPosition = transform.position;
        Vector3 queuedPosition = landingPosition;
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            float spawnZ = fixedDepth ?? -2.5f;
            float distanceFromCamera = spawnZ - mainCamera.transform.position.z;
            // Cua bo o day: dat mieng dat theo % chieu cao camera (luon nam
            // trong khung hinh o bat ky do sau nao), thay vi tinh theo the
            // gioi tu 1 collider vung boi rong hon man hinh thuc te.
            bool landsLow = template.qrId == "cua";
            Vector2 viewportXY = fixedViewportPosition ?? new Vector2(
                UnityEngine.Random.Range(0.2f, 0.8f),
                landsLow
                    ? UnityEngine.Random.Range(0.06f, 0.22f)
                    : UnityEngine.Random.Range(0.25f, 0.75f));
            Vector3 viewportPosition = new Vector3(viewportXY.x, viewportXY.y, distanceFromCamera);
            landingPosition = mainCamera.ViewportToWorldPoint(viewportPosition);
            landingPosition.z = spawnZ;

            Vector3 topPosition = mainCamera.ViewportToWorldPoint(
                new Vector3(viewportPosition.x, 1f, distanceFromCamera));
            queuedPosition = new Vector3(landingPosition.x, topPosition.y + dropHeight, spawnZ);
        }
        else if (swimBounds != null)
        {
            Bounds bounds = swimBounds.bounds;
            landingPosition = new Vector3(
                UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                UnityEngine.Random.Range(bounds.min.y, bounds.max.y),
                -5f);
            queuedPosition = new Vector3(
                landingPosition.x, bounds.max.y + dropHeight, landingPosition.z);
        }

        Vector3 spawnPosition = prepareDrop ? queuedPosition : landingPosition;
        GameObject fish = Instantiate(template.prefab, spawnPosition, Quaternion.identity, spawnParent);
        fish.SetActive(false);
        fish.name = template.qrId + "_Player";

        bool isDefaultFish = fixedDepth.HasValue && fixedViewportPosition.HasValue;
        if (isDefaultFish)
        {
            float depthScale = Mathf.Lerp(
                defaultFishDepthScaleRange.x,
                defaultFishDepthScaleRange.y,
                Mathf.Clamp01(depth01));
            if (IsStarfish(template))
                depthScale *= defaultStarfishScaleMultiplier;
            fish.transform.localScale *= depthScale;
        }
        fish.transform.localScale *= fishSizeMultiplier;

        float emissionIntensity = isDefaultFish
            ? defaultFishEmissionIntensity
            : playerFishEmissionIntensity;
        Color emissionTint = isDefaultFish
            ? defaultFishEmissionTint
            : playerFishEmissionTint;
        float textureUvInset = isDefaultFish && IsStandardWhale(template)
            ? defaultWhaleUvInset
            : 0f;
        bool alignDefaultSmallFish = isDefaultFish && IsSmallFish(template);

        // Xoay sau cung, sau khi crop/mask da chay xong theo dung huong nam
        // ngang tren giay in. Vi du ca ngua: ve nam ngang de to hon nhung
        // dung doc khi boi trong game.
        if (template.textureRotationDegrees != 0)
        {
            Texture2D rotatedTexture = RotateTextureQuarterTurns(
                texture, template.textureRotationDegrees / 90);
            if (rotatedTexture != texture)
            {
                Destroy(texture);
                texture = rotatedTexture;
            }
        }

        if (!isDefaultFish)
        {
            // Prefab's baked X/Y scale assumed the old fixed-canvas template aspect; the mesh is
            // just a 1:1 quad (see Ca_Voi.prefab), so with the new crop (aspect now follows
            // whatever the visitor actually drew) that baked ratio would stretch/squash the fish.
            // Keep the same visual "size" (geometric-mean area) but redistribute X/Y to match the
            // generated texture's real aspect, preserving sign so mirroring/flip rigs still work.
            Vector3 currentScale = fish.transform.localScale;
            float referenceSize = Mathf.Sqrt(Mathf.Abs(currentScale.x) * Mathf.Abs(currentScale.y));
            float textureAspect = Mathf.Clamp(
                (float)texture.width / Mathf.Max(1, texture.height), 0.25f, 4f);
            float aspectRoot = Mathf.Sqrt(textureAspect);
            fish.transform.localScale = new Vector3(
                Mathf.Sign(currentScale.x == 0f ? 1f : currentScale.x) * referenceSize * aspectRoot,
                Mathf.Sign(currentScale.y == 0f ? 1f : currentScale.y) * referenceSize / aspectRoot,
                currentScale.z);
        }

        PlayerFishTextureApplicator applicator = PlayerFishTextureApplicator.Apply(
            fish,
            texture,
            emissionIntensity,
            emissionTint,
            textureUvInset,
            alignDefaultSmallFish ? defaultSmallFishUvScale : Vector2.one,
            alignDefaultSmallFish ? defaultSmallFishUvOffset : Vector2.zero);
        if (applicator == null)
        {
            Destroy(fish);
            return null;
        }

        DOTweenFishAnim animation = fish.GetComponent<DOTweenFishAnim>();
        if (animation == null)
            animation = fish.AddComponent<DOTweenFishAnim>();
        animation.ConfigureStandardFishMotion(template.qrId);

        FishRandomMotion randomMotion = fish.GetComponent<FishRandomMotion>();
        if (randomMotion == null)
            randomMotion = fish.AddComponent<FishRandomMotion>();
        DOTweenFishAnim prefabAnimation = template.prefab.GetComponent<DOTweenFishAnim>();
        randomMotion.Configure(
            swimBounds,
            GetMotionSpecies(template.qrId),
            1f,
            GetMotionVerticalRange(template.qrId),
            prefabAnimation == null ? template.spriteFacesRight : prefabAnimation.spriteFacesRight,
            !prepareDrop);

        if (prepareDrop)
        {
            float waterSurfaceY = swimBounds != null
                ? swimBounds.bounds.max.y
                : Mathf.Lerp(queuedPosition.y, landingPosition.y, 0.3f);
            animation.PrepareWaterDrop(
                landingPosition,
                waterSurfaceY,
                dropDurationSeconds,
                dropAirPhaseRatio,
                dropSettlePhaseRatio,
                dropSinkOvershoot,
                dropImpactTilt);
        }

        BackgroundFishSpawner interactionSettings =
            UnityEngine.Object.FindFirstObjectByType<BackgroundFishSpawner>();
        if (interactionSettings != null)
            interactionSettings.ConfigureClickInteraction(fish, template.qrId);

        if (!isDefaultFish)
            playerFish.Add(fish);

        Debug.Log($"[QR] Spawn: {spawnPosition}, landing: {landingPosition}, " +
                  $"texture: {texture.width}x{texture.height}");
        return fish;
    }

    /// <summary>
    /// Phim R: xoa toan bo ca do nguoi choi quet QR tao ra (dang cho trong hang doi lan da
    /// tha boi), giu nguyen ca mac dinh/background (BackgroundFishSpawner va SpawnDefaultFish).
    /// </summary>
    public void RemovePlayerFish()
    {
        int removed = 0;
        for (int i = playerFish.Count - 1; i >= 0; i--)
        {
            GameObject fish = playerFish[i];
            if (fish == null)
                continue;

            Destroy(fish);
            removed++;
        }

        playerFish.Clear();
        pendingFish.Clear();
        Debug.Log($"<color=orange>[QR]</color> Đã xoá {removed} cá người chơi " +
                  "(giữ nguyên cá mặc định/background).");
    }

    void Update()
    {
        if (WasResetKeyPressed())
            RemovePlayerFish();
    }

    static bool WasResetKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.R);
#else
        return false;
#endif
    }

    static FishMotionSpecies GetMotionSpecies(string fishId)
    {
        switch (fishId)
        {
            case "ca_ngua": return FishMotionSpecies.Seahorse;
            case "sua": return FishMotionSpecies.Jellyfish;
            case "tom": return FishMotionSpecies.Shrimp;
            case "cua": return FishMotionSpecies.Crab;
            case "sao_bien": return FishMotionSpecies.Starfish;
            default: return FishMotionSpecies.Horizontal;
        }
    }

    static float GetMotionVerticalRange(string fishId)
    {
        switch (fishId)
        {
            case "ca_ngua": return 1.4f;
            case "sua": return 1.2f;
            case "tom": return 0.45f;
            case "cua": return 0.2f;
            case "sao_bien": return 0.7f;
            case "ca_muc": return 2.2f;
            default: return 0.9f;
        }
    }

    public void ReleaseNextFish()
    {
        while (pendingFish.Count > 0)
        {
            GameObject fish = pendingFish.Dequeue();
            if (fish == null)
                continue;

            fish.SetActive(true);
            Debug.Log($"<color=cyan>[QR Queue]</color> Thả '{fish.name}'. " +
                      $"Còn {pendingFish.Count} cá trong hàng chờ.");
            return;
        }

        Debug.Log("[QR Queue] Hàng chờ đang trống.");
    }

    IEnumerator FillExecutionFolderIncrementally()
    {
        if (string.IsNullOrWhiteSpace(sourceFolderPath) || !Directory.Exists(sourceFolderPath))
            yield break;

        string sourceFolder = sourceFolderPath;
        string executionFolder = ResolveFolderPath(folderPath);
        int queuedCount = pendingFish.Count;
        int queueLimit = Mathf.Max(1, maxQueueSize);
        bool searchSubfolders = includeSourceSubfolders;
        float minimumAge = minimumFileAgeSeconds;
        var knownSignatures = new HashSet<string>(
            importedSourceSignatures, StringComparer.OrdinalIgnoreCase);

        Task<SourceCopyBatch> copyTask = Task.Run(() => CopySourceFiles(
            sourceFolder,
            executionFolder,
            queuedCount,
            queueLimit,
            searchSubfolders,
            minimumAge,
            knownSignatures));

        while (!copyTask.IsCompleted)
            yield return null;

        if (copyTask.IsFaulted || copyTask.IsCanceled)
        {
            Debug.LogWarning("[QR Import] Không thể duyệt thư mục nguồn: " + copyTask.Exception);
            yield break;
        }

        SourceCopyBatch batch = copyTask.Result;
        for (int i = 0; i < batch.imported.Count; i++)
        {
            ImportedSourceFile imported = batch.imported[i];
            importedSourceSignatures.Add(imported.signature);
            AppendImportLedger(imported.signature);
            Debug.Log($"[QR Import] Đã chuyển '{imported.sourcePath}' vào Executive_folder.");
        }

        for (int i = 0; i < batch.warnings.Count; i++)
            Debug.LogWarning(batch.warnings[i]);
    }

    static SourceCopyBatch CopySourceFiles(
        string sourceFolder,
        string executionFolder,
        int queuedCount,
        int queueLimit,
        bool includeSubfolders,
        float minimumAgeSeconds,
        HashSet<string> knownSignatures)
    {
        var batch = new SourceCopyBatch();
        Directory.CreateDirectory(executionFolder);

        int executionImageCount = 0;
        foreach (string file in Directory.GetFiles(executionFolder))
        {
            if (IsSupportedImage(file))
                executionImageCount++;
        }

        int availableSlots = queueLimit - queuedCount - executionImageCount;
        if (availableSlots <= 0)
            return batch;

        SearchOption searchOption = includeSubfolders
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        string[] sourceFiles = Directory.GetFiles(sourceFolder, "*", searchOption);
        Array.Sort(sourceFiles, (left, right) =>
            File.GetLastWriteTimeUtc(left).CompareTo(File.GetLastWriteTimeUtc(right)));

        for (int i = 0; i < sourceFiles.Length && batch.imported.Count < availableSlots; i++)
        {
            string sourceFile = sourceFiles[i];
            if (!IsSupportedImage(sourceFile))
                continue;

            FileInfo info;
            try
            {
                info = new FileInfo(sourceFile);
                if ((DateTime.UtcNow - info.LastWriteTimeUtc).TotalSeconds < minimumAgeSeconds)
                    continue;
            }
            catch (IOException)
            {
                continue;
            }

            string signature = BuildSourceSignature(info);
            if (knownSignatures.Contains(signature))
                continue;

            string destination = MakeUniqueDestination(executionFolder, info.Name);
            string temporaryDestination = destination + ".importing";
            try
            {
                File.Copy(sourceFile, temporaryDestination, false);
                File.Move(temporaryDestination, destination);
                knownSignatures.Add(signature);
                batch.imported.Add(new ImportedSourceFile
                {
                    sourcePath = sourceFile,
                    signature = signature
                });
            }
            catch (Exception exception)
            {
                try
                {
                    if (File.Exists(temporaryDestination))
                        File.Delete(temporaryDestination);
                }
                catch (Exception)
                {
                    // Giữ lỗi copy ban đầu; lần import sau có thể thử lại.
                }

                batch.warnings.Add(
                    $"[QR Import] Không thể chuyển '{sourceFile}': {exception.Message}");
            }
        }

        return batch;
    }

    void FillExecutionFolder()
    {
        if (string.IsNullOrWhiteSpace(sourceFolderPath) || !Directory.Exists(sourceFolderPath))
            return;

        string executionFolder = ResolveFolderPath(folderPath);
        Directory.CreateDirectory(executionFolder);

        int executionImageCount = 0;
        foreach (string file in Directory.GetFiles(executionFolder))
        {
            if (IsSupportedImage(file))
                executionImageCount++;
        }

        int availableSlots = Mathf.Max(1, maxQueueSize) - pendingFish.Count - executionImageCount;
        if (availableSlots <= 0)
            return;

        SearchOption searchOption = includeSourceSubfolders
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        string[] sourceFiles = Directory.GetFiles(sourceFolderPath, "*", searchOption);
        Array.Sort(sourceFiles, (left, right) =>
            File.GetLastWriteTimeUtc(left).CompareTo(File.GetLastWriteTimeUtc(right)));

        int copiedCount = 0;
        foreach (string sourceFile in sourceFiles)
        {
            if (copiedCount >= availableSlots || !IsSupportedImage(sourceFile))
                continue;

            FileInfo info;
            try
            {
                info = new FileInfo(sourceFile);
                if ((DateTime.UtcNow - info.LastWriteTimeUtc).TotalSeconds < minimumFileAgeSeconds)
                    continue;
            }
            catch (IOException)
            {
                continue;
            }

            string signature = BuildSourceSignature(info);
            if (importedSourceSignatures.Contains(signature))
                continue;

            string destination = MakeUniqueDestination(executionFolder, info.Name);
            string temporaryDestination = destination + ".importing";

            try
            {
                File.Copy(sourceFile, temporaryDestination, false);
                File.Move(temporaryDestination, destination);
                importedSourceSignatures.Add(signature);
                AppendImportLedger(signature);
                copiedCount++;
                Debug.Log($"[QR Import] Đã chuyển '{sourceFile}' vào Executive_folder.");
            }
            catch (Exception exception)
            {
                if (File.Exists(temporaryDestination))
                    File.Delete(temporaryDestination);
                Debug.LogWarning($"[QR Import] Không thể chuyển '{sourceFile}': {exception.Message}");
            }
        }
    }

    void LoadImportLedger()
    {
        importLedgerPath = Path.Combine(
            Application.persistentDataPath, "FishScanner", "imported-sources.txt");

        if (!File.Exists(importLedgerPath))
            return;

        foreach (string line in File.ReadAllLines(importLedgerPath))
        {
            if (!string.IsNullOrWhiteSpace(line))
                importedSourceSignatures.Add(line.Trim());
        }
    }

    void AppendImportLedger(string signature)
    {
        string directory = Path.GetDirectoryName(importLedgerPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.AppendAllLines(importLedgerPath, new[] { signature });
    }

    static string BuildSourceSignature(FileInfo info)
    {
        return info.FullName + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks;
    }

    static bool IsSupportedImage(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension == ".jpg" || extension == ".jpeg" || extension == ".png";
    }

    static string MakeUniqueDestination(string folder, string fileName)
    {
        string destination = Path.Combine(folder, fileName);
        if (!File.Exists(destination) && !File.Exists(destination + ".importing"))
            return destination;

        string name = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        int suffix = 1;
        do
        {
            destination = Path.Combine(folder, name + "_" + suffix + extension);
            suffix++;
        }
        while (File.Exists(destination) || File.Exists(destination + ".importing"));

        return destination;
    }

    void ArchiveScannedFile(string file)
    {
        if (keepProcessedFilesForTesting)
            return;

        try
        {
            string archiveFolder = ResolveFolderPath(scannedFolderPath);
            Directory.CreateDirectory(archiveFolder);
            string destination = MakeUniqueDestination(archiveFolder, Path.GetFileName(file));

            // File vua duoc doc xong co the con bi Unity khoa mot chut de tao
            // .meta (do nam trong Assets/Executive_folder); thu lai vai lan thay
            // vi bo cuoc ngay, thay vi delay ca hang doi file phia sau.
            const int maxAttempts = 6;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    File.Move(file, destination);
                    return;
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[QR] Không thể lưu '{Path.GetFileName(file)}' vào Scanned_Folder: {exception.Message}");
        }
    }

    /// <summary>
    /// Chuyen anh scan LOI ra khoi Executive_folder, giong het ArchiveScannedFile cho anh
    /// THANH CONG. Bat buoc phai lam - neu khong anh loi se kep lai mai mai trong
    /// Executive_folder, bi executionImageCount dem vao va lam can dan availableSlots
    /// (xem CopySourceFiles/FillExecutionFolder) cho toi khi he thong ngung nhan anh moi
    /// hoan toan, du hang cho ca (pendingFish) van con cho trong.
    /// </summary>
    void ArchiveFailedFile(string file)
    {
        try
        {
            string failedFolder = ResolveFolderPath(failedFolderPath);
            Directory.CreateDirectory(failedFolder);
            string destination = MakeUniqueDestination(failedFolder, Path.GetFileName(file));

            const int maxAttempts = 6;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    File.Move(file, destination);
                    return;
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[QR] Không thể lưu '{Path.GetFileName(file)}' vào Failed_Folder: {exception.Message}");
        }
    }

    FishTemplate FindTemplate(string qrId)
    {
        // Prefab name is the source of truth requested by the QR workflow.
        // Case-insensitive comparison supports the existing asset Ca_heo for payload ca_heo.
        FishTemplate byPrefabName = fishTemplates.Find(item => item != null &&
            item.prefab != null &&
            string.Equals(item.prefab.name, qrId, StringComparison.OrdinalIgnoreCase));

        if (byPrefabName != null)
            return byPrefabName;

        // Keep qrId as an alias for future assets whose display name differs.
        return fishTemplates.Find(item => item != null &&
            string.Equals(item.qrId, qrId, StringComparison.OrdinalIgnoreCase));
    }

    FishTemplate FindTemplateFromFileName(string fileName)
    {
        FishTemplate bestMatch = null;
        int bestMatchLength = -1;

        foreach (FishTemplate template in fishTemplates)
        {
            if (template == null)
                continue;

            string[] candidates =
            {
                template.qrId,
                template.prefab != null ? template.prefab.name : null
            };

            foreach (string candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate) ||
                    candidate.Length <= bestMatchLength ||
                    fileName.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                bestMatch = template;
                bestMatchLength = candidate.Length;
            }
        }

        return bestMatch;
    }

    static bool IsStandardWhale(FishTemplate template)
    {
        if (template == null)
            return false;

        string identity = !string.IsNullOrWhiteSpace(template.qrId)
            ? template.qrId
            : template.prefab != null ? template.prefab.name : string.Empty;
        string normalized = identity.Trim().ToLowerInvariant()
            .Replace(' ', '_')
            .Replace('-', '_');

        return (normalized == "ca_voi" || normalized == "ca_voi_1") &&
               !normalized.Contains("sat_thu");
    }

    static bool IsSmallFish(FishTemplate template)
    {
        if (template == null)
            return false;

        string identity = !string.IsNullOrWhiteSpace(template.qrId)
            ? template.qrId
            : template.prefab != null ? template.prefab.name : string.Empty;
        string normalized = identity.Trim().ToLowerInvariant()
            .Replace(' ', '_')
            .Replace('-', '_');
        return normalized == "ca_con";
    }

    static bool IsStarfish(FishTemplate template)
    {
        if (template == null)
            return false;

        string identity = !string.IsNullOrWhiteSpace(template.qrId)
            ? template.qrId
            : template.prefab != null ? template.prefab.name : string.Empty;
        string normalized = identity.Trim().ToLowerInvariant()
            .Replace(' ', '_')
            .Replace('-', '_');
        return normalized == "sao_bien";
    }

    static string ResolveFolderPath(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, path));
    }

    // Danh rieng cho default_fish: khac ResolveFolderPath (tinh theo thu muc
    // CHUA .exe, chi dung cho Executive_folder/Scanned_Folder - nhung thu muc
    // NGOAI project, ban than khong ton tai san san trong ban build). Anh mac
    // dinh la asset that su cua game nen phai nam trong StreamingAssets - thu
    // muc duy nhat Unity dam bao dong goi kem file rieng le vao ban build va
    // doc duoc bang File I/O binh thuong ca trong Editor lan build.
    static string ResolveStreamingAssetsFolder(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        return Path.Combine(Application.streamingAssetsPath, path);
    }

    // quarterTurns duong = xoay theo 1 chieu, am = xoay nguoc lai; chi ho tro
    // boi so cua 90 do (0/1/2/3 vong 90). Dung cho loai ca ve nam ngang tren
    // giay nhung can dung theo huong khac trong game (VD ca ngua).
    static Texture2D RotateTextureQuarterTurns(Texture2D source, int quarterTurns)
    {
        quarterTurns = ((quarterTurns % 4) + 4) % 4;
        if (quarterTurns == 0)
            return source;

        int sourceWidth = source.width;
        int sourceHeight = source.height;
        Color32[] sourcePixels = source.GetPixels32();

        int destWidth = quarterTurns == 2 ? sourceWidth : sourceHeight;
        int destHeight = quarterTurns == 2 ? sourceHeight : sourceWidth;
        Color32[] destPixels = new Color32[sourcePixels.Length];

        for (int y = 0; y < sourceHeight; y++)
        {
            for (int x = 0; x < sourceWidth; x++)
            {
                Color32 pixel = sourcePixels[y * sourceWidth + x];
                int destX;
                int destY;
                switch (quarterTurns)
                {
                    case 1:
                        destX = y;
                        destY = sourceWidth - 1 - x;
                        break;
                    case 2:
                        destX = sourceWidth - 1 - x;
                        destY = sourceHeight - 1 - y;
                        break;
                    default:
                        destX = sourceHeight - 1 - y;
                        destY = x;
                        break;
                }
                destPixels[destY * destWidth + destX] = pixel;
            }
        }

        Texture2D rotated = new Texture2D(destWidth, destHeight, TextureFormat.RGBA32, false)
        {
            wrapMode = source.wrapMode,
            filterMode = source.filterMode
        };
        rotated.SetPixels32(destPixels);
        rotated.Apply(false, false);
        return rotated;
    }

    // Bản thuần CPU dùng bởi worker thread của luồng import I.
    // Không gọi Unity API để tránh khóa main thread khi flood-fill hơn 2 triệu pixel.
    internal static byte[] CalculateFilledSilhouetteAlpha(
        Color32[] maskPixels,
        int width,
        int height)
    {
        int pixelCount = width * height;
        byte[] state = new byte[pixelCount];
        byte[] filledAlpha = new byte[pixelCount];

        for (int y = 0; y < height; y++)
        {
            int minY = Math.Max(0, y - 1);
            int maxY = Math.Min(height - 1, y + 1);
            for (int x = 0; x < width; x++)
            {
                int minX = Math.Max(0, x - 1);
                int maxX = Math.Min(width - 1, x + 1);
                bool boundary = false;

                for (int sampleY = minY; sampleY <= maxY && !boundary; sampleY++)
                {
                    int row = sampleY * width;
                    for (int sampleX = minX; sampleX <= maxX; sampleX++)
                    {
                        if (maskPixels[row + sampleX].a <= 8)
                            continue;

                        boundary = true;
                        break;
                    }
                }

                if (boundary)
                    state[y * width + x] = 2;
            }
        }

        int[] queue = new int[pixelCount];
        int head = 0;
        int tail = 0;

        void EnqueueOutside(int index)
        {
            if (state[index] != 0)
                return;

            state[index] = 1;
            queue[tail++] = index;
        }

        for (int x = 0; x < width; x++)
        {
            EnqueueOutside(x);
            EnqueueOutside((height - 1) * width + x);
        }

        for (int y = 0; y < height; y++)
        {
            EnqueueOutside(y * width);
            EnqueueOutside(y * width + width - 1);
        }

        while (head < tail)
        {
            int index = queue[head++];
            int x = index % width;
            int y = index / width;
            if (x > 0) EnqueueOutside(index - 1);
            if (x + 1 < width) EnqueueOutside(index + 1);
            if (y > 0) EnqueueOutside(index - width);
            if (y + 1 < height) EnqueueOutside(index + width);
        }

        for (int i = 0; i < pixelCount; i++)
        {
            if (state[i] != 1)
                filledAlpha[i] = 255;
        }

        return filledAlpha;
    }
}
