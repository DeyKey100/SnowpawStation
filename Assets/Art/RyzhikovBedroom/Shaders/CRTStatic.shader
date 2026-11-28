Shader "Snowpaw/Bedroom CRT Static"
{
    Properties
    {
        // Pixel grid in columns/rows; use 96x72 or 128x96 for the CRT raster.
        _NoiseGrid ("Noise Grid (X/Y)", Vector) = (96, 72, 0, 0)
        _Rate ("Static Rate (FPS)", Range(1, 30)) = 14
        _Brightness ("Brightness", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "CRTStatic"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseGrid;
                float _Rate;
                float _Brightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // A compact, deterministic integer-style hash. The frame index is
            // quantized by _Rate, so the pattern changes at a discrete 12-16 fps.
            float Hash21(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 grid = max(_NoiseGrid.xy, float2(1.0, 1.0));
                float2 cell = floor(saturate(input.uv) * grid);
                float frame = floor(_Time.y * max(_Rate, 1.0));

                // Independent values keep most of the screen a quiet grey while
                // producing sparse, crisp black dots that re-randomize per frame.
                float noise = Hash21(float3(cell, frame));
                float grain = Hash21(float3(cell + 17.0, frame + 5.0));
                float dotMask = step(noise, 0.135);

                float grey = 0.80 + (grain - 0.5) * 0.10;
                grey = lerp(grey, 0.035, dotMask);

                // Subdued scanlines: a small row-period modulation, not a pulse.
                float scanline = 0.965 + 0.035 * sin(input.uv.y * grid.y * 3.14159265);
                grey *= scanline;

                // CRT edge falloff, intentionally restrained so the screen does
                // not become a strong full-frame flicker or bloom source.
                float2 centered = input.uv * 2.0 - 1.0;
                float edge = saturate(1.0 - dot(centered * 0.52, centered * 0.52));
                grey *= lerp(0.86, 1.0, edge);

                grey = saturate(grey * _Brightness);
                return half4(grey, grey, grey, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
