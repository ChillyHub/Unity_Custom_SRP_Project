#ifndef UNITY_INPUT_INCLUDED
#define UNITY_INPUT_INCLUDED

CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    float4 unity_LODFade;
    real4 unity_WorldTransformParams;
    float4 unity_RenderingLayer;
    float4 _ProjectionParams;
    float4 unity_OrthoParams;
    float4 _ScreenParams;
    float4 _ZBufferParams;
    // Occlusion Probe
    float4 unity_ProbesOcclusion;
    // Reflection Probe
    float4 unity_SpecCube0_HDR;
    // Light Map
    float4 unity_LightmapST;
    float4 unity_DynamicLightmapST;
    // Light Probe
    float4 unity_SHAr;
    float4 unity_SHAg;
    float4 unity_SHAb;
    float4 unity_SHBr;
    float4 unity_SHBg;
    float4 unity_SHBb;
    float4 unity_SHC;
    // Light Probe proxy
    float4 unity_ProbeVolumeParams;
    float4x4 unity_ProbeVolumeWorldToObject;
    float4 unity_ProbeVolumeSizeInv;
    float4 unity_ProbeVolumeMin;
    // Lights per object
    real4 unity_LightData;
    real4 unity_LightIndices[2];
CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 glstate_matrix_projection;

float3 _WorldSpaceCameraPos;

#endif