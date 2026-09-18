// Shader "Paro222/UnderwaterEffects"
// {
//     Properties
//     {
//         _MainTex ("Texture", 2D) = "white" {}
//     }
//     SubShader
//     {
//         Tags { "RenderType"="Opaque" }
//         LOD 100

//         Pass
//         {
//             CGPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag

//             #include "UnityCG.cginc"

//             struct appdata
//             {
//                 float4 vertex : POSITION;
//                 float2 uv : TEXCOORD0;
//             };

//             struct v2f
//             {
//                 float2 uv : TEXCOORD0;
//                 float4 vertex : SV_POSITION;
//             };

//             half remap(half x, half t1, half t2, half s1, half s2)
//             {
//                 return (x - t1) / (t2 - t1) * (s2 - s1) + s1;
//             }

//             sampler2D _MainTex;
//             sampler2D _NormalMap;
//             float4 _normalUV;
//             float4 _MainTex_ST;
//             fixed4 _color;
//             float _FogDensity;
//             float _alpha;
//             float _refraction;
//             sampler2D_float _CameraDepthTexture;

//             v2f vert (appdata v)
//             {
//                 v2f o;
//                 o.vertex = UnityObjectToClipPos(v.vertex);
//                 o.uv = TRANSFORM_TEX(v.uv, _MainTex);
//                 return o;
//             }

//             fixed4 frag (v2f i) : SV_Target
//             {
//                 // sample the texture
//                 fixed3 normalmap = UnpackNormal(tex2D(_NormalMap, i.uv * _normalUV.xy + _normalUV.zw * _Time.y));

//                 float depth_tex = UNITY_SAMPLE_DEPTH(tex2D(_CameraDepthTexture, i.uv + normalmap * _refraction * 0.01));
//                 float depth = Linear01Depth(depth_tex) * Linear01Depth(depth_tex);

//                 float fogAmount = 1.0 - exp(-_FogDensity * depth);
                
//                 fixed4 col = tex2D(_MainTex, i.uv + normalmap * _refraction * 0.01);

//                 return lerp(col, _color, saturate(fogAmount * 1000 + _alpha));
//             }
//             ENDCG
//         }
//     }
// }
Shader "Paro222/UnderwaterEffects"
{
    Properties
    {
        // URP Blitter API tự động truyền hình ảnh camera vào _BlitTexture
        [HideInInspector] _BlitTexture ("Blit Texture", 2D) = "white" {}
        
        // Khai báo các thuộc tính để hiển thị trên Material Inspector (nếu cần)
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _color ("Color", Color) = (0, 0.4, 0.7, 1)
        _FogDensity ("Fog Density", Range(0, 10)) = 1
        _alpha ("Alpha", Range(0, 1)) = 0
        _refraction ("Refraction", Float) = 0.1
        _normalUV ("Normal UV (XY) Speed (ZW)", Vector) = (1, 1, 0.2, 0.1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        
        // Tắt ZWrite và Cull cho hiệu ứng Fullscreen
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "UnderwaterPass"

            // Bắt đầu dùng HLSL thay vì CG
            HLSLPROGRAM
            #pragma vertex Vert // Vert được cung cấp sẵn bởi Blit.hlsl
            #pragma fragment frag

            // Các thư viện bắt buộc của URP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // Khai báo Texture chuẩn URP
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            // Các biến nhận từ script C#
            float4 _normalUV;
            float4 _color;
            float _FogDensity;
            float _alpha;
            float _refraction;

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // 1. Tính toán hiệu ứng sóng nước (Refraction) từ Normal Map
                float2 normalUV = uv * _normalUV.xy + _normalUV.zw * _Time.y;
                half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV);
                float3 normalmap = UnpackNormal(normalSample);
                
                float2 distortedUV = uv + normalmap.xy * _refraction * 0.01;

                // 2. Lấy độ sâu (Depth) chuẩn URP
                float rawDepth = SampleSceneDepth(distortedUV);
                // Chuyển đổi Depth về dạng tuyến tính (Linear 0-1)
                float linearDepth = Linear01Depth(rawDepth, _ZBufferParams);

                // 3. Tính toán độ dày của sương mù nước (Fog)
                float fogAmount = 1.0 - exp(-max(_FogDensity, 0.0) * linearDepth);
                
                // 4. Lấy hình ảnh game (đã bị làm méo bởi sóng nước)
                // _BlitTexture chứa hình ảnh từ Camera, sampler_LinearClamp là mặc định của URP
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distortedUV);

                // 5. Kết hợp hình ảnh game và màu nước
                return lerp(col, _color, saturate(fogAmount + _alpha));
            }
            ENDHLSL
        }
    }
}
