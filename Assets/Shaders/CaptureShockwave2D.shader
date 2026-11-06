// Pulse Gambit — capture shockwave, URP 2D Renderer version.
// Same look as CaptureShockwave.shader, but reads the 2D Renderer's
// Camera Sorting Layer Texture instead of the 3D Opaque Texture.
//
// Requirements: on your 2D Renderer Data asset, set
//   Camera Sorting Layer Texture > Foremost Sorting Layer = the layer your board/pieces are on
// and make sure the shockwave renders AFTER that layer (see CapturePulse sorting fields).
Shader "PulseGambit/CaptureShockwave2D"
{
    Properties
    {
        _Radius       ("Radius (0-1 of quad)", Range(0, 1.2)) = 0
        _Width        ("Ring Width", Range(0.01, 0.5)) = 0.14
        _Distortion   ("Distortion", Range(0, 0.1)) = 0.03
        _BlurStrength ("Radial Blur", Range(0, 0.1)) = 0.03
        _WakeAmount   ("Wake (blur behind ring)", Range(0, 1)) = 0.6
        _Fade         ("Fade", Range(0, 1)) = 1
        [HDR] _GlowColor ("Glow Color", Color) = (1, 0.45, 0.2, 1)
        _Glow         ("Glow", Range(0, 4)) = 0.8
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            // No LightMode tag = SRPDefaultUnlit, which the 2D Renderer draws.
            Name "Shockwave2D"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);

            CBUFFER_START(UnityPerMaterial)
                float  _Radius;
                float  _Width;
                float  _Distortion;
                float  _BlurStrength;
                float  _WakeAmount;
                float  _Fade;
                float4 _GlowColor;
                float  _Glow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS      : SV_POSITION;
                float2 uv              : TEXCOORD0;
                float4 screenPos       : TEXCOORD1;
                float4 centerScreenPos : TEXCOORD2;
            };

            half3 SampleScreen(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, uv).rgb;
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.centerScreenPos = ComputeScreenPos(TransformObjectToHClip(float3(0, 0, 0)));
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float2 cuv = i.centerScreenPos.xy / i.centerScreenPos.w;

                float d = length(i.uv - 0.5) * 2.0;

                float ring = saturate(1.0 - abs(d - _Radius) / _Width);
                ring = ring * ring * (3.0 - 2.0 * ring);

                float behind = step(d, _Radius);
                float wake = saturate(d / max(_Radius, 1e-4)) * behind;
                wake = wake * wake * _WakeAmount;

                float edge = saturate((1.0 - d) * 5.0);
                float k = _Fade * edge;

                float2 dir = suv - cuv;
                dir /= max(length(dir), 1e-5);

                float2 offset  = dir * (_Distortion * ring * k);
                float  blurAmt = _BlurStrength * (ring + wake) * k;

                const int TAPS = 10;
                half3 col = 0;
                [unroll]
                for (int t = 0; t < TAPS; t++)
                {
                    float s = t / (float)(TAPS - 1);
                    col += SampleScreen(suv + offset - dir * blurAmt * s);
                }
                col /= TAPS;

                col += _GlowColor.rgb * (ring * _Glow * k);

                float alpha = saturate((ring + wake) * 3.0) * k;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
