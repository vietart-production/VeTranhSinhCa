Shader "Custom/SwordfishWave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Song Chinh)]
        _WaveSpeed ("Tốc độ vẫy đuôi", Float) = 3.0
        _WaveAmplitude ("Biên độ uốn éo", Float) = 0.15
        _WaveFrequency ("Tần số sóng", Float) = 4.0

        [Header(Song Phu)]
        _Wave2Speed ("Tốc độ sóng phụ", Float) = 5.0
        _Wave2Amplitude ("Biên độ sóng phụ", Float) = 0.05
        _Wave2Frequency ("Tần số sóng phụ", Float) = 8.0

        [Header(Dieu Khien Dau Duoi)]
        _HeadStiffness ("Giữ cứng phần đầu (0-1)", Range(0, 1)) = 0.35
        _TailPower ("Độ mạnh đuôi (1=tuyến tính, 2=mạnh dần, 3=cực mạnh)", Range(1, 4)) = 2.5

        [Header(Song Z Luong Song Sau)]
        _ZWaveAmplitude ("Biên độ lượng sóng sâu (trục Z)", Float) = 0.2
        _YWaveAmplitude ("Biên độ dọc nhẹ (trục Y)", Float) = 0.0

        [Header(Huong Ca)]
        [Toggle] _FlipDirection ("Lật hướng cá (Đầu bên phải)", Float) = 0
    }

    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            fixed4 _Color;

            float _WaveSpeed;
            float _WaveAmplitude;
            float _WaveFrequency;

            float _Wave2Speed;
            float _Wave2Amplitude;
            float _Wave2Frequency;

            float _HeadStiffness;
            float _TailPower;
            float _ZWaveAmplitude;
            float _YWaveAmplitude;
            float _FlipDirection;

            v2f vert(appdata_t IN)
            {
                v2f OUT;

                // === TÍNH VỊ TRÍ TRÊN THÂN CÁ (0 = đầu, 1 = đuôi) ===
                // UV.x chạy từ 0 đến 1. Mặc định: 0 = bên trái (đầu cá kiếm), 1 = bên phải (đuôi)
                // Nếu cá hướng ngược (đầu bên phải), lật lại
                float bodyPos = _FlipDirection > 0.5 ? (1.0 - IN.texcoord.x) : IN.texcoord.x;

                // === HỆ SỐ UỐN: Đầu cứng đơ, đuôi uốn mạnh ===
                // Phần đầu (bodyPos < HeadStiffness) sẽ bị giữ cứng hoàn toàn (hệ số = 0)
                // Phần còn lại tăng dần theo lũy thừa (Power) để đuôi lắc mạnh nhất
                float flexRange = saturate((bodyPos - _HeadStiffness) / (1.0 - _HeadStiffness));
                float flexFactor = pow(flexRange, _TailPower);

                // === SÓNG CHÍNH: Chuyển động uốn lượn chủ đạo của thân cá ===
                // Cá kiếm bơi bằng cách uốn thân hình sin, sóng truyền từ đầu về đuôi
                float wave1 = sin(bodyPos * _WaveFrequency * 6.2832 - _Time.y * _WaveSpeed) * _WaveAmplitude;

                // === SÓNG PHỤ: Rung nhẹ tự nhiên ở đuôi (mô phỏng vây đuôi rung) ===
                float wave2 = sin(bodyPos * _Wave2Frequency * 6.2832 - _Time.y * _Wave2Speed) * _Wave2Amplitude;

                // === TỔNG HỢP SÓNG ===
                float totalWave = (wave1 + wave2) * flexFactor;

                // === TRÚC Z: SÓNG LƯỢNG SÓNG SÙ (ra vào so với camera) ===
                // Đây là hiệu ứng chính - đuôi cá uốn vào/ra khỏi màn hình
                // Camera orthographic nhìn thẳng vào Z nên sẽ thấy đuôi ngắn lại khi vào sâu
                // và dài ra khi nhô ra ngoài - giống hiệu ứng flag waving
                IN.vertex.z += totalWave * _ZWaveAmplitude;

                // === TRÚC Y: NHÁP NHÔ NHẸ (ức chế phụ giữ tự nhiên) ===
                // Giữ một chút dao động Y rất nhẹ để tạo cảm giác cá vẫng về
                IN.vertex.y += totalWave * _YWaveAmplitude;

                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            sampler2D _MainTex;

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
