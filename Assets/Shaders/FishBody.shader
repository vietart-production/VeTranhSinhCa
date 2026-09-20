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
        _BodyAxis("Body Axis (0 = truc ngang UV.x mac dinh, 1 = truc doc UV.y - danh cho texture da bi xoay 90 do qua textureRotationDegrees)", Float) = 0

        _DeformMode("Deform Mode (0 = song doc than tu dau den duoi, 1 = song toa tron tu tam ra bien, danh cho sao bien/cua khong co truc dau-duoi)", Float) = 0
        _ArmCount("Arm Count (chi dung khi Deform Mode = 1, so 'canh' toa quanh tam)", Float) = 5

        _FinBandCenter("Fin Band Center (0..1 doc than, vi tri vay nguc gan dau)", Range(0, 1)) = 0.18
        _FinBandWidth("Fin Band Width (do rong vung vay rung)", Range(0.02, 0.5)) = 0.12
        _FinWaveFrequency("Fin Wave Frequency", Float) = 6
        _FinWaveAmplitude("Fin Wave Amplitude (0 = tat, mac dinh khong anh huong loai chua bat)", Float) = 0

        _TwistTriggerTime("Twist Trigger Time (_Time.y luc kich hoat, -1000 = chua kich hoat)", Float) = -1000
        _TwistTravelDuration("Twist Travel Duration (thoi gian con xoan lan tu dau den duoi)", Float) = 0.5
        _TwistSpinDuration("Twist Spin Duration (thoi gian moi diem quay-quay roi tat han)", Float) = 0.35
        _TwistFlailCycles("Twist Flail Cycles (so nhip lac qua lai truoc khi tat)", Float) = 2.5
        _TwistMaxAngle("Twist Max Angle (goc lac toi da, do)", Range(5, 90)) = 35
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
            float _BodyAxis;
            float _DeformMode;
            float _ArmCount;
            float _FinBandCenter;
            float _FinBandWidth;
            float _FinWaveFrequency;
            float _FinWaveAmplitude;
            float _TwistTriggerTime;
            float _TwistTravelDuration;
            float _TwistSpinDuration;
            float _TwistFlailCycles;
            float _TwistMaxAngle;
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

            // Vay nguc: 1 vung nho gan dau dao dong nhe, doc lap voi vay duoi,
            // de phan than truoc khong con dung im hoan toan. Gaussian quanh
            // _FinBandCenter thay vi ramp, vi day la 1 diem khu tru chu khong
            // phai "tu day tro di". _FinWaveAmplitude mac dinh = 0 nen khong
            // anh huong loai nao chua duoc bat rieng.
            float finOffset = bodyPos01 - _FinBandCenter;
            float finBand = exp(-(finOffset * finOffset) / max(_FinBandWidth * _FinBandWidth, 0.0001));
            float finWave = sin(_Time.y * _FinWaveFrequency + _WavePhase * 1.7) * _FinWaveAmplitude * finBand;

            positionOS.y += wave + finWave;
            return positionOS;
        }

        // Danh cho sinh vat khong co truc dau-duoi (sao bien, cua): song toa
        // tu tam UV ra vien thay vi chay doc theo 1 truc. _ArmCount lam cac
        // "canh" quanh tam lech pha nhau theo goc, tao cam giac tung canh
        // rung doc lap thay vi ca khoi cung uon nhu 1 con ca.
        float3 ApplyRadialWave(float3 positionOS, float2 uv)
        {
            float2 fromCenter = uv - 0.5;
            float radius = saturate(length(fromCenter) * 1.41421356);
            float angle = atan2(fromCenter.y, fromCenter.x);
            float weight = smoothstep(_TailWaveStart, 1.0, radius);
            float wave = sin(_Time.y * _WaveFrequency + angle * _ArmCount + radius * _WaveLength + _WavePhase) * _WaveAmplitude * weight;
            float2 dir = radius > 1e-4 ? normalize(fromCenter) : float2(0.0, 0.0);
            positionOS.xy += dir * wave;
            return positionOS;
        }

        // Quay-quay (xoac) quanh truc doc than (truc X cuc bo): bat dau tu dau
        // mui (bodyPos01 = 0), lan dan ve duoi, moi diem lac qua lai vai nhip
        // (_TwistFlailCycles) voi bien do tat dan roi dung han tai nguyen trang
        // - giong dang quay minh gion gion cua ca that hon la 1 vong lon tron
        // trinh dien. Van la 1 su kien kich hoat rieng le (script set
        // _TwistTriggerTime 1 lan), khong tu lap lien tuc nhu wave; truoc khi
        // kich hoat lan dau, _TwistTriggerTime mac dinh rat am de elapsed luon
        // am va khong xoay gi ca.
        float3 ApplyBodyTwist(float3 positionOS, float bodyPos01)
        {
            float elapsed = _Time.y - _TwistTriggerTime;
            float localStart = bodyPos01 * _TwistTravelDuration;
            float localProgress = saturate((elapsed - localStart) / max(_TwistSpinDuration, 0.0001));
            float decay = 1.0 - localProgress;
            float angle = sin(localProgress * _TwistFlailCycles * 6.28318530718) *
                radians(_TwistMaxAngle) * decay;

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
            // Anh xoay 90 do qua textureRotationDegrees (sua/ca_muc/ca_ngua) khien
            // truc dau-duoi thuc te nam doc theo UV.y thay vi UV.x mac dinh.
            float axisCoord = lerp(IN.uv.x, IN.uv.y, _BodyAxis);
            float bodyPos01 = lerp(axisCoord, 1 - axisCoord, _TailSide);
            float3 posOS = _DeformMode > 0.5
                ? ApplyRadialWave(IN.positionOS.xyz, IN.uv)
                : ApplyTailWave(IN.positionOS.xyz, bodyPos01);
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
