Shader "Snowpaw/PSX Lit Vertex Snap"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _Metallic ("Metallic", Range(0, 1)) = 0
        _MetallicGlossMap ("Metallic Map", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap ("Emission Map", 2D) = "black" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _AlphaClip ("Alpha Clip", Float) = 0
        _Surface ("Surface", Float) = 0
        _Cull ("Cull", Float) = 2
        _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        _SnapRows ("Vertex Snap (rows on screen, 0 = off)", Float) = 400
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }
        Cull [_Cull]
        ZWrite [_ZWrite]
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _BumpMap_ST;
                float4 _MetallicGlossMap_ST;
                float4 _OcclusionMap_ST;
                float4 _EmissionMap_ST;
                float4 _EmissionColor;
                float _BumpScale;
                float _Metallic;
                float _Smoothness;
                float _OcclusionStrength;
                float _Cutoff;
                float _AlphaClip;
                float _Surface;
                float _SnapRows;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 baseUV : TEXCOORD1;
                float2 normalUV : TEXCOORD2;
                float2 metallicUV : TEXCOORD3;
                float2 occlusionUV : TEXCOORD4;
                float2 emissionUV : TEXCOORD5;
                half3 normalWS : TEXCOORD6;
                half4 tangentWS : TEXCOORD7;
                float fogFactor : TEXCOORD8;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 9);
                float4 probeOcclusion : TEXCOORD10;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positions.positionCS;
                if (_SnapRows > 0 && output.positionCS.w > 0)
                {
                    float2 grid = float2(_SnapRows * _ScreenParams.x / _ScreenParams.y, _SnapRows) * 0.5;
                    output.positionCS.xy = round(output.positionCS.xy / output.positionCS.w * grid) / grid * output.positionCS.w;
                }

                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.baseUV = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalUV = TRANSFORM_TEX(input.uv, _BumpMap);
                output.metallicUV = TRANSFORM_TEX(input.uv, _MetallicGlossMap);
                output.occlusionUV = TRANSFORM_TEX(input.uv, _OcclusionMap);
                output.emissionUV = TRANSFORM_TEX(input.uv, _EmissionMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH4(output.positionWS, output.normalWS, GetWorldSpaceNormalizeViewDir(output.positionWS), output.vertexSH, output.probeOcclusion);
                return output;
            }

            half3 SampleNormalTS(Varyings input)
            {
                return UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.normalUV), _BumpScale);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
                half4 baseColor = baseSample * _BaseColor;
                if (_AlphaClip > 0) clip(baseColor.a - _Cutoff);

                InputData lightingInput = (InputData)0;
                lightingInput.positionWS = input.positionWS;
                lightingInput.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lightingInput.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lightingInput.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lightingInput.normalWS = NormalizeNormalPerPixel(
                    TransformTangentToWorld(
                        SampleNormalTS(input),
                        half3x3(
                            input.tangentWS.xyz,
                            cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w,
                            input.normalWS)));
                lightingInput.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, lightingInput.normalWS);
                lightingInput.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = baseColor.rgb;
                surface.alpha = baseColor.a;
                surface.normalTS = SampleNormalTS(input);
                surface.metallic = _Metallic * SAMPLE_TEXTURE2D(
                    _MetallicGlossMap, sampler_MetallicGlossMap, input.metallicUV).r;
                surface.smoothness = _Smoothness;
                surface.occlusion = lerp(1, SAMPLE_TEXTURE2D(
                    _OcclusionMap, sampler_OcclusionMap, input.occlusionUV).g, _OcclusionStrength);
                surface.emission = SAMPLE_TEXTURE2D(
                    _EmissionMap, sampler_EmissionMap, input.emissionUV).rgb * _EmissionColor.rgb;

                half4 color = UniversalFragmentPBR(lightingInput, surface);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/Meta"
    }
}
