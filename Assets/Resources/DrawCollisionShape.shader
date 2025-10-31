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
// A shader explicitly for the rendering of the collision shapes from Chrono.
// Includes a distance limit, to cull any vertices over a certain distance
// from the camera. Updated for URP multi-camera support
//
// =============================================================================
Shader "Custom/DrawCollisionShape"
{
    Properties
    {
        _Color("Color", Color) = (0,1,1,1)
        _MaxDistance("Max Distance", Float) = 25.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalRenderPipeline" }
        
        Pass
        {
            Name "CollisionLines"
            Tags { "LightMode" = "UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _MaxDistance;
            CBUFFER_END

            struct appdata
            {
                uint vertexID : SV_VertexID;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float distance : TEXCOORD0;
            };

            StructuredBuffer<float3> vertexPositions;

            v2f vert(appdata v)
            {
                v2f o;
                uint index = v.vertexID;
                float3 positionWS = vertexPositions[index];
                
                // Transform world space to clip space using URP helpers
                o.positionCS = TransformWorldToHClip(positionWS);
                
                // Calculate distance from camera to vertex in world space
                o.distance = length(GetCameraPositionWS() - positionWS);
                o.color = _Color;
                
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Apply distance-based fade
                float alpha = i.distance < _MaxDistance ? _Color.a : 0.0;
                return float4(i.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}