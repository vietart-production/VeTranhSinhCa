Shader "Custom/SpriteWave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _WaveSpeed ("Tốc độ vẫy đuôi", Float) = 15.0
        _WaveAmplitude ("Độ uốn éo", Float) = 0.5
        _WaveFrequency ("Độ nhăn của sóng", Float) = 2.0
        _TailOffset ("Giữ yên đầu cá", Float) = 0.5
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
        Blend One OneMinusSrcAlpha // Premultiplied Alpha blend

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
                float2 texcoord  : TEXCOORD0;
            };

            fixed4 _Color;
            float _WaveSpeed;
            float _WaveAmplitude;
            float _WaveFrequency;
            float _TailOffset;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                
                // Hiệu ứng lượn sóng (SINE WAVE)
                // Dùng trục X của vertex kết hợp thời gian để tạo sóng
                float wave = sin(IN.vertex.x * _WaveFrequency + _Time.y * _WaveSpeed) * _WaveAmplitude;
                
                // Mẹo nhỏ: Dùng trục X để tính độ uốn. 
                // Thường cá bơi thì đầu ít lắc, đuôi lắc mạnh. 
                // Ta nhân độ lắc với vị trí X (Giả sử X càng về số âm (đuôi) thì sóng càng to)
                float tailFactor = IN.texcoord.x - _TailOffset; // Từ -0.5 (trái) đến 0.5 (phải)
                
                IN.vertex.y += wave * abs(tailFactor);

                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap (OUT.vertex);
                #endif

                return OUT;
            }

            sampler2D _MainTex;

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D (_MainTex, IN.texcoord) * IN.color;
                c.rgb *= c.a; // Premultiply alpha cho chuẩn Sprite mặc định của Unity
                return c;
            }
            ENDCG
        }
    }
}
