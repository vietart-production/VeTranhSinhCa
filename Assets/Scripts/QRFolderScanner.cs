using System.IO;
using UnityEngine;
using ZXing;
using System.Collections.Generic;

public class QRFolderScanner : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Đường dẫn đến thư mục chứa ảnh (ví dụ: Assets/scaning_folder hoặc D:/images)")]
    public string folderPath = "Assets/scaning_folder";

    [Tooltip("Tự động quét khi bắt đầu game")]
    public bool scanOnStart = true;

    [Header("Spawning")]
    [Tooltip("Danh sách các prefab để Instantiate. Kéo thả các Prefab vào đây. Tên của prefab phải khớp 100% với nội dung mã QR.")]
    public List<GameObject> prefabsToSpawn = new List<GameObject>();

    [Tooltip("Nơi sẽ chứa các prefab được sinh ra (có thể bỏ trống)")]
    public Transform spawnParent;

    [Tooltip("Khoảng cách giữa các vật thể sinh ra")]
    public Vector3 spawnOffset = new Vector3(2, 0, 0);

    private int spawnedCount = 0; // Dùng để dịch chuyển vị trí mỗi khi spawn thêm object mới

    void Start()
    {
        if (scanOnStart)
        {
            ScanImagesInFolder();
        }
    }

    [ContextMenu("Scan Folder Now")]
    public void ScanImagesInFolder()
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            Debug.LogError("Đường dẫn thư mục trống. Vui lòng nhập đường dẫn trong Inspector.");
            return;
        }

        if (!Directory.Exists(folderPath))
        {
            Debug.LogError("Không tìm thấy thư mục: " + folderPath);
            return;
        }

        string[] files = Directory.GetFiles(folderPath);
        Debug.Log($"[Bắt đầu] Đang quét thư mục: '{folderPath}'. Tìm thấy {files.Length} file trong thư mục này.");
        
        MultiFormatReader reader = new MultiFormatReader();
        var hints = new System.Collections.Generic.Dictionary<DecodeHintType, object>();
        hints.Add(DecodeHintType.TRY_HARDER, true);
        reader.Hints = hints;

        int count = 0;
        spawnedCount = 0; // Reset lại biến đếm khi bắt đầu quét mới

        foreach (string file in files)
        {
            // Bỏ qua các file meta của Unity
            if (file.EndsWith(".meta") || file.EndsWith(".ini")) continue;

            Debug.Log($"[Xử lý] Đang đọc file: {Path.GetFileName(file)}");

            // Đọc file ảnh dưới dạng byte array
            byte[] fileData;
            try
            {
                fileData = File.ReadAllBytes(file);
                Debug.Log($"[Xử lý] Đã đọc xong byte array, kích thước: {fileData.Length} bytes");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Lỗi] Không thể đọc file {file}: {e.Message}");
                continue;
            }

            // Tạo Texture2D và load ảnh từ byte array
            Texture2D texture = new Texture2D(2, 2);
            bool isLoaded = texture.LoadImage(fileData);
            Debug.Log($"[Xử lý] LoadImage thành công? {isLoaded}");

            if (isLoaded)
            {
                bool isTextureUsed = false;

                Color32[] pixels = texture.GetPixels32();
                int width = texture.width;
                int height = texture.height;

                // Chuyển Color32[] sang byte[] định dạng RGB24
                byte[] rawRGB = new byte[width * height * 3];
                int idx = 0;
                // Unity GetPixels32 trả về mảng từ dưới lên trên (bottom-up), ZXing cần từ trên xuống (top-down)
                for (int y = height - 1; y >= 0; y--)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color32 pixel = pixels[y * width + x];
                        rawRGB[idx++] = pixel.r;
                        rawRGB[idx++] = pixel.g;
                        rawRGB[idx++] = pixel.b;
                    }
                }

                try
                {
                    // Decode mã QR bằng MultiFormatReader
                    var source = new RGBLuminanceSource(rawRGB, width, height, RGBLuminanceSource.BitmapFormat.RGB24);
                    var bitmap = new ZXing.BinaryBitmap(new ZXing.Common.HybridBinarizer(source));
                    Result result = reader.decode(bitmap);

                    if (result != null)
                    {
                        string qrText = result.Text.Trim(); // Cắt khoảng trắng dư thừa (nếu có)
                        Debug.Log($"<color=green>[Thành công]</color> Ảnh '{Path.GetFileName(file)}' chứa nội dung QR: <b>{qrText}</b>");
                        
                        GameObject prefabToSpawn = null;
                        
                        // Ưu tiên 1: Tìm trong list truyền vào từ Inspector
                        if (prefabsToSpawn != null)
                        {
                            foreach (var p in prefabsToSpawn)
                            {
                                if (p != null && p.name == qrText)
                                {
                                    prefabToSpawn = p;
                                    break;
                                }
                            }
                        }

                        // Ưu tiên 2: Tìm trong thư mục Resources (Resources/Prefabs/tên_qr)
                        if (prefabToSpawn == null)
                        {
                            prefabToSpawn = Resources.Load<GameObject>("Prefabs/" + qrText);
                        }

                        // Thực hiện spawn nếu tìm thấy Prefab
                        if (prefabToSpawn != null)
                        {
                            Vector3 spawnPos = transform.position + (spawnOffset * spawnedCount);
                            GameObject spawnedObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity, spawnParent);
                            spawnedObj.name = qrText;

                            // TÌM VÀ ÁP DỤNG ẢNH LÊN MODEL 3D
                            Renderer[] renderers = spawnedObj.GetComponentsInChildren<Renderer>();
                            foreach (Renderer r in renderers)
                            {
                                // Việc gọi r.material sẽ tự động tạo một Material Instance, 
                                // nên không sợ bị đè texture của các con cá khác hay đè file gốc.
                                r.material.mainTexture = texture;
                            }
                            isTextureUsed = true; // Đánh dấu đã dùng texture này

                            spawnedCount++;
                            Debug.Log($"Đã Spawn: {qrText} tại vị trí {spawnPos}");
                        }
                        else
                        {
                            Debug.LogWarning($"<color=yellow>Không thể Spawn:</color> Không tìm thấy Prefab nào có tên '{qrText}' trong danh sách (Inspector) hoặc thư mục Resources/Prefabs.");
                        }
                    }
                    else
                    {
                        Debug.Log($"<color=orange>[Không tìm thấy QR]</color> trong ảnh '{Path.GetFileName(file)}'");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Lỗi Decode] Đã xảy ra lỗi khi giải mã ảnh {Path.GetFileName(file)}: {ex.Message}");
                }

                // Xóa texture khỏi bộ nhớ nếu không được dùng đến (tránh rò rỉ RAM)
                if (!isTextureUsed)
                {
                    Destroy(texture);
                }
                count++;
            }
            else
            {
                Debug.LogWarning($"[Cảnh báo] Unity không thể nhận diện định dạng ảnh của file '{Path.GetFileName(file)}'.");
                Destroy(texture);
            }
        }

        Debug.Log($"Đã hoàn thành quét {count} ảnh trong thư mục.");
    }
}
