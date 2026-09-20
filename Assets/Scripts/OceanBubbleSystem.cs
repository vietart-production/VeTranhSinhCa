using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// OCEAN BUBBLE SYSTEM — thay thế hoàn toàn VFX Graph cũ.
/// 1 prefab bong bóng (SpriteRenderer 2D) + object pool + DOTween.
///
/// ⭐ KÍCH THƯỚC LÀ HỆ SỐ NHÂN, KHÔNG PHẢI MÉT. sizeMultiplierRange = (0.5, 1.2)
/// nghĩa là bong bóng có scale từ 0.5× đến 1.2× localScale GỐC của prefab. Muốn cả
/// đám to lên → chỉnh scale prefab, không cần đụng vào các số ở đây.
///
/// Ý tưởng để đám bong bóng "ra đại dương" chứ không phải confetti bay lên:
///  • DÀY ĐẶC Ở TÂM: r = R * u^coreDensityBias (u^0.5 = trải đều; mũ càng lớn càng dồn tâm).
///  • PHÂN BỐ CỠ LỆCH: đa số li ti, thi thoảng mới có 1 quả to.
///  • PHỌT RA TỪ TÂM RỒI MỚI LÊN: hạt sinh NGAY tại điểm chạm (không mọc sẵn ở vị trí
///    ngẫu nhiên). outwardVel = lateralVel (đẩy hạt trôi ra tới bán kính r đã random,
///    tỉ lệ theo r nên lõi dày đặc gần như đứng yên) + diveVel (⭐ vận tốc lặn XUỐNG
///    ĐỘC LẬP với r — diveSpeed, cùng logic với diveFizzSpeed của li ti — nên MỌI hạt
///    đều lặn rõ như nhau, không chỉ hạt rìa). Cả hai tắt dần theo outwardDamping rồi
///    mới nhường chỗ cho riseSpeed (OceanBubble.Tick) đẩy lên như bọt khí thật.
///  • RẢI DỌC ĐƯỜNG KÉO: hạt rải ngẫu nhiên trên đoạn con trỏ đi trong frame.
///  • KÉO NHANH = SỦI MẠNH: rate nội suy theo tốc độ con trỏ.
///  • BURST khi bắt đầu giữ và khi thả tay.
///  • DIVE FIZZ: RIÊNG 1 lớp bong bóng LI TI (cùng cơ chế phọt-xuống-từ-tâm ở trên
///    nhưng cỡ nhỏ hơn nhiều, bán kính rải rộng hơn để không bị đám bong bóng to che
///    khuất) — phọt 1 đợt lúc vừa chạm (spawnDiveFizzOnPress) RỒI sinh thêm liên tục
///    trong suốt lúc giữ (diveFizzContinuous/diveFizzRatePerSecond). ⭐ Lớp này KHÔNG
///    bị chặn bởi maxAlive như bong bóng thường — spawn tự do, xem SpawnOneDiveFizz.
///  • THU HỒI: bong bóng chỉ mất khi bay ra HẲN NGOÀI khung hình camera (cullWhenOffscreen,
///    xem OceanBubble.Tick/IsOffScreen) — không có mực nước cố định nào cả. Hết lifetime
///    mà vẫn còn trong khung hình thì mới chạy animation vỡ-co-lại (OceanBubble.Pop).
///  • XOÁY QUANH TRỤC (vortex, axisConvergeRate): mỗi hạt quay quanh 1 trục Y thẳng đứng
///    đâm lên mặt nước đúng tại điểm nó sinh ra (dùng spiralSpeed sẵn có), đồng thời co
///    dần bán kính quỹ đạo về 0 khi đã lên cao hơn — xem OceanBubble.ApplyAxisVortex.
///
/// Prefab yêu cầu: SpriteRenderer + component <see cref="OceanBubble"/>.
/// KHÔNG collider, KHÔNG Rigidbody. localScale prefab = cỡ chuẩn (hệ số 1).
/// </summary>
[DisallowMultipleComponent]
public class OceanBubbleSystem : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════
    [Header("PREFAB & POOL")]

    [Tooltip("Prefab bong bóng: 1 GameObject có SpriteRenderer + component OceanBubble, KHÔNG collider.\n\n" +
             "⭐ localScale của prefab chính là cỡ CHUẨN (hệ số 1). Muốn cả đám to/nhỏ hơn → sửa scale prefab, " +
             "không cần đụng vào sizeMultiplierRange bên dưới.")]
    public OceanBubble bubblePrefab;

    [Tooltip("Số instance tạo sẵn lúc Awake (đang tắt) để frame đầu tiên không giật vì Instantiate.\n\n" +
             "Đặt xấp xỉ số bong bóng thường thấy cùng lúc. Quá nhỏ = khựng nhẹ lần chạm đầu; " +
             "quá lớn = tốn RAM + thời gian load scene.\nGợi ý: 120–200.")]
    [Min(0)] public int prewarmCount = 160;

    [Tooltip("Trần số bong bóng sống cùng lúc. Chạm trần thì hệ thống NGỪNG sinh thêm (bong bóng cũ vẫn nổi bình thường) " +
             "— đây là van an toàn chống tụt FPS khi người dùng giữ tay quá lâu.\n\n" +
             "Mobile: 250–400. PC: 500–800.")]
    [Min(1)] public int maxAlive = 500;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("⭐ SPRITE 2D")]

    [Tooltip("Sprite luôn xoay mặt về camera.\n\n" +
             "BẬT nếu camera là Perspective hoặc bong bóng nằm rải rác theo chiều sâu — nếu không sprite " +
             "sẽ bị nhìn nghiêng, mỏng dần rồi biến mất.\n" +
             "TẮT được nếu camera Orthographic nhìn thẳng trục Z (tiết kiệm 1 phép gán rotation/hạt).")]
    public bool billboard = true;

    [Tooltip("Camera dùng để billboard. Để trống = tự lấy Camera.main.\n" +
             "Chỉ cần gán tay khi scene có nhiều camera hoặc camera chính không tag MainCamera.")]
    public Camera billboardCamera;

    [Tooltip("Lệch sortingOrder ngẫu nhiên ± quanh giá trị của prefab, giúp các sprite chồng nhau không bị " +
             "vẽ theo đúng một thứ tự phẳng lì → đám có chiều sâu.\n\n" +
             "Để 0 nếu bạn đang dùng camera Perspective + Transparency Sort Mode theo khoảng cách " +
             "(khi đó Unity đã tự sắp xếp theo Z).\nGợi ý: 2–5.")]
    [Min(0)] public int sortingOrderJitter = 3;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("⭐ CỤM QUANH CON TRỎ")]

    [Tooltip("Bán kính đám bong bóng quanh hold point (world units).\n\n" +
             "Nhỏ (0.2–0.4) = tia sủi tập trung như ống thở.\n" +
             "Lớn (1–2) = cả vùng nước sôi lên quanh ngón tay.\n" +
             "Chọn object này trong scene sẽ thấy 2 vòng gizmo: vòng ngoài = bán kính này, vòng trong = lõi dày đặc.")]
    public float clusterRadius = 0.75f;

    [Tooltip("⭐ ĐỘ DỒN VÀO TÂM. Điểm sinh lấy theo công thức r = R × u^bias.\n\n" +
             "0.5 = trải ĐỀU trên cả đĩa (nhìn bẹt, rìa cũng dày như tâm).\n" +
             "1.0 = hơi dồn vào giữa.\n" +
             "1.4–2.0 = lõi rất dày quanh con trỏ, rìa chỉ lác đác vài quả → đúng cảm giác 'búi bong bóng'.\n" +
             "3.0 = gần như tất cả dính vào 1 điểm.")]
    [Range(0.5f, 3f)] public float coreDensityBias = 1.4f;

    [Tooltip("Tán ngẫu nhiên theo trục Z (chiều sâu, ±giá trị này).\n\n" +
             "Cảnh gần như 2D: để nhỏ (0.05–0.2) — vừa đủ tránh z-fighting giữa các sprite.\n" +
             "Cảnh 3D thật: tăng lên để đám có bề dày, nhưng nhớ bật billboard.")]
    public float depthSpread = 0.15f;

    [Tooltip("Lệch cao độ ngẫu nhiên của điểm sinh (±). Tránh việc mọi hạt xuất phát cùng một mực nước " +
             "tạo thành 'đường kẻ' rõ rệt lúc mới phụt ra.\nGợi ý: 0.05–0.2.")]
    public float spawnHeightJitter = 0.12f;

    [Tooltip("Hệ số nhân thêm vào vận tốc phọt ra từ tâm (nhân với vận tốc tính từ bán kính " +
             "random r × outwardDamping — xem SpawnOne). 1 = phọt đúng khớp tới bán kính r đã random. " +
             "» 1 = cú phọt giật mạnh hơn, hạt vọt ra xa hơn tầm r rồi mới bị nước cản kéo lại; " +
             "‹ 1 = phọt yếu, hạt gần như trôi ra từ từ chứ không bắn.\n0 = tắt hẳn hiệu ứng phọt.")]
    public float outwardBurstSpeed = 1f;

    [Tooltip("Hệ số cản nước làm tắt vận tốc bung ở trên (tắt theo hàm mũ e^-k·t).\n\n" +
             "Càng LỚN càng nhanh 'đứng' lại → cú phụt ngắn và gọn.\n" +
             "Càng NHỎ hạt càng bay xa trước khi chịu nổi lên → đám loang rộng.\n" +
             "Gợi ý: 2–5.")]
    public float outwardDamping = 3.2f;

    [Tooltip("⭐ CÙNG LOGIC CHÌM NHƯ LI TI (diveFizzSpeed): vận tốc bắn THẲNG XUỐNG lúc mới sinh, " +
             "ĐỘC LẬP với bán kính r random — mọi bong bóng đều lặn mạnh như nhau lúc vừa phọt ra, " +
             "không chỉ hạt rìa (r lớn) mới lặn rõ như travelSpeed phía trên (vốn tỉ lệ theo r nên hạt " +
             "ở lõi dày đặc, r≈0, gần như không lặn chút nào). 0 = tắt hẳn, chỉ còn cú bung ngang.")]
    [Min(0f)] public float diveSpeed = 1.2f;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("⭐ TỐC ĐỘ PHÁT")]

    [Tooltip("Số bong bóng sinh mỗi GIÂY khi con trỏ đứng yên (giữ nguyên một chỗ).\n" +
             "Gợi ý: 25–60. Tăng cao nhớ tăng maxAlive theo.")]
    [Min(0)] public float idleRate = 45f;

    [Tooltip("Số bong bóng sinh mỗi GIÂY khi con trỏ kéo ở tốc độ tối đa.\n\n" +
             "Chênh lệch với idleRate chính là cảm giác 'khuấy nước mạnh thì sủi nhiều'. " +
             "Muốn tắt hiệu ứng này thì đặt bằng idleRate.")]
    [Min(0)] public float dragRate = 140f;

    [Tooltip("Tốc độ kéo con trỏ (đơn vị/giây) để đạt dragRate. Dưới mức này thì nội suy tuyến tính từ idleRate.\n\n" +
             "Đặt nhỏ (2–3) = chỉ cần nhích nhẹ đã sủi tối đa. Đặt lớn (8–10) = phải quẹt thật nhanh mới bùng lên.")]
    public float dragSpeedForMaxRate = 5f;

    [Tooltip("Số hạt phụt ra NGAY khi bắt đầu chạm/giữ. Tạo 'cú đấm' mở màn thay vì vòi nước bật từ từ.\n" +
             "Gợi ý: 30–70.")]
    [Min(0)] public int pressBurst = 55;

    [Tooltip("Số hạt phụt ra khi THẢ tay — cú ngắt cuối của luồng khí. Nên nhỏ hơn pressBurst.\n" +
             "Để 0 nếu muốn nguồn tắt lịm hoàn toàn.")]
    [Min(0)] public int releaseBurst = 22;

    [Tooltip("Tự động Burst khi bắt đầu/kết thúc phát, để TouchOceanManager chỉ cần gọi SetEmitPoint().\n\n" +
             "Tắt nếu bạn muốn tự kiểm soát thời điểm burst bằng cách gọi Burst() từ script khác.")]
    public bool autoBurstOnStartStop = true;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("⭐ LI TI PHỌT XUỐNG LÚC CHẠM (dive fizz)")]

    [Tooltip("Bật: ngay lúc vừa chạm/giữ (press), phun thêm 1 đợt bong bóng RẤT NHỎ bắn " +
             "XUỐNG DƯỚI điểm chạm trước, rồi mới trồi lên bình thường (mô phỏng bọt khí bị " +
             "ngón tay ấn vào nước) — tách biệt với pressBurst thường ở trên.")]
    public bool spawnDiveFizzOnPress = true;

    [Tooltip("Số bong bóng li ti phun xuống NGAY LÚC vừa chạm (cú đấm mở màn). Nên để CAO (40–90) vì " +
             "mỗi hạt rất nhỏ, cần số lượng đông mới ra cảm giác 'sủi bọt li ti dày đặc'.")]
    [Min(0)] public int diveFizzCount = 60;

    [Tooltip("Bật: SAU cú phọt mở màn ở trên, tiếp tục sinh thêm li ti LIÊN TỤC trong suốt lúc đang " +
             "giữ tay/kéo — không chỉ 1 lần lúc chạm xuống.")]
    public bool diveFizzContinuous = true;

    [Tooltip("Số bong bóng li ti sinh thêm mỗi GIÂY trong lúc đang giữ (chỉ có tác dụng khi " +
             "diveFizzContinuous bật). Cộng thêm vào rate của idleRate/dragRate bên trên.")]
    [Min(0f)] public float diveFizzRatePerSecond = 35f;

    [Tooltip("Khoảng cỡ (hệ số nhân localScale gốc) cho lớp li ti này — CHỦ Ý nhỏ hơn hẳn " +
             "sizeMultiplierRange thường (X = nhỏ nhất, Y = lớn nhất).")]
    public Vector2 diveFizzSizeRange = new Vector2(0.06f, 0.22f);

    [Tooltip("Vận tốc bắn THẲNG XUỐNG lúc mới sinh (đơn vị/giây). Càng lớn càng lặn sâu/lâu " +
             "trước khi outwardDamping tắt dần và bọt bắt đầu trồi lên.")]
    [Min(0f)] public float diveFizzSpeed = 1.6f;

    [Tooltip("⭐ Hệ số nhân RIÊNG vào outwardDamping cho lớp li ti (không ảnh hưởng bong bóng thường).\n\n" +
             "Li ti sống rất ngắn (diveFizzLifeMul), nên nếu dùng chung outwardDamping chậm với bong " +
             "bóng thường thì gần như suốt vòng đời vẫn bị vận tốc lặn (giống nhau cho mọi hạt) chi phối, " +
             "chưa kịp lộ ra tốc độ NỔI khác nhau theo cỡ (riseSizeExponent) thì đã hết đời/bay khỏi màn " +
             "hình → nhìn như mọi hạt li ti nổi cùng tốc độ. Để > 1 (ví dụ 2–3) cho cú lặn của li ti tắt " +
             "nhanh hơn, nhường chỗ sớm cho tốc độ nổi riêng từng hạt.")]
    [Min(0.01f)] public float diveFizzDampingMul = 2.5f;

    [Tooltip("⭐ Hệ số nhân RIÊNG vào riseSpeed cho lớp li ti (không ảnh hưởng bong bóng thường) — " +
             "vận tốc NỔI LÊN riêng cho li ti, tách khỏi riseSpeed dùng chung.\n\n" +
             "1 = dùng đúng riseSpeed chung (chỉ khác nhau do cỡ qua riseSizeExponent). " +
             "‹ 1 = li ti nổi chậm/lờ đờ rõ rệt hơn hẳn bong bóng thường. › 1 = li ti nổi nhanh hơn.")]
    [Min(0f)] public float diveFizzRiseSpeedMul = 1f;

    [Tooltip("Bán kính rải quanh điểm chạm cho lớp li ti — nên để RỘNG HƠN NHIỀU so với " +
             "clusterRadius (bong bóng to). Vì hạt li ti quá nhỏ nên nếu rải cùng bán kính với " +
             "bong bóng to, chúng sẽ nằm lọt phía sau/dưới đám bong bóng to và bị che khuất mất; " +
             "rải rộng ra thì phần lộ ra ngoài rìa đám to mới nhìn thấy được lớp li ti lấm tấm.")]
    [Min(0f)] public float diveFizzRadius = 1.6f;

    [Tooltip("Nhân vào tuổi thọ (so với lifetime chung) — li ti nên sống ngắn hơn bong bóng thường.")]
    [Min(0.01f)] public float diveFizzLifeMul = 0.6f;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("⭐ KÍCH THƯỚC — HỆ SỐ NHÂN VỚI SCALE GỐC CỦA PREFAB")]

    [Tooltip("Khoảng random cỡ cho bong bóng THƯỜNG (X = nhỏ nhất, Y = lớn nhất).\n\n" +
             "1 = đúng bằng localScale của prefab. 0.45 = bằng 45% prefab.\n" +
             "⭐ Phân bố lệch về phía cận DƯỚI (random²) nên đa số hạt sẽ li ti, chỉ vài hạt chạm mức Y — " +
             "đây chính là thứ làm đám bong bóng trông thật thay vì đều tăm tắp.")]
    public Vector2 sizeMultiplierRange = new Vector2(0.315f, 0.77f);

    [Tooltip("Khoảng random cỡ cho vài quả TO nổi bật (X = nhỏ nhất, Y = lớn nhất).\n\n" +
             "Nên tách hẳn khỏi sizeMultiplierRange (ví dụ 1.5–2.4 so với 0.45–1.1) để có sự đối lập rõ: " +
             "một biển hạt li ti + dăm quả to lừ đừ nổi lên.")]
    public Vector2 bigSizeMultiplierRange = new Vector2(1.05f, 1.68f);

    [Tooltip("Xác suất một hạt bất kỳ là quả TO (0 = không bao giờ, 1 = tất cả đều to).\n" +
             "Gợi ý: 0.05–0.15. Trên 0.3 là mất cảm giác 'hiếm' của quả to.")]
    [Range(0f, 1f)] public float bigBubbleChance = 0.10f;

    [Tooltip("Hệ số được coi là 'cỡ chuẩn' khi quy đổi tốc độ nổi và tần số lắc. Thường để 1.\n\n" +
             "Bong bóng có hệ số bằng đúng giá trị này sẽ nổi đúng bằng riseSpeed bên dưới; " +
             "hạt to hơn thì nhanh hơn, hạt nhỏ hơn thì chậm hơn.")]
    [Min(0.01f)] public float referenceMultiplier = 1f;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("⭐ NỔI LÊN")]

    [Tooltip("Tốc độ nổi (đơn vị/giây) của bong bóng cỡ CHUẨN. Các cỡ khác được quy đổi qua riseSizeExponent.\n\n" +
             "Nước sâu, tĩnh: 0.4–0.8. Gần mặt nước, sôi động: 1.2–2.")]
    public float riseSpeed = 1.0f;

    [Tooltip("⭐ Quan hệ giữa CỠ và TỐC ĐỘ NỔI: v ~ size^exponent.\n\n" +
             "0.2 = mọi cỡ nổi gần như nhau (nhìn giả, như hạt particle).\n" +
             "0.5–0.7 = TỰ NHIÊN: quả to vọt lên, hạt li ti gần như lơ lửng → đám tự phân tầng theo cỡ.\n" +
             "1.5 = chênh lệch cực đoan, quả to bắn thẳng lên mất hút.")]
    [Range(0.2f, 1.5f)] public float riseSizeExponent = 0.6f;

    [Tooltip("Thời gian (giây) tăng tốc từ 0 → tốc độ tới hạn, bằng DOTween Ease.OutQuad.\n\n" +
             "Mô phỏng quán tính nước: lực đẩy Ác-si-mét phải thắng dần lực cản chứ không đạt tốc độ tối đa ngay.\n" +
             "0.1 = giật lên ngay (khô khan). 0.5–0.8 = mượt, nặng nước.")]
    [Min(0.01f)] public float riseAccelTime = 0.55f;

    [Tooltip("Tuổi thọ ngẫu nhiên của mỗi bong bóng, tính bằng giây (X = min, Y = max). " +
             "Hết tuổi thọ thì hạt tự vỡ dù chưa kịp trôi ra khỏi khung hình.\n\n" +
             "Đây là van an toàn giữ số hạt sống trong tầm kiểm soát. Nếu bong bóng biến mất giữa chừng " +
             "trước khi kịp bay ra khỏi màn hình thì tăng giá trị này.")]
    public Vector2 lifetime = new Vector2(2.5f, 6f);

    [Tooltip("Bong bóng NỞ ra bao nhiêu lần khi đi hết đời (áp suất nước giảm dần khi lên cao).\n\n" +
             "1 = không nở. 1.2–1.4 = tự nhiên. Trên 2 là phóng đại kiểu hoạt hình.")]
    [Min(1f)] public float growthOverLife = 1.3f;

    [Tooltip("Bật: bong bóng bay ra HẲN NGOÀI khung hình camera (xem offscreenMargin) rồi mới bị thu " +
             "hồi thẳng VỀ POOL — không chạy animation vỡ vì lúc đó không còn ai nhìn thấy nữa.\n" +
             "Tắt: bong bóng chỉ mất khi hết lifetime (kiểu tan dần trong nước sâu, không có mặt nước " +
             "trong khung hình) — dùng Pop() có animation co lại.")]
    public bool cullWhenOffscreen = true;

    [Tooltip("Biên NGOÀI khung hình (tỉ lệ viewport, 0–1) trước khi thật sự thu hồi — cho bong bóng " +
             "trôi qua khỏi mép màn hình thêm 1 đoạn trước khi biến mất, tránh cảm giác bị chặn cứng " +
             "đúng tại mép. 0 = thu hồi ngay khi vừa qua mép. Gợi ý: 0.15–0.3.")]
    [Range(0f, 1f)] public float offscreenMargin = 0.2f;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("⭐ LẮC / XOÁY (đường đi zig-zag)")]

    [Tooltip("Tần số lắc cơ bản (dao động/giây). Hạt NHỎ tự động lắc nhanh hơn, hạt TO lắc chậm hơn.\n\n" +
             "Bong bóng thật không đi thẳng: xoáy tách khỏi mặt sau làm chúng lượn zig-zag. " +
             "Đây là tham số 'thật' nhất của cả hệ thống.\nGợi ý: 3–6.")]
    public float wobbleFrequency = 4.5f;

    [Tooltip("Biên độ zig-zag = ĐƯỜNG KÍNH bong bóng × hệ số này. Vì tỉ lệ theo cỡ nên hạt to lượn rộng, " +
             "hạt nhỏ lượn hẹp — tự động đúng dù bạn đổi scale prefab.\n\n" +
             "0 = đi thẳng đứng (giả). 1.5–2.5 = lượn rõ và đẹp. Trên 4 = say sóng.")]
    public float wobbleAmplitudePerSize = 1.8f;

    [Tooltip("Bán kính đường XOẮN ỐC = đường kính bong bóng × hệ số này. " +
             "Kết hợp với zig-zag ở trên tạo quỹ đạo 3D không lặp lại.\n" +
             "Gợi ý: 0.5–1.5.")]
    public float spiralRadiusPerSize = 1.0f;

    [Tooltip("Tốc độ quay của đường xoắn (rad/giây, X = min, Y = max). Mỗi hạt random một giá trị và " +
             "random luôn chiều xoắn (thuận/nghịch kim đồng hồ).")]
    public Vector2 spiralSpeed = new Vector2(1.2f, 3.5f);

    [Tooltip("Độ dẹt/phình của màng bong bóng: phình ngang thì dẹt dọc (gần như giữ nguyên thể tích).\n\n" +
             "0 = hình tròn cứng đờ. 0.1–0.18 = rung nhẹ như màng nước thật. Trên 0.3 = biến dạng kiểu jelly.")]
    [Range(0f, 0.4f)] public float deform = 0.13f;

    [Tooltip("⭐ XOÁY QUANH TRỤC Y CHUNG (vortex): tưởng tượng 1 đường thẳng đâm lên mặt nước đúng tại " +
             "điểm bong bóng sinh ra — mỗi hạt vừa QUAY quanh trục đó (dùng chính spiralSpeed ở trên) " +
             "vừa CO DẦN bán kính quỹ đạo về 0 khi đã lên cao hơn, y hệt nước bị hút vào 1 cái phễu dựng " +
             "đứng. Giá trị này là tốc độ co bán kính mỗi giây MỖI ĐƠN VỊ ĐỘ CAO đã lên được — càng lớn " +
             "càng co nhanh khi vừa nhích lên; 0 = tắt hẳn, giữ nguyên hành vi cũ (không hội tụ về trục).\n" +
             "Gợi ý: 0.3–1.")]
    [Min(0f)] public float axisConvergeRate = 0.5f;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("⭐ DÒNG CHẢY & NHIỄU")]

    [Tooltip("Dòng hải lưu CHUNG áp lên mọi bong bóng (đơn vị/giây, theo world).\n\n" +
             "Một chút lệch ngang (0.03–0.1 trên trục X) làm cả cột bong bóng nghiêng đi — " +
             "khác hẳn cảm giác cột thẳng đứng nhân tạo. Để Vector3.zero nếu nước hoàn toàn tĩnh.")]
    public Vector3 current = new Vector3(0.05f, 0f, 0f);

    [Tooltip("Cường độ xoáy nhiễu Perlin riêng của từng hạt (đơn vị/giây). Khác với 'current' ở chỗ " +
             "mỗi hạt bị đẩy một hướng khác nhau và biến thiên theo thời gian → đám tản ra tự nhiên, " +
             "không đi song song như in.\nGợi ý: 0.2–0.5.")]
    [Min(0f)] public float turbulenceStrength = 0.3f;

    [Tooltip("Tốc độ BIẾN THIÊN của nhiễu Perlin theo thời gian.\n\n" +
             "Nhỏ (0.1–0.3) = nước lười, hạt trôi thành luồng chậm rãi.\n" +
             "Lớn (1–2) = nước động, hạt run rẩy liên tục (dễ thành nhiễu vụn nếu quá cao).")]
    [Min(0f)] public float turbulenceSpeed = 0.35f;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("⭐ MÀU")]

    [Tooltip("Màu nhân (tint) cho bong bóng NHỎ NHẤT. Ngả xanh hơn một chút giúp hạt li ti chìm vào nước.\n" +
             "Lưu ý: alpha của color này bị bỏ qua — alpha do alphaRange quyết định.")]
    [ColorUsage(false, true)] public Color tintSmall = new Color(0.80f, 0.93f, 1.00f);

    [Tooltip("Màu nhân (tint) cho bong bóng TO NHẤT. Trắng hơn vì màng dày bắt sáng nhiều hơn.\n" +
             "Các cỡ ở giữa được nội suy tuyến tính giữa tintSmall và tintBig.")]
    [ColorUsage(false, true)] public Color tintBig = new Color(0.95f, 0.99f, 1.00f);

    [Tooltip("Độ mờ theo cỡ (X = alpha của hạt nhỏ nhất, Y = alpha của hạt to nhất), có thêm ±20% ngẫu nhiên.\n\n" +
             "Cho X thấp (0.2–0.4) để hạt li ti chỉ thấp thoáng — đám sẽ có chiều sâu thay vì một mảng trắng đặc.")]
    public Vector2 alphaRange = new Vector2(0.35f, 0.85f);

    [Tooltip("Random Smoothness cho vật liệu — CHỈ có tác dụng khi prefab dùng mesh + URP/Lit. " +
             "Prefab SpriteRenderer bỏ qua hoàn toàn giá trị này.")]
    public Vector2 smoothnessRange = new Vector2(0.88f, 1f);

    [Tooltip("Thời gian (giây) alpha đi từ 0 lên mức đích lúc hạt mới sinh. " +
             "Giúp hạt 'hiện ra' thay vì bật vào khung hình.\nGợi ý: 0.1–0.25.")]
    [Min(0f)] public float fadeInTime = 0.18f;

    [Tooltip("⭐ Thời gian (giây) scale CO LẠI từ popInOvershoot về đúng 100%.\n\n" +
             "CỐ Ý co lại thay vì phồng lên từ 0: nếu phồng từ 0 thì hạt gần như VÔ HÌNH trong " +
             "suốt lúc đang bay ra (outwardVel), chỉ hiện rõ khi đã gần dừng hẳn → nhìn như " +
             "'đột nhiên xuất hiện tại chỗ' chứ không thấy được cú phọt/lặn xuống. Co từ to → " +
             "chuẩn thì hạt HIỆN RÕ NGAY khi vừa phọt ra khỏi tâm, thấy rõ cả quãng đường bay.\n" +
             "Gợi ý: 0.2–0.35.")]
    [Min(0.01f)] public float popInTime = 0.28f;

    [Tooltip("⭐ Scale BAN ĐẦU lúc vừa sinh, tính theo hệ số so với size cuối (1 = không phồng, " +
             "chỉ hiện thẳng ở size chuẩn). 1.4–1.8 = phồng vừa phải rồi co lại — cảm giác 'phọt' rõ " +
             "mà không cần overshoot kiểu OutBack.")]
    [Min(1f)] public float popInOvershoot = 1.6f;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("⭐ MẢNH VỠ KHI BONG BÓNG VỠ")]

    [Tooltip("Bật: bong bóng đủ to khi vỡ sẽ văng ra vài hạt li ti sống ngắn. " +
             "Chi tiết nhỏ nhưng làm khoảnh khắc vỡ đỡ 'biến mất đột ngột'.\n" +
             "Tắt để tiết kiệm hiệu năng trên máy yếu.")]
    public bool spawnPopShards = true;

    [Tooltip("Chỉ bong bóng có hệ số cỡ LỚN HƠN mức này mới văng mảnh. " +
             "Đặt quanh cận dưới của bigSizeMultiplierRange để chỉ những quả to mới có mảnh vỡ.")]
    public float shardMinParentMultiplier = 1.2f;

    [Tooltip("Số mảnh văng ra mỗi lần vỡ (X = min, Y = max, đã bao gồm cả hai đầu).\n" +
             "Các mảnh này cũng tính vào maxAlive.")]
    public Vector2Int shardCount = new Vector2Int(2, 5);

    // ═══════════════════════════════════════════════════════════════════════
    [Header("HIỆU NĂNG")]

    [Tooltip("Tự nới capacity của DOTween lúc Awake theo maxAlive (mỗi bong bóng dùng ~4 tween).\n\n" +
             "Nếu tắt, hãy tự tăng capacity trong DOTween Utility Panel — nếu không DOTween sẽ tự grow " +
             "và gây một nhịp cấp phát (GC spike) giữa lúc đang phun hạt.")]
    public bool autoSetTweenCapacity = true;

    // ── Runtime ──────────────────────────────────────────────────────────────
    readonly Stack<OceanBubble> _pool = new Stack<OceanBubble>();
    readonly List<OceanBubble> _alive = new List<OceanBubble>(256);

    Transform _poolRoot;
    bool _emitting;
    Vector3 _emitPoint, _prevEmitPoint;
    float _accumulator;
    float _diveFizzAccumulator;

    public int AliveCount => _alive.Count;
    public bool IsEmitting => _emitting;

    void Awake()
    {
        if (autoSetTweenCapacity)
            DOTween.SetTweensCapacity(Mathf.Max(1000, maxAlive * 4 + 200), 100);

        if (billboardCamera == null) billboardCamera = Camera.main;

        _poolRoot = new GameObject("[BubblePool]").transform;
        _poolRoot.SetParent(transform, false);
        _poolRoot.localScale = Vector3.one;   // pool root KHÔNG được scale, kẻo méo hạt

        if (bubblePrefab == null)
        {
            Debug.LogWarning("[OceanBubbleSystem] Chưa gán bubblePrefab — hệ thống sẽ không sinh gì.", this);
            return;
        }
        for (int i = 0; i < prewarmCount; i++) _pool.Push(CreateInstance());
    }

    // ── API cho TouchOceanManager ────────────────────────────────────────────

    /// <summary>Gọi mỗi frame khi đang giữ (truyền hold point), gọi null khi thả tay.</summary>
    public void SetEmitPoint(Vector3? point)
    {
        if (point.HasValue)
        {
            if (!_emitting)
            {
                _emitting = true;
                _emitPoint = _prevEmitPoint = point.Value;
                _accumulator = 0f;
                _diveFizzAccumulator = 0f;
                if (autoBurstOnStartStop) Burst(point.Value, pressBurst, 1f, 1.25f);
                if (spawnDiveFizzOnPress) SpawnDiveFizz(point.Value, diveFizzCount);
            }
            else
            {
                _prevEmitPoint = _emitPoint;
                _emitPoint = point.Value;
            }
        }
        else if (_emitting)
        {
            _emitting = false;
            if (autoBurstOnStartStop) Burst(_emitPoint, releaseBurst, 0.7f, 0.85f);
        }
    }

    /// <summary>Phụt một nhúm bong bóng tức thì tại một điểm.</summary>
    /// <param name="radiusMul">Nhân vào clusterRadius cho riêng cú burst này.</param>
    /// <param name="energyMul">Nhân vào lực bung ra — &gt;1 cho cú phụt mạnh hơn.</param>
    public void Burst(Vector3 center, int count, float radiusMul = 1f, float energyMul = 1f)
    {
        for (int i = 0; i < count; i++) SpawnOne(center, radiusMul, energyMul);
    }

    /// <summary>Cho mọi bong bóng đang sống vỡ ngay (đổi scene, reset...).</summary>
    public void PopAll()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
            if (_alive[i] != null) _alive[i].ForcePop();
    }

    // ── Vòng lặp ─────────────────────────────────────────────────────────────

    void Update()
    {
        float dt = Time.deltaTime;
        float time = Time.time;

        if (_emitting && bubblePrefab != null)
        {
            // Kéo càng nhanh → sủi càng nhiều (khuấy nước mạnh hơn)
            float dragSpeed = dt > 0f ? Vector3.Distance(_emitPoint, _prevEmitPoint) / dt : 0f;
            float k = Mathf.Clamp01(dragSpeed / Mathf.Max(dragSpeedForMaxRate, 0.01f));
            float rate = Mathf.Lerp(idleRate, dragRate, k);

            _accumulator += rate * dt;
            int n = Mathf.FloorToInt(_accumulator);
            _accumulator -= n;
            n = Mathf.Min(n, 60);   // trần mỗi frame, phòng khi dt nhảy cóc

            for (int i = 0; i < n; i++)
            {
                // Rải đều dọc đoạn con trỏ đi trong frame này → vệt liền mạch
                Vector3 c = Vector3.Lerp(_prevEmitPoint, _emitPoint, Random.value);
                SpawnOne(c, 1f, Mathf.Lerp(1f, 1.6f, k));
            }

            // Li ti LIÊN TỤC trong lúc giữ/kéo (không chỉ đợt phọt mở màn lúc press)
            if (diveFizzContinuous && diveFizzRatePerSecond > 0f)
            {
                _diveFizzAccumulator += diveFizzRatePerSecond * dt;
                int nf = Mathf.FloorToInt(_diveFizzAccumulator);
                _diveFizzAccumulator -= nf;
                nf = Mathf.Min(nf, 60);   // trần mỗi frame, phòng khi dt nhảy cóc

                for (int i = 0; i < nf; i++)
                {
                    Vector3 c = Vector3.Lerp(_prevEmitPoint, _emitPoint, Random.value);
                    SpawnOneDiveFizz(c);
                }
            }

            _prevEmitPoint = _emitPoint;
        }

        // Billboard: tính 1 lần cho cả đám
        if (billboard && billboardCamera == null) billboardCamera = Camera.main;
        bool doBillboard = billboard && billboardCamera != null;
        Quaternion billboardRot = doBillboard ? billboardCamera.transform.rotation : Quaternion.identity;

        // Tick tập trung — rẻ hơn nhiều so với hàng trăm Update() riêng lẻ
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            var b = _alive[i];
            if (b == null) { _alive.RemoveAt(i); continue; }
            b.Tick(dt, time, billboardRot, doBillboard);
        }
    }

    // ── Sinh hạt ─────────────────────────────────────────────────────────────

    void SpawnOne(Vector3 center, float radiusMul, float energyMul)
    {
        if (bubblePrefab == null || _alive.Count >= maxAlive) return;

        // r = R * u^bias — bias > 0.5 dồn hạt vào tâm → lõi dày đặc, rìa thưa
        float u = Random.value;
        float r = clusterRadius * radiusMul * Mathf.Pow(u, coreDensityBias);
        float a = Random.Range(0f, Mathf.PI * 2f);

        // "offset" giờ chỉ là ĐÍCH lan toả (khoảng cách hạt sẽ trôi tới nhờ outwardVel),
        // KHÔNG còn là vị trí sinh ra ngay — xem ⭐ PHỌT RA TỪ TÂM bên dưới.
        Vector3 offset = new Vector3(
            Mathf.Cos(a) * r,
            Mathf.Sin(a) * r * 0.75f + Random.Range(-spawnHeightJitter, spawnHeightJitter),
            Random.Range(-depthSpread, depthSpread));

        // ⭐ PHỌT RA TỪ TÂM: sinh NGAY tại điểm chạm rồi để outwardVel đẩy hạt trôi
        // ra tới "offset" ở trên — nhìn như một cú phọt thật từ đầu ngón tay, thay
        // vì hạt tự "mọc" sẵn ở một vị trí ngẫu nhiên rồi mới phồng lên (popInTime).
        Vector3 pos = center;

        // Phân bố cỡ lệch mạnh: random² kéo đa số hạt về cận dưới, thi thoảng 1 quả to
        float sizeMul = Random.value < bigBubbleChance
            ? Random.Range(bigSizeMultiplierRange.x, bigSizeMultiplierRange.y)
            : Mathf.Lerp(sizeMultiplierRange.x, sizeMultiplierRange.y, Random.value * Random.value);

        // Vận tốc lan toả NGANG phải đủ để hạt THỰC SỰ trôi từ tâm ra tới đúng bán kính r đã
        // random (v(t) tắt theo e^-damping·t → quãng đường tới hạn ≈ v0 / outwardDamping,
        // nên v0 = r * outwardDamping mới ra đúng tầm r). Hạt gần tâm (r nhỏ) gần như đứng
        // yên tại chỗ → lõi dày đặc; hạt xa (r lớn) trôi xa hơn → rìa thưa dần.
        Vector3 lateralDir = new Vector3(offset.x, 0f, offset.z);
        lateralDir = lateralDir.sqrMagnitude > 1e-6f ? lateralDir.normalized : Random.onUnitSphere;
        float travelSpeed = r * outwardDamping * outwardBurstSpeed;
        Vector3 lateralVel = lateralDir * travelSpeed * energyMul * Random.Range(0.85f, 1.15f);

        // ⭐ CÙNG LOGIC CHÌM NHƯ LI TI: vận tốc lặn XUỐNG riêng, ĐỘC LẬP với r (xem diveSpeed) —
        // khác với lateralVel ở trên (tỉ lệ theo r nên lõi dày đặc gần như không bung), mọi bong
        // bóng đều lặn mạnh như nhau lúc vừa phọt ra, y hệt cách SpawnOneDiveFizz làm cho li ti.
        Vector3 diveVel = Vector3.down * diveSpeed * energyMul * Random.Range(0.75f, 1.25f);

        Vector3 outward = lateralVel + diveVel;

        Get()?.Spawn(this, pos, outward, sizeMul);
    }

    /// <summary>Đợt bong bóng li ti bắn XUỐNG DƯỚI điểm chạm — xem DIVE FIZZ ở đầu file.</summary>
    void SpawnDiveFizz(Vector3 center, int count)
    {
        for (int i = 0; i < count; i++) SpawnOneDiveFizz(center);
    }

    /// <summary>
    /// Sinh MỘT hạt li ti quanh điểm chạm. ⭐ CỐ Ý KHÔNG kiểm tra maxAlive — lớp li ti
    /// spawn TỰ DO, không bị đám bong bóng thường (vốn dễ chạm trần maxAlive khi giữ
    /// lâu/kéo nhanh) chặn mất. Bù lại hạt li ti rất nhỏ + lifeMul ngắn nên không tốn
    /// nhiều tài nguyên render dù số lượng đông.
    /// </summary>
    void SpawnOneDiveFizz(Vector3 center)
    {
        if (bubblePrefab == null) return;

        // Rải quanh điểm chạm với bán kính RIÊNG (rộng hơn cluster để không bị bong bóng to che)
        float u = Random.value;
        float r = diveFizzRadius * Mathf.Pow(u, coreDensityBias);
        float a = Random.Range(0f, Mathf.PI * 2f);
        Vector3 offset = new Vector3(
            Mathf.Cos(a) * r,
            Mathf.Sin(a) * r * 0.75f,
            Random.Range(-depthSpread, depthSpread));
        Vector3 pos = center + offset;

        float sizeMul = Random.Range(diveFizzSizeRange.x, diveFizzSizeRange.y);

        // Bắn THẲNG XUỐNG (ngược hướng nổi) — Tick() sẽ tự tắt dần outwardVel
        // (outwardDamping) trong lúc _speed ramp lên dương, tạo hiệu ứng lặn
        // xuống một đoạn ngắn rồi mới trồi lên như bọt khí thật.
        Vector3 outward = Vector3.down * diveFizzSpeed * Random.Range(0.75f, 1.25f)
                         + new Vector3(Random.Range(-0.2f, 0.2f), 0f, 0f);

        Get()?.Spawn(this, pos, outward, sizeMul, diveFizzLifeMul, diveFizzDampingMul, diveFizzRiseSpeedMul);
    }

    /// <summary>Mảnh li ti văng ra khi một bong bóng lớn vỡ.</summary>
    public void EmitShards(Vector3 pos, float parentSizeMul, float parentWorldDiameter)
    {
        int n = Random.Range(shardCount.x, shardCount.y + 1);
        for (int i = 0; i < n; i++)
        {
            if (_alive.Count >= maxAlive) return;
            Vector3 p = pos + Random.insideUnitSphere * parentWorldDiameter * 0.6f;
            float sizeMul = parentSizeMul * Random.Range(0.12f, 0.3f);
            Vector3 outward = Random.onUnitSphere * parentWorldDiameter * 6f;
            Get()?.Spawn(this, p, outward, sizeMul, 0.45f);
        }
    }

    // ── Pool ─────────────────────────────────────────────────────────────────

    OceanBubble CreateInstance()
    {
        var b = Instantiate(bubblePrefab, _poolRoot);
        b.gameObject.SetActive(false);   // Awake đã chạy → _baseScale đã được chụp
        return b;
    }

    OceanBubble Get()
    {
        OceanBubble b = null;
        while (_pool.Count > 0 && b == null) b = _pool.Pop();
        if (b == null) b = CreateInstance();
        b.transform.SetParent(null, false);   // tách khỏi pool root, giữ nguyên local values
        _alive.Add(b);
        return b;
    }

    public void Release(OceanBubble b)
    {
        if (b == null) return;
        _alive.Remove(b);
        b.ResetForPool();
        if (b != null && b.transform != null) b.transform.SetParent(_poolRoot, false);
        _pool.Push(b);
    }

    /// <summary>Điểm world này có đang ở ngoài khung hình camera (cộng thêm biên offscreenMargin) không.</summary>
    public bool IsOffScreen(Vector3 worldPos)
    {
        var cam = billboardCamera != null ? billboardCamera : Camera.main;
        if (cam == null) return false;   // không có camera thì không tự cull, để lifetime lo hết

        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        if (vp.z < 0f) return true;   // ở sau lưng camera cũng coi như ngoài khung hình

        return vp.x < -offscreenMargin || vp.x > 1f + offscreenMargin
            || vp.y < -offscreenMargin || vp.y > 1f + offscreenMargin;
    }

    void OnDisable()
    {
        _emitting = false;
        for (int i = _alive.Count - 1; i >= 0; i--)
            if (_alive[i] != null) _alive[i].KillTweens();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 c = (Application.isPlaying && _emitting) ? _emitPoint : transform.position;
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.35f);
        Gizmos.DrawWireSphere(c, clusterRadius);                 // bán kính đám
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireSphere(c, clusterRadius * 0.35f);         // vùng lõi dày đặc
    }
#endif
}
