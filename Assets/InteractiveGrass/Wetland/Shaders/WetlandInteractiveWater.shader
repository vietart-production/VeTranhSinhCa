Shader "Interactive Grass/Wetland Glass Water"
{
    Properties
    {
        _BaseMap("Water Detail", 2D) = "white" {}
        _BaseColor("Water Tint", Color) = (0.08, 0.19, 0.13, 1)
        [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0, 1)) = 0.28
        [NoScaleOffset] _RoughnessMap("Roughness Map", 2D) = "white" {}
        _Smoothness("Smoothness", Range(0, 2)) = 1.25
        _TintStrength("Tint Strength", Range(0, 1)) = 0.32
        _SurfaceAlpha("Surface Alpha", Range(0, 1)) = 0.82
        _RefractionStrength("Glass Refraction", Range(0, 0.03)) = 0.006
        _HighlightStrength("Surface Highlight", Range(0, 2)) = 0.55
        _DetailStrength("Moving Detail", Range(0, 0.25)) = 0.055
        _Speed("Flow Speed", Vector) = (0.0035, 0.0018, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Name "GlassWater"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float4 screenPosition : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _NormalStrength;
                float _Smoothness;
                float _TintStrength;
                float _SurfaceAlpha;
                float _RefractionStrength;
                float _HighlightStrength;
                float _DetailStrength;
                float4 _Speed;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPosition = ComputeScreenPos(output.positionHCS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normals.normalWS;
                output.tangentWS = float4(normals.tangentWS, input.tangentOS.w);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uvA = input.uv + _Time.y * _Speed.xy;
                float2 uvB = input.uv * 1.43 - _Time.y * _Speed.yx * 0.73;

                float3 normalA = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvA));
                float3 normalB = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvB));
                float2 combinedNormal = (normalA.xy + normalB.xy * 0.62) * _NormalStrength;
                float3 normalTS = normalize(float3(combinedNormal, 1.0));

                float tangentSign = input.tangentWS.w * GetOddNegativeScale();
                float3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * tangentSign;
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);
                float3 finalNormalWS = normalize(TransformTangentToWorld(normalTS, tangentToWorld));

                float2 screenUV = input.screenPosition.xy / input.screenPosition.w;
                float2 refractedUV = saturate(screenUV + combinedNormal * _RefractionStrength);
                half3 sceneColor = SampleSceneColor(refractedUV);

                half detailValue = dot(
                    SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvA).rgb,
                    half3(0.2126, 0.7152, 0.0722)) - 0.5h;
                half3 tintedScene = lerp(sceneColor, sceneColor * _BaseColor.rgb * 1.7h, _TintStrength);
                tintedScene += detailValue * _DetailStrength;

                Light mainLight = GetMainLight();
                float3 viewDirection = normalize(GetCameraPositionWS() - input.positionWS);
                float3 halfDirection = normalize(mainLight.direction + viewDirection);
                float roughness = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, uvA).r;
                float smoothness = saturate((1.0 - roughness) * _Smoothness);
                float highlight = pow(saturate(dot(finalNormalWS, halfDirection)), exp2(7.0 * smoothness + 1.0));
                float grazing = pow(1.0 - saturate(dot(finalNormalWS, viewDirection)), 3.0);
                half3 surfaceLight = mainLight.color * highlight * smoothness * _HighlightStrength;
                surfaceLight += grazing * _BaseColor.rgb * 0.22;

                return half4(tintedScene + surfaceLight, _SurfaceAlpha);
            }
            ENDHLSL
        }
    }
}
