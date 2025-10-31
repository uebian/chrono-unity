// =============================================================================
// PROJECT CHRONO - http://projectchrono.org
//
// Copyright (c) 2024 projectchrono.org
// All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found
// in the LICENSE file at the top level of the distribution.
//
// =============================================================================
// Authors: Josh Diyn
// =============================================================================
//
// A basic grid shader using local coordinates to wrap about a tyre for
// visualisation purposes
//
// =============================================================================
Shader "Custom/CheckerShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _CheckerSize("Checker Size", Float) = 10.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.3
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _AmbientOcclusion("Ambient Occlusion", Range(0.0, 1.0)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        // PASS 1: Base checker pattern (renders everywhere)
        Pass
        {
            Name "CheckerBase"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogCoord : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _CheckerSize;
                float _Smoothness;
                float _Metallic;
                float _AmbientOcclusion;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.normalWS = normalInput.normalWS;
                
                output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Checker pattern from local coordinates
                float2 uv = mul(UNITY_MATRIX_I_M, float4(input.positionWS, 1)).xz * _CheckerSize;
                float checker = (step(0.5, frac(uv.x)) * step(0.5, frac(uv.y)) + step(0.5, frac(uv.x + 0.5)) * step(0.5, frac(uv.y + 0.5))) * 2.0 - 1.0;
                half4 color = _Color * checker;

                // Setup vectors
                float3 N = normalize(input.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 L = mainLight.direction;
                float3 H = normalize(L + V);
                
                // Lighting terms
                float NdotL = saturate(dot(N, L));
                float NdotH = saturate(dot(N, H));
                float NdotV = saturate(dot(N, V));
                float roughness2 = pow(1.0 - _Smoothness, 2.0);
                
                half3 radiance = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                
                // GGX + Fresnel
                float D = roughness2 / (3.14159 * pow(NdotH * NdotH * (roughness2 - 1.0) + 1.0, 2.0));
                float3 F = lerp(0.04, color.rgb, _Metallic) + (1.0 - lerp(0.04, color.rgb, _Metallic)) * pow(1.0 - saturate(dot(H, V)), 5.0);
                
                half3 diffuse = radiance * NdotL * (1.0 - _Metallic);
                half3 specular = D * F * radiance * NdotL * _Smoothness;
                
                // Additional lights
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < lightCount; ++i)
                {
                    Light light = GetAdditionalLight(i, input.positionWS);
                    float3 L2 = light.direction;
                    float NdotL2 = saturate(dot(N, L2));
                    half3 rad2 = light.color * light.distanceAttenuation * light.shadowAttenuation;
                    diffuse += rad2 * NdotL2 * (1.0 - _Metallic);
                    specular += (roughness2 / (3.14159 * pow(saturate(dot(N, normalize(L2 + V))) * saturate(dot(N, normalize(L2 + V))) * (roughness2 - 1.0) + 1.0, 2.0))) * F * rad2 * NdotL2 * _Smoothness * 0.5;
                }
                
                // Ambient + rim
                half3 lighting = diffuse + SampleSH(N) * _AmbientOcclusion + specular + pow(1.0 - NdotV, 4.0) * 0.5 * mainLight.color;
                
                return half4(MixFog(color.rgb * lighting, input.fogCoord), color.a);
            }
            ENDHLSL
        }
        
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}

