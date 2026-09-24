using UnityEngine;

/// <summary>
/// MỘT bong bóng (SpriteRenderer 2D — tự nhận diện, vẫn chạy được với mesh).
/// Không tự Update — được <see cref="OceanBubbleSystem"/> tick tập trung.
///
/// ⭐ SCALE: mọi thứ nhân theo localScale GỐC của prefab (chụp lại lúc Awake).
/// Random size chỉ là HỆ SỐ ±: prefab scale 0.3 với hệ số 1.4 → bong bóng scale 0.42.
///
/// ⭐ KHÔNG DÙNG DOTWEEN: tốc độ nổi, pop-in, nở dần theo tuổi, fade-in/fade-out đều là
/// lerp thủ công tính thẳng trong Tick() dựa trên _age/_popAge — không tạo Tween/Sequence
/// nào cả. Với maxAlive lớn (nhiều người chơi cùng lúc → nhiều bong bóng), mỗi Tween riêng
/// là một entry DOTween phải tự duyệt + gọi delegate mỗi frame; bỏ hẳn phần này rẻ hơn nhiều
/// so với để DOTween quản lý hàng nghìn tween cùng lúc, mà kết quả hình ảnh giống hệt (cùng
/// công thức easing Quad/Sine như DOTween dùng).
///
/// Các field bên dưới là GHI ĐÈ RIÊNG CHO PREFAB NÀY (đặt trên prefab, không phải
/// trên system). Mục đích: một OceanBubbleSystem có thể dùng nhiều prefab bong bóng
/// khác nhau (bong bóng trong, bong bóng có highlight, bong bóng méo...) mà mỗi loại
/// vẫn giữ được tính cách riêng. Để tất cả = 1 nếu chỉ dùng một prefab duy nhất.
/// </summary>
[DisallowMultipleComponent]
public class OceanBubble : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");   // URP/Lit
    static readonly int ColorId = Shader.PropertyToID("_Color");       // shader sprite
    static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    // ═══════════════════════════════════════════════════════════════════════
    [Header("⭐ GHI ĐÈ RIÊNG CHO PREFAB NÀY (để 1 = dùng nguyên giá trị của System)")]

    [Tooltip("Nhân thêm vào hệ số cỡ mà System cấp cho hạt này.\n\n" +
             "Dùng khi một System phun nhiều prefab khác nhau: ví dụ prefab 'bong bóng nhỏ li ti' " +
             "để 0.6, prefab 'bong bóng lớn có highlight' để 1.8 — cả hai vẫn dùng chung dải random " +
             "của System nhưng ra hai lớp cỡ khác hẳn nhau.\n\n" +
             "Lưu ý: giá trị này ảnh hưởng cả tốc độ nổi và biên độ lượn (vì hai thứ đó tính theo cỡ thật).")]
    [Min(0.01f)] public float sizeWeight = 1f;

    [Tooltip("Nhân vào tốc độ nổi. <1 = loại bong bóng này nặng/ì hơn, >1 = nhẹ và vọt lên nhanh hơn.\n\n" +
             "Hữu ích khi sprite có hình dạng không tròn (bong bóng dẹt nổi chậm hơn trong thực tế).")]
    [Min(0f)] public float riseSpeedWeight = 1f;

    [Tooltip("Nhân vào độ mờ đích. <1 cho sprite vốn đã đậm màu, >1 cho sprite mảnh và nhạt.\n" +
             "Kết quả cuối vẫn bị kẹp trong khoảng 0–1.")]
    [Min(0f)] public float alphaWeight = 1f;

    [Tooltip("Nhân vào biên độ zig-zag và bán kính xoắn. 0 = hạt này đi thẳng đứng hoàn toàn.\n\n" +
             "Đặt thấp (0.3) cho bong bóng rất to (thực tế chúng lượn ít hơn theo tỉ lệ), " +
             "hoặc cho lớp bong bóng nền ở xa để đỡ rối mắt.")]
    [Min(0f)] public float wobbleWeight = 1f;

    [Tooltip("Nhân vào tuổi thọ do System cấp. <1 = loại bong bóng sống ngắn (bọt tan nhanh), " +
             ">1 = sống lâu, đi được quãng đường xa hơn trước khi vỡ.")]
    [Min(0.01f)] public float lifetimeWeight = 1f;

    [Tooltip("Buộc ghi màu qua MaterialPropertyBlock thay vì SpriteRenderer.color.\n\n" +
             "Chỉ bật khi sprite dùng shader tuỳ biến đọc _BaseColor/_Color và KHÔNG đọc vertex color " +
             "(khi đó SpriteRenderer.color sẽ không có tác dụng và bong bóng luôn hiện đục).\n" +
             "Đánh đổi: MPB làm mất SRP Batcher cho nhóm sprite này.")]
    public bool forceMaterialPropertyBlock = false;

    [Tooltip("Nếu có phần tử: mỗi lần Spawn sẽ random 1 sprite trong mảng này cho SpriteRenderer, " +
             "thay vì luôn dùng sprite gốc gán sẵn trên prefab — 1 prefab vẫn ra nhiều hình dạng bong bóng khác nhau.\n\n" +
             "Để trống = giữ nguyên sprite gốc trên SpriteRenderer.")]
    public Sprite[] spriteVariants;

    // ── Tham chiếu (cache lúc Awake) ─────────────────────────────────────────
    OceanBubbleSystem _sys;
    Transform _t;
    Renderer _renderer;
    SpriteRenderer _sprite;
    MaterialPropertyBlock _mpb;

    // ⭐ Scale gốc của prefab + đường kính world khi localScale = 1
    Vector3 _baseScale = Vector3.one;   // chụp từ prefab, KHÔNG bao giờ bị ghi đè
    float _unitDiameter = 1f;           // đo từ sprite.bounds (đã tính Pixels Per Unit)
    int _baseSortingOrder;

    // ── Trạng thái chuyển động ───────────────────────────────────────────────
    Vector3 _column;       // vị trí "cột nước": chỉ đi lên + dòng chảy + nhiễu, KHÔNG gồm lượn
    Vector3 _outwardVel;   // vận tốc bung ra lúc mới sinh, tắt dần theo hàm mũ (cản nước)
    float _outwardDampingMul = 1f;   // nhân thêm vào outwardDamping — xem Spawn()
    Vector2 _axisXZ;       // trục Y "đâm lên mặt nước" tại đúng điểm sinh — xem ApplyAxisVortex()
    float _spawnY;          // cao độ lúc sinh, để tính đã lên được bao xa (deltaY)
    Vector3 _zigAxis;      // trục zig-zag ngẫu nhiên của riêng hạt này
    float _speed;          // tốc độ nổi hiện tại — lerp thủ công 0 → _terminalSpeed trong Tick()
    float _terminalSpeed;  // tốc độ nổi tới hạn (đích của lerp _speed) — random theo cỡ lúc Spawn
    float _age, _life;

    // ── Trạng thái hình dạng (compose thành localScale mỗi Tick) ─────────────
    float _sizeMul;        // hệ số cỡ cuối cùng = System cấp × sizeWeight
    float _worldDiameter;  // đường kính thật (world units) — dùng cho biên độ lượn
    float _spawnScale;     // popInOvershoot → 1, co lại lúc sinh (KHÔNG phồng từ 0)
    float _growth = 1f;    // 1 → growthOverLife, nở dần khi lên cao
    float _popScale = 1f;  // 1 → 0, co thẳng lúc vỡ (không phồng lên trước)
    float _phase, _wobbleFreq, _wobbleAmp, _spiralR, _spiralSpeed, _deformAmp;
    float _noiseSeed;

    // ── Trạng thái màu ───────────────────────────────────────────────────────
    float _alpha, _appliedAlpha = -1f, _smoothness;
    float _targetAlpha;    // đích fade-in (lúc _popping thì lerp nốt phần fade-out riêng)
    Color _tint;

    // ── Trạng thái vỡ (pop) — lerp thủ công rieng, thay cho DOTween Sequence ──
    const float PopDuration = 0.16f;
    bool _popping;
    float _popAge;
    float _popStartScale;
    float _popStartAlpha;

    public bool IsPopping => _popping;
    public float SizeMultiplier => _sizeMul;
    public float WorldDiameter => _worldDiameter;

    void Awake()
    {
        _t = transform;
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _renderer = _sprite != null ? (Renderer)_sprite : GetComponentInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();

        // ⭐ Chụp scale gốc TRƯỚC khi có bất kỳ lệnh ghi scale nào
        _baseScale = _t.localScale;
        if (_baseScale.sqrMagnitude < 1e-8f) _baseScale = Vector3.one;

        // Đường kính world khi localScale = 1
        if (_sprite != null && _sprite.sprite != null)
        {
            _unitDiameter = Mathf.Max(_sprite.sprite.bounds.size.x, 1e-4f);
            _baseSortingOrder = _sprite.sortingOrder;
        }
        else
        {
            var mf = GetComponentInChildren<MeshFilter>();
            _unitDiameter = (mf != null && mf.sharedMesh != null)
                ? Mathf.Max(mf.sharedMesh.bounds.size.x, 1e-4f)
                : 1f;
        }
    }

    // ── Easing thủ công (cùng công thức DOTween dùng cho Quad/Sine) ──────────
    static float EaseOutQuad(float t) => t * (2f - t);
    static float EaseInQuad(float t) => t * t;
    static float EaseOutSine(float t) => Mathf.Sin(t * Mathf.PI * 0.5f);
    static float EaseInSine(float t) => 1f - Mathf.Cos(t * Mathf.PI * 0.5f);

    // ── Sinh ra ──────────────────────────────────────────────────────────────

    /// <param name="sizeMul">Hệ số nhân với localScale gốc của prefab (KHÔNG phải mét).</param>
    /// <param name="lifeMul">Nhân thêm vào tuổi thọ — System dùng &lt;1 cho mảnh vỡ.</param>
    /// <param name="outwardDampingMul">Nhân thêm vào outwardDamping CHỈ CHO HẠT NÀY — dùng khi 1 loại
    /// hạt (ví dụ li ti, sống rất ngắn) cần cú lặn/bung tắt NHANH hơn để kịp lộ ra tốc độ nổi riêng
    /// theo cỡ (riseSizeExponent) trước khi hết đời, thay vì bị outwardVel chi phối suốt vòng đời.</param>
    /// <param name="riseSpeedMul">Nhân thêm vào riseSpeed CHỈ CHO HẠT NÀY — cho 1 loại hạt (ví dụ li ti)
    /// vận tốc nổi RIÊNG, tách khỏi riseSpeed dùng chung cho bong bóng thường.</param>
    public void Spawn(OceanBubbleSystem sys, Vector3 pos, Vector3 outwardVel, float sizeMul,
                       float lifeMul = 1f, float outwardDampingMul = 1f, float riseSpeedMul = 1f)
    {
        _sys = sys;
        _outwardDampingMul = Mathf.Max(outwardDampingMul, 0f);

        // Random sprite mỗi lần sinh (nếu prefab có khai spriteVariants) → 1 prefab vẫn
        // cho nhiều hình dạng bong bóng. Đường kính unit phải đo lại theo sprite mới chọn.
        if (_sprite != null && spriteVariants != null && spriteVariants.Length > 0)
        {
            _sprite.sprite = spriteVariants[Random.Range(0, spriteVariants.Length)];
            if (_sprite.sprite != null)
                _unitDiameter = Mathf.Max(_sprite.sprite.bounds.size.x, 1e-4f);
        }

        _sizeMul = Mathf.Max(sizeMul * sizeWeight, 1e-3f);
        _worldDiameter = _unitDiameter * Mathf.Abs(_baseScale.x) * _sizeMul;
        _column = pos;
        _outwardVel = outwardVel;
        _age = 0f;
        _popping = false;
        _popAge = 0f;

        // ⭐ Trục xoáy nước: 1 đường thẳng đứng "đâm lên mặt nước" đúng tại điểm sinh —
        // xem ApplyAxisVortex() trong Tick().
        _axisXZ = new Vector2(pos.x, pos.z);
        _spawnY = pos.y;

        // Chuẩn hoá cỡ 0..1 → nội suy màu / alpha
        float sizeT = Mathf.InverseLerp(sys.sizeMultiplierRange.x, sys.bigSizeMultiplierRange.y, _sizeMul);

        // Terminal velocity ~ (hệ số cỡ)^exponent — to nổi nhanh, li ti lờ đờ.
        // riseSpeedMul cho phép 1 loại hạt (li ti) có vận tốc nổi RIÊNG, tách khỏi riseSpeed dùng
        // chung — vì li ti cỡ đã rất nhỏ, chỉ mỗi riseSizeExponent không đủ để tách biệt hẳn cảm giác.
        _terminalSpeed = sys.riseSpeed * riseSpeedWeight * riseSpeedMul
                       * Mathf.Pow(_sizeMul / Mathf.Max(sys.referenceMultiplier, 1e-3f), sys.riseSizeExponent)
                       * Random.Range(0.85f, 1.15f);

        _life = Random.Range(sys.lifetime.x, sys.lifetime.y) * lifeMul * lifetimeWeight;

        // Nhỏ → lắc nhanh, xoắn hẹp. To → lượn chậm, xoắn rộng.
        float invSize = Mathf.Sqrt(Mathf.Max(sys.referenceMultiplier, 1e-3f) / _sizeMul);
        _phase = Random.Range(0f, Mathf.PI * 2f);
        _wobbleFreq = sys.wobbleFrequency * Mathf.Clamp(invSize, 0.5f, 2.5f) * Random.Range(0.8f, 1.25f);
        _wobbleAmp = _worldDiameter * sys.wobbleAmplitudePerSize * wobbleWeight * Random.Range(0.6f, 1.4f);
        _spiralR = _worldDiameter * sys.spiralRadiusPerSize * wobbleWeight * Random.Range(0.5f, 1.5f);
        _spiralSpeed = Random.Range(sys.spiralSpeed.x, sys.spiralSpeed.y) * (Random.value < 0.5f ? -1f : 1f);
        _deformAmp = sys.deform * Random.Range(0.5f, 1.3f);
        _noiseSeed = Random.Range(0f, 999f);

        float a = Random.Range(0f, Mathf.PI * 2f);
        _zigAxis = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));

        _tint = Color.Lerp(sys.tintSmall, sys.tintBig, sizeT);
        _smoothness = Random.Range(sys.smoothnessRange.x, sys.smoothnessRange.y);
        _targetAlpha = Mathf.Clamp01(
            Mathf.Lerp(sys.alphaRange.x, sys.alphaRange.y, sizeT) * alphaWeight * Random.Range(0.8f, 1.2f));

        // Sorting ngẫu nhiên → đám sprite chồng nhau có chiều sâu, không bẹt như decal
        if (_sprite != null && sys.sortingOrderJitter > 0)
            _sprite.sortingOrder = _baseSortingOrder + Random.Range(-sys.sortingOrderJitter, sys.sortingOrderJitter + 1);

        // ⭐ Bắt đầu TO hơn size cuối (popInOvershoot) rồi CO LẠI, KHÔNG phồng từ 0 lên —
        // phồng từ 0 làm hạt gần như vô hình suốt lúc đang bay ra do outwardVel, chỉ hiện
        // rõ khi đã gần dừng hẳn (nhìn như "đột nhiên xuất hiện tại chỗ" thay vì phọt ra
        // và lặn xuống). Co từ to thì hạt HIỆN RÕ NGAY khi vừa phọt khỏi tâm.
        // ⭐ CÙNG LÝ như trên: fade alpha từ 0 lên (fadeInTime ~0.18s) trùng GẦN NHƯ CHÍNH
        // XÁC lúc bọt đang lặn xuống (outwardVel thắng _speed chỉ trong ~0.15-0.2s đầu) →
        // bọt gần như trong suốt suốt đúng lúc đang lặn, mắt không thấy được cú lặn dù toạ
        // độ đã di chuyển đúng. Bắt đầu ở mức ĐÃ THẤY RÕ (55% target) rồi mới fade nốt phần
        // còn lại, thay vì fade từ vô hình. _speed/_spawnScale/_growth/_alpha đều được lerp
        // thủ công moi frame trong Tick() dua theo _age, khong con tao Tween nao ca.
        _speed = 0f;
        _spawnScale = sys.popInOvershoot;
        _growth = 1f;
        _popScale = 1f;
        _alpha = _targetAlpha * 0.55f;
        _appliedAlpha = -1f;

        _t.position = _column;
        _t.localScale = Vector3.zero;
        gameObject.SetActive(true);
        ApplyColor();
    }

    // ── Tick (gọi từ OceanBubbleSystem.Update) ───────────────────────────────

    /// <param name="billboardRot">Rotation của camera, System tính sẵn 1 lần cho cả đám.</param>
    public void Tick(float dt, float time, Quaternion billboardRot, bool billboard)
    {
        if (_sys == null) return;
        _age += dt;

        // Cột nước: nổi + dòng chảy + xoáy nhiễu + đà bung ra lúc sinh
        Vector3 vel = Vector3.up * _speed + _sys.current + Turbulence(time) + _outwardVel;
        _column += vel * dt;
        _outwardVel *= Mathf.Exp(-_sys.outwardDamping * _outwardDampingMul * dt);   // cản nước

        ApplyAxisVortex(dt);

        // Xoắn ốc + zig-zag quanh cột nước, biên độ giảm dần theo tuổi
        float damp = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(_age / Mathf.Max(_life, 0.01f)));
        float ang = _phase + _age * _spiralSpeed;
        Vector3 lateral = (new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * _spiralR
                        + _zigAxis * Mathf.Sin(_age * _wobbleFreq + _phase) * _wobbleAmp) * damp;

        _t.position = _column + lateral;

        // ⭐ Billboard: sprite luôn hướng thẳng camera — bong bóng tròn nên xoay quanh
        // trục nhìn cũng không nhận ra được, bỏ hẳn roll cho đỡ tốn 1 phép tính/hạt.
        _t.rotation = billboard ? billboardRot : Quaternion.identity;

        if (_popping)
        {
            // ⭐ Vỡ: lerp thủ công popScale/alpha về 0 trong PopDuration, tu giai phong ve
            // pool khi xong — thay cho DOTween.Sequence + OnComplete truoc day.
            _popAge += dt;
            float popT = Mathf.Clamp01(_popAge / PopDuration);
            _popScale = Mathf.Lerp(_popStartScale, 0.05f, EaseInSine(popT));
            _alpha = Mathf.Lerp(_popStartAlpha, 0f, EaseInQuad(popT));
        }
        else
        {
            // Tốc độ nổi: quán tính nước, ramp 0 → _terminalSpeed trong riseAccelTime
            float speedT = Mathf.Clamp01(_age / Mathf.Max(_sys.riseAccelTime, 0.0001f));
            _speed = Mathf.Lerp(0f, _terminalSpeed, EaseOutQuad(speedT));

            // Pop-in: co lại từ popInOvershoot về 100% trong popInTime
            float popInT = Mathf.Clamp01(_age / Mathf.Max(_sys.popInTime, 0.0001f));
            _spawnScale = Mathf.Lerp(_sys.popInOvershoot, 1f, EaseOutQuad(popInT));

            // Nở dần khi lên cao (áp suất giảm), suốt cả vòng đời
            float growT = Mathf.Clamp01(_age / Mathf.Max(_life, 0.0001f));
            _growth = Mathf.Lerp(1f, _sys.growthOverLife, EaseInQuad(growT));

            // Fade-in alpha
            float fadeT = Mathf.Clamp01(_age / Mathf.Max(_sys.fadeInTime, 0.0001f));
            _alpha = Mathf.Lerp(_targetAlpha * 0.55f, _targetAlpha, EaseOutSine(fadeT));
        }

        // ⭐ Scale = scale GỐC prefab × hệ số random × các lerp; squash giữ "thể tích"
        float squash = _popping ? 1f : 1f + Mathf.Sin(_age * _wobbleFreq * 1.7f + _phase) * _deformAmp;
        float s = _sizeMul * _growth * _spawnScale * _popScale;
        float inv = 1f / Mathf.Max(squash, 0.05f);
        _t.localScale = new Vector3(_baseScale.x * s * squash,
                                    _baseScale.y * s * inv,
                                    _baseScale.z * s);

        if (Mathf.Abs(_alpha - _appliedAlpha) > 0.002f) ApplyColor();

        if (_popping)
        {
            if (_popAge >= PopDuration) _sys.Release(this);
            return;
        }

        // ⭐ Bay ra HẲN NGOÀI khung hình camera → thu hồi thẳng về pool, KHÔNG chạy
        // animation vỡ — lúc này không còn ai nhìn thấy nên tốn công co/phồng là vô ích.
        if (_sys.cullWhenOffscreen && _sys.IsOffScreen(_t.position))
        {
            _sys.Release(this);
            return;
        }

        if (_age >= _life) Pop();
    }

    /// <summary>
    /// ⭐ XOÁY NƯỚC QUANH TRỤC Y: tưởng tượng 1 đường thẳng đâm thẳng lên mặt nước tại đúng
    /// điểm bong bóng được sinh ra (_axisXZ). Mỗi bong bóng vừa QUAY quanh trục đó (vận tốc
    /// góc = _spiralSpeed, có sẵn mỗi hạt 1 giá trị + chiều random) vừa CO DẦN bán kính quỹ
    /// đạo về 0 khi đã lên cao hơn — giống nước bị hút xoáy vào 1 cái phễu dựng đứng.
    /// axisConvergeRate càng lớn thì co càng nhanh theo độ cao (deltaY) đã lên được.
    /// </summary>
    void ApplyAxisVortex(float dt)
    {
        if (_sys.axisConvergeRate <= 0f) return;

        Vector2 toAxis = new Vector2(_column.x - _axisXZ.x, _column.z - _axisXZ.y);

        // Quay quanh trục trong dt này (vận tốc góc riêng của từng hạt)
        float ang = _spiralSpeed * dt;
        float cosA = Mathf.Cos(ang), sinA = Mathf.Sin(ang);
        Vector2 rotated = new Vector2(toAxis.x * cosA - toAxis.y * sinA,
                                       toAxis.x * sinA + toAxis.y * cosA);

        // Co bán kính về 0 — càng lên cao (deltaY lớn) thì hệ số co mỗi giây càng mạnh,
        // đúng ý "càng lên cao càng tiến gần trục hơn".
        float deltaY = Mathf.Max(_column.y - _spawnY, 0f);
        float rate = _sys.axisConvergeRate * (1f + deltaY);
        float pull = 1f - Mathf.Exp(-rate * dt);
        Vector2 converged = Vector2.Lerp(rotated, Vector2.zero, pull);

        _column.x = _axisXZ.x + converged.x;
        _column.z = _axisXZ.y + converged.y;
    }

    Vector3 Turbulence(float time)
    {
        float ts = _sys.turbulenceSpeed;
        float nx = Mathf.PerlinNoise(_noiseSeed, time * ts) - 0.5f;
        float nz = Mathf.PerlinNoise(_noiseSeed + 37.1f, time * ts) - 0.5f;
        float ny = Mathf.PerlinNoise(_noiseSeed + 71.3f, time * ts) - 0.5f;
        return new Vector3(nx, ny * 0.35f, nz) * (_sys.turbulenceStrength * 2f);
    }

    // ── Vỡ ───────────────────────────────────────────────────────────────────

    /// <summary>Cho hạt vỡ ngay lập tức (System gọi khi PopAll).</summary>
    public void ForcePop() { if (!_popping) Pop(); }

    /// <summary>
    /// Vỡ khi HẾT LIFETIME mà chưa kịp trôi ra khỏi khung hình (bọt tan trong nước sâu).
    /// Bay ra khỏi khung hình bình thường thì KHÔNG đi qua đường này — xem Tick(), lúc đó
    /// thu hồi thẳng về pool luôn vì không còn ai nhìn thấy để cần animation.
    /// </summary>
    void Pop()
    {
        _popping = true;
        _popAge = 0f;
        _popStartScale = _popScale;
        _popStartAlpha = _alpha;

        if (_sys.spawnPopShards && _sizeMul >= _sys.shardMinParentMultiplier)
            _sys.EmitShards(_t.position, _sizeMul, _worldDiameter);
    }

    // ── Màu ──────────────────────────────────────────────────────────────────

    void ApplyColor()
    {
        _appliedAlpha = _alpha;
        var c = new Color(_tint.r, _tint.g, _tint.b, _alpha);

        // SpriteRenderer.color là đường nhanh nhất và không phá batching của sprite
        if (_sprite != null && !forceMaterialPropertyBlock) { _sprite.color = c; return; }

        if (_renderer == null) return;
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColorId, c);   // URP/Lit
        _mpb.SetColor(ColorId, c);       // shader sprite — set thừa cũng vô hại
        _mpb.SetFloat(SmoothnessId, _smoothness);
        _renderer.SetPropertyBlock(_mpb);
    }

    // ── Tiện ích ─────────────────────────────────────────────────────────────

    /// <summary>Đưa hạt về trạng thái sạch để nằm chờ trong pool.</summary>
    public void ResetForPool()
    {
        _popping = false;
        _popAge = 0f;
        _alpha = 0f; _appliedAlpha = -1f;
        _spawnScale = 0f; _popScale = 1f; _growth = 1f;
        _t.localScale = Vector3.zero;
        if (_sprite != null) _sprite.sortingOrder = _baseSortingOrder;
        gameObject.SetActive(false);
    }
}
