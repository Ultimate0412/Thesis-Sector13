// Sector13 Decal Studio - atlas decal shader for URP 17 (Unity 6)
// Hand-authored DBuffer decal shader: does not depend on Shader Graph.
// Channels:
//   _BaseMap   : RGB = albedo, A = coverage (blend weight)
//   _NormalMap : RG  = tangent-space normal XY (Z reconstructed), B = height
//   _MaskMap   : R = metallic, G = occlusion, B = smoothness, A = MAOS blend weight
Shader "Sector13/Decals/DecalAtlas"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo (RGB) Alpha (A)", 2D) = "white" {}
        [Normal] _NormalMap ("Normal (RG) Height (B)", 2D) = "bump" {}
        [NoScaleOffset] _MaskMap ("Metallic(R) AO(G) Smoothness(B) Blend(A)", 2D) = "white" {}

        [Header(Tint)]
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Variation ("Texture Variation", Range(0, 1)) = 0.2
        _Brightness ("Tint Brightness", Range(0, 4)) = 1.45
        _Saturation ("Tint Saturation", Range(0, 3)) = 1.6

        [Header(Blend)]
        _Opacity ("Opacity", Range(0, 1)) = 1
        [Toggle(_ALPHACLIP)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Normal)]
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1
        _NormalFlipY ("Flip Normal Y", Range(0, 1)) = 0

        [Header(Height Cavity)]
        _HeightStrength ("Height Cavity Strength", Range(0, 1)) = 0.5
        _HeightContrast ("Cavity Contrast", Range(0.1, 6)) = 1.5

        [Header(Surface)]
        _MetallicScale ("Metallic Scale", Range(0, 2)) = 1
        _SmoothnessScale ("Smoothness Scale", Range(0, 2)) = 1
        _OcclusionScale ("Occlusion Scale", Range(0, 2)) = 1

        [Header(Emission (UV traces))]
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 8)) = 1

        // ช่องของ decal ใน atlas (x,y,w,h) — ใช้แค่วาดพรีวิวใน Inspector
        [HideInInspector] _DecalRect ("Atlas Cell", Vector) = (0, 0, 1, 1)
    }

    CustomEditor "Sector13.Decals.EditorTools.DecalAtlasShaderGUI"

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        // ------------------------------------------------------------------
        // DBuffer projector pass (technique: DBuffer, surface data AlbedoNormalMAOS)
        // ------------------------------------------------------------------
        Pass
        {
            Name "DBufferProjector"
            Tags { "LightMode" = "DBufferProjector" }

            Blend 0 SrcAlpha OneMinusSrcAlpha, Zero OneMinusSrcAlpha
            Blend 1 SrcAlpha OneMinusSrcAlpha, Zero OneMinusSrcAlpha
            Blend 2 SrcAlpha OneMinusSrcAlpha, Zero OneMinusSrcAlpha
            Cull Front
            ZTest Greater
            ZWrite Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma shader_feature_local_fragment _ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            UNITY_INSTANCING_BUFFER_START(Decal)
                UNITY_DEFINE_INSTANCED_PROP(float4x4, _NormalToWorld)
                UNITY_DEFINE_INSTANCED_PROP(float, _DecalLayerMaskFromDecal)
            UNITY_INSTANCING_BUFFER_END(Decal)

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskMap);    SAMPLER(sampler_MaskMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _Variation;
                float  _Brightness;
                float  _Saturation;
                float  _Opacity;
                float  _Cutoff;
                float  _NormalStrength;
                float  _NormalFlipY;
                float  _HeightStrength;
                float  _HeightContrast;
                float  _MetallicScale;
                float  _SmoothnessScale;
                float  _OcclusionScale;
                float4 _EmissionColor;
                float  _EmissionIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            void Frag(Varyings input, OUTPUT_DBUFFER(outDBuffer))
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // --- screen space reconstruction -------------------------
                float2 positionSS = input.positionCS.xy * _ScreenSize.zw;
                float depth = LoadSceneDepth(input.positionCS.xy);
            #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
            #endif
                float3 positionWS = ComputeWorldSpacePosition(positionSS, depth, UNITY_MATRIX_I_VP);

                // --- decal (object) space clipping -----------------------
                float3 positionDS = TransformWorldToObject(positionWS);
                positionDS *= float3(1.0, -1.0, 1.0);
                float clipValue = 0.5 - Max3(abs(positionDS.x), abs(positionDS.y), abs(positionDS.z));
                clip(clipValue);

                // --- atlas uv (projector uvScale / uvBias) --------------
                float2 uv = positionDS.xz + float2(0.5, 0.5);

                float4x4 normalToWorld = UNITY_ACCESS_INSTANCED_PROP(Decal, _NormalToWorld);
                float fadeFactor = saturate(normalToWorld[0][3]);
                float2 atlasScale  = float2(normalToWorld[3][0], normalToWorld[3][1]);
                float2 atlasOffset = float2(normalToWorld[3][2], normalToWorld[3][3]);
                uv = uv * atlasScale + atlasOffset;

                // --- samples --------------------------------------------
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half4 maskSample = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
                half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv);

                half alpha = baseSample.a * _Opacity * fadeFactor;

            #ifdef _ALPHACLIP
                clip(alpha - _Cutoff);
            #endif

                // --- normal + height cavity -----------------------------
                // B channel carries height, so Z cannot be unpacked from the texture:
                // rebuild Z from XY instead of UnpackNormalScale().
                half2 normalXY = normalSample.rg * 2.0 - 1.0;
                normalXY *= _NormalStrength;
                half normalZ = sqrt(saturate(1.0 - dot(normalXY, normalXY)));
                half3 normalTS = half3(normalXY, normalZ);
                normalTS.y = lerp(normalTS.y, -normalTS.y, _NormalFlipY);
                half3 normalWS = normalize(mul((half3x3)normalToWorld, normalTS));

                half slope = saturate(length(normalTS.xy) * _HeightContrast);
                half cavity = saturate(1.0 - slope * _HeightStrength);

                // --- surface --------------------------------------------
                half lum = saturate(dot(baseSample.rgb, half3(0.299, 0.587, 0.114)));

                // ดัน saturation ของ tint ให้สีออกชัด (ไม่ต้องพึ่ง emission)
                half3 tint = _BaseColor.rgb;
                half tintGray = dot(tint, half3(0.299, 0.587, 0.114));
                tint = saturate(lerp(tintGray.xxx, tint, _Saturation));

                half3 tinted = saturate(tint * lerp(1.0h, lum, _Variation) * _Brightness);

                DecalSurfaceData surfaceData = (DecalSurfaceData)0;
                surfaceData.baseColor   = half4(tinted, alpha);
                surfaceData.normalWS    = half4(normalWS, alpha);
                surfaceData.metallic    = saturate(maskSample.r * _MetallicScale);
                surfaceData.occlusion   = saturate(maskSample.g * cavity * _OcclusionScale);
                surfaceData.smoothness  = saturate(maskSample.b * _SmoothnessScale);
                surfaceData.MAOSAlpha   = alpha;
                surfaceData.emissive    = half3(0, 0, 0);

                ENCODE_INTO_DBUFFER(surfaceData, outDBuffer);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Forward emissive pass (glow under UV light, same as URP decals)
        // ------------------------------------------------------------------
        Pass
        {
            Name "DecalProjectorForwardEmissive"
            Tags { "LightMode" = "DecalProjectorForwardEmissive" }

            Blend 0 SrcAlpha One
            Cull Front
            ZTest Greater
            ZWrite Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            UNITY_INSTANCING_BUFFER_START(Decal)
                UNITY_DEFINE_INSTANCED_PROP(float4x4, _NormalToWorld)
                UNITY_DEFINE_INSTANCED_PROP(float, _DecalLayerMaskFromDecal)
            UNITY_INSTANCING_BUFFER_END(Decal)

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskMap);    SAMPLER(sampler_MaskMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _Variation;
                float  _Brightness;
                float  _Saturation;
                float  _Opacity;
                float  _Cutoff;
                float  _NormalStrength;
                float  _NormalFlipY;
                float  _HeightStrength;
                float  _HeightContrast;
                float  _MetallicScale;
                float  _SmoothnessScale;
                float  _OcclusionScale;
                float4 _EmissionColor;
                float  _EmissionIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target0
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 positionSS = input.positionCS.xy * _ScreenSize.zw;
                float depth = LoadSceneDepth(input.positionCS.xy);
            #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
            #endif
                float3 positionWS = ComputeWorldSpacePosition(positionSS, depth, UNITY_MATRIX_I_VP);

                float3 positionDS = TransformWorldToObject(positionWS);
                positionDS *= float3(1.0, -1.0, 1.0);
                clip(0.5 - Max3(abs(positionDS.x), abs(positionDS.y), abs(positionDS.z)));

                float2 uv = positionDS.xz + float2(0.5, 0.5);

                float4x4 normalToWorld = UNITY_ACCESS_INSTANCED_PROP(Decal, _NormalToWorld);
                float fadeFactor = saturate(normalToWorld[0][3]);
                float2 atlasScale  = float2(normalToWorld[3][0], normalToWorld[3][1]);
                float2 atlasOffset = float2(normalToWorld[3][2], normalToWorld[3][3]);
                uv = uv * atlasScale + atlasOffset;

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half alpha = baseSample.a * _Opacity * fadeFactor;

            #ifdef _ALPHACLIP
                clip(alpha - _Cutoff);
            #endif

                half3 emission = _EmissionColor.rgb * _EmissionIntensity * baseSample.rgb;
                return half4(emission, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
