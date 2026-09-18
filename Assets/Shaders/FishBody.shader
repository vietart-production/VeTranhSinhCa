Shader "Custom/FishBody"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _AlphaClip("Alpha Clip", Float) = 1
        _EmissionMap("Emission Map", 2D) = "white" {}
        _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _SrcBlend("Src Blend", Float) = 1
        _DstBlend("Dst Blend", Float) = 0
        _SrcBlendAlpha("Src Blend Alpha", Float) = 1
        _DstBlendAlpha("Dst Blend Alpha", Float) = 0
        _ZWrite("ZWrite", Float) = 1

        _WaveAmplitude("Wave Amplitude", Float) = 0.3
        _WaveFrequency("Wave Frequency", Float) = 4
        _WaveLength("Wave Length", Float) = 2.5
        _TailSide("Tail Side (0 = UV.x 0 la duoi, 1 = UV.x 1 la duoi)", Float) = 1
        _WavePhase("Wave Phase (rieng tung ca, tranh dong bo gia tao)", Float) = 0
        _TailWaveStart("Tail Wave Start (0..1, truoc nguong nay gan nhu dung yen)", Range(0, 1)) = 0.72

        _TwistTriggerTime("Twist Trigger Time (_Time.y luc kich hoat, -1000 = chua kich hoat)", Float) = -1000
        _TwistTravelDuration("Twist Travel Duration (thoi gian con xoan lan tu dau den duoi)", Float) = 0.5
        _TwistSpinDuration("Twist Spin Duration (thoi gian moi diem tu xoay du 1 vong)", Float) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _Cutoff;
            float _AlphaClip;
            float4 _EmissionColor;
            float _WaveAmplitude;
            float _WaveFrequency;
            float _WaveLength;
            float _TailSide;
            float _WavePhase;
            float _TailWaveStart;
            float _TwistTriggerTime;
            float _TwistTravelDuration;
            float _TwistSpinDuration;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        // Chi doan ngan cuoi duoi vay lien tuc (tu _TailWaveStart den 1), phan
        // truoc gan nhu dung yen thay vi ramp tuyen tinh tu dau gay cam giac
        // quay ca nua than. _TailSide chon canh UV nao la duoi vi mesh dung
        // chung cho nhieu loai, khong the biet truoc huong texture.
        float3 ApplyTailWave(float3 positionOS, float bodyPos01)
        {
            float tailWeight = smoothstep(_TailWaveStart, 1.0, bodyPos01);
            float wave = sin(_Time.y * _WaveFrequency + bodyPos01 * _WaveLength + _WavePhase) * _WaveAmplitude * tailWeight;
            positionOS.y += wave;
            return positionOS;
        }

        // Xoay 360 do quanh truc doc than (truc X cuc bo): bat dau tu dau mui
        // (bodyPos01 = 0), lan dan ve duoi, moi diem tu xoay du 1 vong roi dung
        // (360 do = 0 do nen tu tro ve nguyen trang). Day la 1 su kien kich hoat
        // rieng le (script set _TwistTriggerTime 1 lan), khong tu lap lien tuc
        // nhu wave; truoc khi kich hoat lan dau, _TwistTriggerTime mac dinh rat
        // am de elapsed luon am va khong xoay gi ca.
        float3 ApplyBodyTwist(float3 positionOS, float bodyPos01)
        {
            float elapsed = _Time.y - _TwistTriggerTime;
            float localStart = bodyPos01 * _TwistTravelDuration;
            float localProgress = saturate((elapsed - localStart) / max(_TwistSpinDuration, 0.0001));
            float angle = localProgress * 6.28318530718;

            float s, c;
            sincos(angle, s, c);
            float y = positionOS.y * c - positionOS.z * s;
            float z = positionOS.y * s + positionOS.z * c;
            positionOS.y = y;
            positionOS.z = z;
            return positionOS;
        }

        Varyings FishVertex(Attributes IN)
        {
            Varyings OUT;
            float bodyPos01 = lerp(IN.uv.x, 1 - IN.uv.x, _TailSide);
            float3 posOS = ApplyTailWave(IN.positionOS.xyz, bodyPos01);
            posOS = ApplyBodyTwist(posOS, bodyPos01);
            OUT.positionCS = TransformObjectToHClip(posOS);
            OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
            return OUT;
        }

        half4 SampleClippedColor(Varyings IN)
        {
            half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
            if (_AlphaClip > 0.5)
                clip(baseColor.a - _Cutoff);
            return baseColor;
        }
        ENDHLSL

        // Ca la mieng phang mong da cat theo alpha; ve 2 mat de tranh rui ro
        // bi cull nham chieu khi mesh luoi tu sinh co thu tu winding khac Quad goc.
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex FishVertex
            #pragma fragment FishFragment

            half4 FishFragment(Varyings IN) : SV_Target
            {
                half4 baseColor = SampleClippedColor(IN);
                half4 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv) * _EmissionColor;
                return half4(baseColor.rgb + emission.rgb, baseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex FishVertex
            #pragma fragment FishFragmentDepth

            half4 FishFragmentDepth(Varyings IN) : SV_Target
            {
                SampleClippedColor(IN);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex FishVertex
            #pragma fragment FishFragmentDepth

            half4 FishFragmentDepth(Varyings IN) : SV_Target
            {
                SampleClippedColor(IN);
                return 0;
            }
            ENDHLSL
        }
    }
}
