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
    private static readonly Dictionary<string, byte[]> FilledAlphaCache =
        new Dictionary<string, byte[]>();

    [Serializable]
    public class FishTemplate
    {
        public string qrId;
        public GameObject prefab;
        public Texture2D alphaTemplate;

        [Tooltip("Bật nếu model ở scale X dương quay đầu sang phải.")]
        public bool spriteFacesRight = true;

        [Tooltip("Vùng cá trong ảnh scan, tọa độ chuẩn hóa từ góc trên-trái.")]
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
    [Tooltip("Tự động quét/nhập ảnh mới mỗi X giây, không cần bấm phím I.")]
    [Min(0.5f)] public float autoImportIntervalSeconds = 1f;

    [Header("Default fish")]
    public string defaultFishFolderPath = "Assets/default_fish";
    public bool spawnDefaultFishOnStart = true;
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

    [Header("Scan alignment")]
    [Tooltip("Tự đo viền cá trên ảnh scan và căn khớp với alpha template trước khi tạo texture.")]
    public bool autoAlignScannedArtwork = true;
    [Range(40, 180)] public int artworkDarkPixelThreshold = 105;
    [Range(256, 1024)] public int alignmentSampleResolution = 768;
    [Tooltip("Nới rộng vùng tìm mực đậm quanh Scan Crop khai báo (tỉ lệ theo kích " +
             "thước Scan Crop), để loại QR/tiêu đề/logo tài trợ khỏi vùng tìm mà " +
             "vẫn chịu được sai lệch khi đặt giấy lên máy scan.")]
    [Range(0f, 1f)] public float autoAlignSearchPadding = 0.35f;

    [Header("Performance")]
    [Tooltip("Ngân sách xử lý pixel tối đa mỗi frame khi nhấn I.")]
    [Range(1f, 12f)] public float processingFrameBudgetMs = 4f;

    [Header("Spawn")]
    public BoxCollider2D swimBounds;
    [Tooltip("Vung tha ca nguoi choi: ca sinh o canh tren cua Box, X ngau nhien trong chieu rong Box.")]
    public BoxCollider2D playerDropSpawnBounds;
    [Tooltip("Khoang dem theo chieu ngang, tranh tha ca sat hai canh trai/phai cua Box.")]
    [Range(0f, 0.4f)] public float playerDropBoundsPadding = 0.08f;
    public Transform spawnParent;
    [Min(1f)] public float lifetimeSeconds = 300f;
    [Min(0.2f)] public float dropDurationSeconds = 1.5f;
    [Min(0f)] public float dropHeight = 2f;

    [Header("Water drop effect")]
    public Material dropBubbleMaterial;
    [Range(0.1f, 0.55f)] public float dropAirPhaseRatio = 0.28f;
    [Range(0.1f, 0.4f)] public float dropSettlePhaseRatio = 0.2f;
    [Range(0f, 0.8f)] public float dropSinkOvershoot = 0.22f;
    [Range(0f, 25f)] public float dropImpactTilt = 9f;
    [Range(0, 40)] public int dropBubbleCount = 18;
    public Vector2 dropBubbleSizeRange = new Vector2(0.08f, 0.2f);

    private readonly Queue<GameObject> pendingFish = new Queue<GameObject>();
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

        LoadImportLedger();

        if (spawnDefaultFishOnStart)
            SpawnDefaultFish();

        if (scanOnStart)
            ScanImagesInFolder();

        InvokeRepeating(nameof(ImportImagesNow), autoImportIntervalSeconds, autoImportIntervalSeconds);
    }

    void Update()
    {
        if (WasReleaseKeyPressed())
            ReleaseNextFish();

        if (WasImportKeyPressed())
        {
            ImportImagesNow();
        }
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

        if (string.IsNullOrWhiteSpace(qrId) || template == null ||
            template.prefab == null || template.alphaTemplate == null)
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
        string resolvedFolder = ResolveFolderPath(defaultFishFolderPath);
        if (!Directory.Exists(resolvedFolder))
        {
            Debug.LogWarning("[Default Fish] Không tìm thấy thư mục: " + resolvedFolder);
            return;
        }

        string[] files = Array.FindAll(Directory.GetFiles(resolvedFolder), IsSupportedImage);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        int spawnedCount = 0;
        for (int index = 0; index < files.Length; index++)
        {
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

            if (template == null || template.prefab == null ||
                (!useReadyTexture && template.alphaTemplate == null))
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

    Texture2D BuildFishTexture(Texture2D scan, FishTemplate template)
    {
        Rect scanCrop = template.scanCrop;
        if (scanCrop.width <= 0f || scanCrop.height <= 0f)
        {
            // Calibration mặc định cho ảnh Comet Scanner 240 DPI của mẫu ca_heo.
            scanCrop = new Rect(0.01f, 0.095f, 0.98f, 0.8f);
            Debug.LogWarning($"[QR] Scan Crop của '{template.qrId}' chưa hợp lệ; " +
                             "đang dùng calibration mặc định.");
        }

        int outputHeight = Mathf.Max(1,
            Mathf.RoundToInt(outputWidth * (template.alphaTemplate.height /
                                             (float)template.alphaTemplate.width)));
        Texture2D mask = CreateReadableCopy(template.alphaTemplate, outputWidth, outputHeight);
        try
        {
            Texture2D output = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false);
            // Các material rig hiện tại lật UV bằng texture scale X = -1.
            // Repeat giữ phép lật này hoạt động; Clamp sẽ kẹp toàn bộ UV âm vào
            // cột alpha 0 ở mép texture và khiến cá hoàn toàn vô hình.
            output.wrapMode = TextureWrapMode.Repeat;
            output.filterMode = FilterMode.Bilinear;

            Color32[] scanPixels = scan.GetPixels32();
            Color32[] maskPixels = mask.GetPixels32();
            byte[] filledAlpha = GetFilledSilhouetteAlpha(
                template.alphaTemplate, maskPixels, mask.width, mask.height);

            if (autoAlignScannedArtwork && TryCalculateAlignedCrop(
                    scanPixels,
                    scan.width,
                    scan.height,
                    filledAlpha,
                    mask.width,
                    mask.height,
                    scanCrop,
                    out Rect alignedCrop))
            {
                scanCrop = alignedCrop;
                Debug.Log($"[FishTexture] Auto-aligned '{template.qrId}' crop: " +
                          $"x={scanCrop.x:F4}, y={scanCrop.y:F4}, " +
                          $"w={scanCrop.width:F4}, h={scanCrop.height:F4}");
            }

            Color32[] result = new Color32[outputWidth * outputHeight];
            int coloredPixelCount = 0;
            int opaquePixelCount = 0;

            for (int y = 0; y < outputHeight; y++)
            {
                float v = y / (float)Mathf.Max(1, outputHeight - 1);
                // scanCrop dùng gốc trên-trái, còn texture Unity dùng gốc dưới-trái.
                float scanVTop = scanCrop.y + (1f - v) * scanCrop.height;
                int scanYTop = Mathf.Clamp(Mathf.RoundToInt(scanVTop * (scan.height - 1)), 0, scan.height - 1);
                int scanY = scan.height - 1 - scanYTop;

                for (int x = 0; x < outputWidth; x++)
                {
                    float u = x / (float)Mathf.Max(1, outputWidth - 1);
                    byte alpha = filledAlpha[y * outputWidth + x];

                    float scanU = scanCrop.x + u * scanCrop.width;
                    int scanX = Mathf.Clamp(Mathf.RoundToInt(scanU * (scan.width - 1)), 0, scan.width - 1);
                    Color32 color = scanPixels[scanY * scan.width + scanX];
                    color.a = alpha;
                    result[y * outputWidth + x] = color;

                    if (alpha > 127)
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
            Debug.Log($"[FishTexture] Generated {output.width}x{output.height}; " +
                      $"colored pixels inside fish: {coloredPercent:F1}%");
            return output;
        }
        finally
        {
            Destroy(mask);
        }
    }

    IEnumerator BuildFishTextureIncrementally(
        Texture2D scan,
        FishTemplate template,
        Action<Texture2D> completed)
    {
        Rect scanCrop = template.scanCrop;
        if (scanCrop.width <= 0f || scanCrop.height <= 0f)
            scanCrop = new Rect(0.01f, 0.095f, 0.98f, 0.8f);

        int generatedWidth = Mathf.Max(256, outputWidth);
        int generatedHeight = Mathf.Max(1,
            Mathf.RoundToInt(generatedWidth * (template.alphaTemplate.height /
                                               (float)template.alphaTemplate.width)));
        Texture2D mask = null;
        Texture2D output = null;
        bool ownershipTransferred = false;

        try
        {
            output = new Texture2D(
                generatedWidth, generatedHeight, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            Color32[] scanPixels = scan.GetPixels32();
            string alphaCacheKey = template.alphaTemplate.GetInstanceID() + ":" +
                                   generatedWidth + "x" + generatedHeight;
            byte[] filledAlpha;
            if (!FilledAlphaCache.TryGetValue(alphaCacheKey, out filledAlpha))
            {
                mask = CreateReadableCopy(
                    template.alphaTemplate, generatedWidth, generatedHeight);
                Color32[] maskPixels = mask.GetPixels32();
                Task<byte[]> alphaTask = Task.Run(() =>
                    CalculateFilledSilhouetteAlpha(
                        maskPixels, generatedWidth, generatedHeight));
                while (!alphaTask.IsCompleted)
                    yield return null;

                if (alphaTask.IsFaulted || alphaTask.IsCanceled)
                    yield break;

                filledAlpha = alphaTask.Result;
                FilledAlphaCache[alphaCacheKey] = filledAlpha;
            }

            if (autoAlignScannedArtwork && TryCalculateAlignedCrop(
                    scanPixels,
                    scan.width,
                    scan.height,
                    filledAlpha,
                    generatedWidth,
                    generatedHeight,
                    scanCrop,
                    out Rect alignedCrop))
            {
                scanCrop = alignedCrop;
                Debug.Log($"[FishTexture] Auto-aligned '{template.qrId}' crop: " +
                          $"x={scanCrop.x:F4}, y={scanCrop.y:F4}, " +
                          $"w={scanCrop.width:F4}, h={scanCrop.height:F4}");
            }

            Color32[] resultPixels = new Color32[generatedWidth * generatedHeight];
            int coloredPixelCount = 0;
            int opaquePixelCount = 0;
            var frameTimer = System.Diagnostics.Stopwatch.StartNew();
            double budget = Math.Max(1d, processingFrameBudgetMs);

            for (int y = 0; y < generatedHeight; y++)
            {
                float v = y / (float)Mathf.Max(1, generatedHeight - 1);
                float scanVTop = scanCrop.y + (1f - v) * scanCrop.height;
                int scanYTop = Mathf.Clamp(
                    Mathf.RoundToInt(scanVTop * (scan.height - 1)),
                    0,
                    scan.height - 1);
                int scanY = scan.height - 1 - scanYTop;

                for (int x = 0; x < generatedWidth; x++)
                {
                    float u = x / (float)Mathf.Max(1, generatedWidth - 1);
                    byte alpha = filledAlpha[y * generatedWidth + x];
                    float scanU = scanCrop.x + u * scanCrop.width;
                    int scanX = Mathf.Clamp(
                        Mathf.RoundToInt(scanU * (scan.width - 1)),
                        0,
                        scan.width - 1);
                    Color32 color = scanPixels[scanY * scan.width + scanX];
                    color.a = alpha;
                    resultPixels[y * generatedWidth + x] = color;

                    if (alpha > 127)
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
            Debug.Log($"[FishTexture] Generated {output.width}x{output.height}; " +
                      $"colored pixels inside fish: {coloredPercent:F1}%");

            completed(output);
            ownershipTransferred = true;
        }
        finally
        {
            if (mask != null)
                Destroy(mask);
            if (!ownershipTransferred && output != null)
                Destroy(output);
        }
    }

    bool TryCalculateAlignedCrop(
        Color32[] scanPixels,
        int scanWidth,
        int scanHeight,
        byte[] silhouetteAlpha,
        int silhouetteWidth,
        int silhouetteHeight,
        Rect expectedScanCrop,
        out Rect crop)
    {
        crop = default;
        if (!TryGetAlphaBounds(
                silhouetteAlpha,
                silhouetteWidth,
                silhouetteHeight,
                out Rect templateBounds))
            return false;

        float templatePixelAspect =
            (templateBounds.width * silhouetteWidth) /
            Mathf.Max(1f, templateBounds.height * silhouetteHeight);

        // Chi tim trong vung lan can Scan Crop da khai bao, khong quet ca trang,
        // de QR/tieu de/logo tai tro (cung nam tren trang scan) khong bi nham
        // thanh "vung ca" khi tre to mau nhat hon cac khoi in san.
        Rect searchRegion = ExpandNormalizedRect(expectedScanCrop, autoAlignSearchPadding);

        if (!TryGetArtworkBounds(
                scanPixels,
                scanWidth,
                scanHeight,
                templatePixelAspect,
                searchRegion,
                out Rect artworkBounds))
            return false;

        float width = artworkBounds.width / Mathf.Max(0.0001f, templateBounds.width);
        float height = artworkBounds.height / Mathf.Max(0.0001f, templateBounds.height);
        float x = artworkBounds.xMin - templateBounds.xMin * width;
        float y = artworkBounds.yMin - templateBounds.yMin * height;

        Rect candidate = new Rect(x, y, width, height);
        if (candidate.width < 0.2f || candidate.width > 1.1f ||
            candidate.height < 0.2f || candidate.height > 1.1f ||
            candidate.xMin < -0.08f || candidate.yMin < -0.08f ||
            candidate.xMax > 1.08f || candidate.yMax > 1.08f)
            return false;

        crop = candidate;
        return true;
    }

    bool TryGetAlphaBounds(
        byte[] alpha,
        int width,
        int height,
        out Rect bounds)
    {
        int minX = width;
        int minYTop = height;
        int maxX = -1;
        int maxYTop = -1;

        for (int yBottom = 0; yBottom < height; yBottom++)
        {
            int yTop = height - 1 - yBottom;
            int row = yBottom * width;
            for (int x = 0; x < width; x++)
            {
                if (alpha[row + x] <= 32)
                    continue;

                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minYTop = Mathf.Min(minYTop, yTop);
                maxYTop = Mathf.Max(maxYTop, yTop);
            }
        }

        if (maxX < minX || maxYTop < minYTop)
        {
            bounds = default;
            return false;
        }

        bounds = Rect.MinMaxRect(
            minX / (float)width,
            minYTop / (float)height,
            (maxX + 1f) / width,
            (maxYTop + 1f) / height);
        return true;
    }

    bool TryGetArtworkBounds(
        Color32[] pixels,
        int width,
        int height,
        float targetAspect,
        Rect searchRegion,
        out Rect bounds)
    {
        int maximumDimension = Mathf.Clamp(alignmentSampleResolution, 256, 1024);
        int stride = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(width, height) / (float)maximumDimension));
        int gridWidth = Mathf.CeilToInt(width / (float)stride);
        int gridHeight = Mathf.CeilToInt(height / (float)stride);
        int gridLength = gridWidth * gridHeight;

        bool[] dark = new bool[gridLength];
        bool[] visited = new bool[gridLength];
        int borderX = Mathf.Max(1, Mathf.RoundToInt(gridWidth * 0.012f));
        int borderY = Mathf.Max(1, Mathf.RoundToInt(gridHeight * 0.012f));
        int threshold = Mathf.Clamp(artworkDarkPixelThreshold, 40, 180);

        for (int gy = borderY; gy < gridHeight - borderY; gy++)
        {
            int yTop = Mathf.Min(height - 1, gy * stride + stride / 2);
            float vTop = yTop / (float)height;
            if (vTop < searchRegion.yMin || vTop > searchRegion.yMax)
                continue;

            int yBottom = height - 1 - yTop;
            for (int gx = borderX; gx < gridWidth - borderX; gx++)
            {
                int x = Mathf.Min(width - 1, gx * stride + stride / 2);
                float u = x / (float)width;
                if (u < searchRegion.xMin || u > searchRegion.xMax)
                    continue;

                Color32 color = pixels[yBottom * width + x];
                int luminance = (299 * color.r + 587 * color.g + 114 * color.b) / 1000;
                dark[gy * gridWidth + gx] = luminance < threshold;
            }
        }

        int[] queue = new int[gridLength];
        float bestScore = 0f;
        int bestMinX = 0;
        int bestMinY = 0;
        int bestMaxX = -1;
        int bestMaxY = -1;

        for (int start = 0; start < gridLength; start++)
        {
            if (!dark[start] || visited[start])
                continue;

            int head = 0;
            int tail = 0;
            int count = 0;
            int startX = start % gridWidth;
            int startY = start / gridWidth;
            int minX = startX;
            int maxX = startX;
            int minY = startY;
            int maxY = startY;
            queue[tail++] = start;
            visited[start] = true;

            while (head < tail)
            {
                int current = queue[head++];
                int x = current % gridWidth;
                int y = current / gridWidth;
                count++;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);

                VisitDarkNeighbour(current - 1, x > 0, dark, visited, queue, ref tail);
                VisitDarkNeighbour(current + 1, x + 1 < gridWidth, dark, visited, queue, ref tail);
                VisitDarkNeighbour(current - gridWidth, y > 0, dark, visited, queue, ref tail);
                VisitDarkNeighbour(current + gridWidth, y + 1 < gridHeight, dark, visited, queue, ref tail);
                VisitDarkNeighbour(current - gridWidth - 1, x > 0 && y > 0, dark, visited, queue, ref tail);
                VisitDarkNeighbour(current - gridWidth + 1, x + 1 < gridWidth && y > 0, dark, visited, queue, ref tail);
                VisitDarkNeighbour(current + gridWidth - 1, x > 0 && y + 1 < gridHeight, dark, visited, queue, ref tail);
                VisitDarkNeighbour(current + gridWidth + 1,
                    x + 1 < gridWidth && y + 1 < gridHeight,
                    dark,
                    visited,
                    queue,
                    ref tail);
                VisitDarkNeighbour(current - gridWidth - 1, x > 0 && y > 0, dark, visited, queue, ref tail);
                VisitDarkNeighbour(current - gridWidth + 1, x + 1 < gridWidth && y > 0, dark, visited, queue, ref tail);
                VisitDarkNeighbour(current + gridWidth - 1, x > 0 && y + 1 < gridHeight, dark, visited, queue, ref tail);
                VisitDarkNeighbour(current + gridWidth + 1,
                    x + 1 < gridWidth && y + 1 < gridHeight,
                    dark,
                    visited,
                    queue,
                    ref tail);
            }

            if (count < 24)
                continue;

            float componentWidth = (maxX - minX + 1) * stride;
            float componentHeight = (maxY - minY + 1) * stride;
            float componentAspect = componentWidth / Mathf.Max(1f, componentHeight);
            float aspectError = Mathf.Abs(Mathf.Log(
                componentAspect / Mathf.Max(0.001f, targetAspect)));
            float score = count / (1f + aspectError * 4f);

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestMinX = minX;
            bestMaxX = maxX;
            bestMinY = minY;
            bestMaxY = maxY;
        }

        if (bestMaxX < bestMinX || bestMaxY < bestMinY)
        {
            bounds = default;
            return false;
        }

        // Nới rất nhẹ để giữ đủ phần anti-alias ở nét viền ngoài.
        float paddingX = stride * 0.75f / width;
        float paddingY = stride * 0.75f / height;
        bounds = Rect.MinMaxRect(
            Mathf.Max(0f, bestMinX * stride / (float)width - paddingX),
            Mathf.Max(0f, bestMinY * stride / (float)height - paddingY),
            Mathf.Min(1f, (bestMaxX + 1) * stride / (float)width + paddingX),
            Mathf.Min(1f, (bestMaxY + 1) * stride / (float)height + paddingY));
        return true;
    }

    static Rect ExpandNormalizedRect(Rect rect, float padding)
    {
        float padX = rect.width * padding;
        float padY = rect.height * padding;
        return Rect.MinMaxRect(
            Mathf.Clamp01(rect.xMin - padX),
            Mathf.Clamp01(rect.yMin - padY),
            Mathf.Clamp01(rect.xMax + padX),
            Mathf.Clamp01(rect.yMax + padY));
    }

    static void VisitDarkNeighbour(
        int index,
        bool isValid,
        bool[] dark,
        bool[] visited,
        int[] queue,
        ref int tail)
    {
        if (!isValid || !dark[index] || visited[index])
            return;

        visited[index] = true;
        queue[tail++] = index;
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
            Vector2 viewportXY = fixedViewportPosition ?? new Vector2(
                UnityEngine.Random.Range(0.2f, 0.8f),
                UnityEngine.Random.Range(0.25f, 0.75f));
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

        if (prepareDrop && playerDropSpawnBounds != null)
        {
            Bounds dropBounds = playerDropSpawnBounds.bounds;
            float padding = Mathf.Clamp(playerDropBoundsPadding, 0f, 0.4f);
            float dropX = Mathf.Lerp(
                dropBounds.min.x,
                dropBounds.max.x,
                UnityEngine.Random.Range(padding, 1f - padding));
            queuedPosition = new Vector3(
                dropX,
                dropBounds.max.y,
                landingPosition.z);

            // Keep the whole drop inside the selected left-side region. Previously the
            // spawn point used the Box but the landing point kept its random screen X,
            // making the fish immediately drift back toward the centre.
            landingPosition.x = dropX;
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

        animation.swimBounds = swimBounds;
        animation.lifetime = prepareDrop ? lifetimeSeconds : defaultFishLifetimeSeconds;

        // Hướng gốc của prefab là nguồn chuẩn. Trước đây template QR có thể ghi đè
        // bằng một giá trị cũ và khiến riêng một số loại cá bơi bằng đuôi.
        DOTweenFishAnim prefabAnimation = template.prefab.GetComponent<DOTweenFishAnim>();
        animation.spriteFacesRight = prefabAnimation != null
            ? prefabAnimation.spriteFacesRight
            : template.spriteFacesRight;
        if (isDefaultFish)
        {
            animation.swimSpeed *= Mathf.Lerp(
                defaultFishDepthSpeedRange.x,
                defaultFishDepthSpeedRange.y,
                Mathf.Clamp01(depth01));
            animation.useSwimLane = true;
            animation.swimLaneCenter01 = Mathf.InverseLerp(
                defaultFishViewportArea.yMin,
                defaultFishViewportArea.yMax,
                fixedViewportPosition.Value.y);
            animation.swimLaneHeight01 = defaultFishLaneHeight;
        }
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
                dropImpactTilt,
                dropBubbleMaterial,
                dropBubbleCount,
                dropBubbleSizeRange);
        }

        BackgroundFishSpawner interactionSettings =
            UnityEngine.Object.FindFirstObjectByType<BackgroundFishSpawner>();
        if (interactionSettings != null)
            interactionSettings.ConfigureClickInteraction(fish);

        Debug.Log($"[QR] Spawn: {spawnPosition}, landing: {landingPosition}, " +
                  $"texture: {texture.width}x{texture.height}");
        return fish;
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

    static bool WasReleaseKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.P);
#else
        return false;
#endif
    }

    static bool WasImportKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.I);
#else
        return false;
#endif
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

    static Texture2D CreateReadableCopy(Texture source, int width, int height)
    {
        RenderTexture temporary = RenderTexture.GetTemporary(
            width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;

        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            Texture2D copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            copy.Apply(false, false);
            return copy;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
        }
    }

    static byte[] GetFilledSilhouetteAlpha(
        Texture2D sourceTemplate,
        Color32[] maskPixels,
        int width,
        int height)
    {
        string cacheKey = sourceTemplate.GetInstanceID() + ":" + width + "x" + height;
        if (FilledAlphaCache.TryGetValue(cacheKey, out byte[] cachedAlpha))
            return cachedAlpha;

        int pixelCount = width * height;
        // 0 = chưa phân loại, 1 = nền ngoài, 2 = nét biên.
        byte[] state = new byte[pixelCount];
        byte[] filledAlpha = new byte[pixelCount];

        // Dilate nét template một pixel để đóng các khe rất nhỏ sinh ra khi resize.
        // Nếu không đóng khe, flood fill có thể lọt vào bên trong thân cá.
        for (int y = 0; y < height; y++)
        {
            int minY = Mathf.Max(0, y - 1);
            int maxY = Mathf.Min(height - 1, y + 1);

            for (int x = 0; x < width; x++)
            {
                int minX = Mathf.Max(0, x - 1);
                int maxX = Mathf.Min(width - 1, x + 1);
                bool isBoundary = false;

                for (int sampleY = minY; sampleY <= maxY && !isBoundary; sampleY++)
                {
                    int sampleRow = sampleY * width;
                    for (int sampleX = minX; sampleX <= maxX; sampleX++)
                    {
                        if (maskPixels[sampleRow + sampleX].a > 8)
                        {
                            isBoundary = true;
                            break;
                        }
                    }
                }

                if (isBoundary)
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

        int opaqueCount = 0;
        for (int i = 0; i < pixelCount; i++)
        {
            // Mọi pixel flood-fill không chạm tới đều nằm trong silhouette kín.
            if (state[i] != 1)
            {
                filledAlpha[i] = 255;
                opaqueCount++;
            }
        }

        FilledAlphaCache[cacheKey] = filledAlpha;
        Debug.Log($"[FishTexture] Filled silhouette alpha: " +
                  $"{opaqueCount * 100f / pixelCount:F1}% opaque");
        return filledAlpha;
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
