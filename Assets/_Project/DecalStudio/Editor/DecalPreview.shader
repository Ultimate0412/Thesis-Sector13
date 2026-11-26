// Editor-only preview shader for decal materials.
// Draws one atlas cell on a neutral lit surface so the Inspector / Decal Preview
// window reads like the decal in the scene (normal, height cavity, specular, emission).
Shader "Hidden/Sector13/DecalPreview"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _NormalMap ("Normal", 2D) = "bump" {}
        _MaskMap ("Mask", 2D) = "white" {}
        _DecalRect ("Cell", Vector) = (0, 0, 1, 1)
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Float) = 1
        _Brightness ("Brightness", Float) = 1
        _Saturation ("Saturation", Float) = 1
        _Variation ("Variation", Float) = 0
        _HeightStrength ("Height", Float) = 0
        _NormalStrength ("Normal", Float) = 1
        _MetallicScale ("Metallic", Float) = 1
        _SmoothnessScale ("Smoothness", Float) = 1
        _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _EmissionIntensity ("Emission Intensity", Float) = 0
        _LightDir ("Light Direction", Vector) = (-0.35, 0.55, 0.75, 0)
        _LightColor ("Light Color", Color) = (1, 1, 1, 1)
        _Ambient ("Ambient", Color) = (0.22, 0.22, 0.22, 1)
        _SurfaceColor ("Surface", Color) = (0.16, 0.16, 0.16, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Overlay" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _BaseMap;
            sampler2D _NormalMap;
            sampler2D _MaskMap;
            float4 _DecalRect;
            float4 _BaseColor;
            float _Opacity;
            float _Brightness;
            float _Saturation;
            float _Variation;
            float _HeightStrength;
            float _NormalStrength;
            float _MetallicScale;
            float _SmoothnessScale;
            float4 _EmissionColor;
            float _EmissionIntensity;
            float4 _LightDir;
            float4 _LightColor;
            float4 _Ambient;
            float4 _SurfaceColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv * _DecalRect.zw + _DecalRect.xy;

                float4 baseSample = tex2D(_BaseMap, uv);
                float4 normalSample = tex2D(_NormalMap, uv);
                float4 mask = tex2D(_MaskMap, uv);

                float3 n = float3(normalSample.r * 2.0 - 1.0,
                                  normalSample.g * 2.0 - 1.0,
                                  0.0);
                n.z = sqrt(saturate(1.0 - dot(n.xy, n.xy)));
                n = normalize(lerp(float3(0.0, 0.0, 1.0), n, _NormalStrength));

                float3 L = normalize(_LightDir.xyz);
                float3 V = float3(0.0, 0.0, 1.0);
                float3 H = normalize(L + V);

                float ndl = saturate(dot(n, L));
                float smoothness = saturate(mask.b * _SmoothnessScale);
                float metallic = saturate(mask.r * _MetallicScale);
                float specPower = exp2(lerp(2.0, 11.0, smoothness));
                float spec = pow(saturate(dot(n, H)), specPower) * lerp(0.04, 1.0, metallic);
                float ao = mask.g;
                float cavity = saturate(1.0 - length(n.xy) * _HeightStrength * 0.6);

                float3 tint = _BaseColor.rgb;
                float tintGray = dot(tint, float3(0.2126, 0.7152, 0.0722));
                tint = saturate(lerp(tintGray.xxx, tint, _Saturation));

                float3 albedo = baseSample.rgb * tint * _Brightness;
                float3 lit = albedo * (_Ambient.rgb + _LightColor.rgb * ndl) * ao * cavity;
                lit += spec * _LightColor.rgb * smoothness;

                float3 emission = _EmissionColor.rgb * _EmissionIntensity * baseSample.rgb;
                float3 color = lit + emission;
                float alpha = saturate(baseSample.a * _Opacity);

                // neutral surface with a slight vertical falloff so it reads as a lit plane
                float3 surface = _SurfaceColor.rgb * lerp(0.85, 1.15, i.uv.y);
                surface *= 1.0 - 0.22 * length(i.uv - 0.5);
                float3 outColor = lerp(surface, color, alpha);

                return fixed4(outColor, 1.0);
            }
            ENDCG
        }
    }
}
