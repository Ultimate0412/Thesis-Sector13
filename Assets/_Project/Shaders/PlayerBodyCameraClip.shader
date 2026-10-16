Shader "Sector13/PlayerBodyClip"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _HeadHideY("Hide Above Local Y", Float) = 1.58
        _HeadHideSoft("Hide Edge Softness", Float) = 0.01
        _CutoffDistance("Camera Cutoff Distance (m, 0=off)", Float) = 0.08
        _CutoffSoftness("Camera Cutoff Softness (m)", Float) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _Smoothness;
            half   _Metallic;
            float  _HeadHideY;
            float  _HeadHideSoft;
            float  _CutoffDistance;
            float  _CutoffSoftness;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        float HideMask(float3 positionWS)
        {
            float localY = mul(unity_WorldToObject, float4(positionWS, 1.0)).y;
            float headHide = smoothstep(_HeadHideY, _HeadHideY + max(_HeadHideSoft, 1e-5), localY);

            float dist = distance(positionWS, _WorldSpaceCameraPos);
            float nearHide = 1.0 - smoothstep(_CutoffDistance, _CutoffDistance + max(_CutoffSoftness, 1e-5), dist);

            return max(headHide, nearHide);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(0.5 - HideMask(input.positionWS));

                float3 normalWS = normalize(input.normalWS);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = mainLight.color * (ndotl * mainLight.shadowAttenuation) + SampleSH(normalWS);

                half3 color = albedo * lighting;
                color = MixFog(color, input.fogCoord);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct ShadowVaryings  { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            ShadowVaryings shadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                output.positionCS = positionCS;
                output.positionWS = positionWS;
                return output;
            }

            half4 shadowFrag(ShadowVaryings input) : SV_Target
            {
                clip(0.5 - HideMask(input.positionWS));
                return 0;
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
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma target 3.0
            #pragma multi_compile_instancing

            struct DepthAttributes { float4 positionOS : POSITION; };
            struct DepthVaryings  { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            DepthVaryings depthVert(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 depthFrag(DepthVaryings input) : SV_Target
            {
                clip(0.5 - HideMask(input.positionWS));
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex normalsVert
            #pragma fragment normalsFrag
            #pragma target 3.0
            #pragma multi_compile_instancing

            struct NormalsAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct NormalsVaryings  { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            NormalsVaryings normalsVert(NormalsAttributes input)
            {
                NormalsVaryings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 normalsFrag(NormalsVaryings input) : SV_Target
            {
                clip(0.5 - HideMask(input.positionWS));
                return half4(normalize(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
