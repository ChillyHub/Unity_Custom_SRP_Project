#ifndef COMMON_INCLUDED
#define COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_PREV_MATRIX_M unity_ObjectToWorld
#define UNITY_PREV_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection

#if defined(_SHADOW_MASK_DISTANCE) || defined(_SHADOW_MASK_DISTANCE)
    #define SHADOWS_SHADOWMASK
#endif

#include "UnityInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_point_clamp);
SAMPLER(sampler_CameraColorTexture);

bool IsOrthographicCamera()
{
    return unity_OrthoParams.w;
}

float OrthographicDepthBufferToLinear(float rawDepth)
{
#if UNITY_REVERSED_Z
    rawDepth = 1.0 - rawDepth;
#endif
    return (_ProjectionParams.z - _ProjectionParams.y) * rawDepth + _ProjectionParams.y;
}

#include "Fragment.hlsl"

float Squr(float v)
{
    return v * v;
}

// Calculate square of distance
float DistanceSquared(float3 pA, float3 pB)
{
    return dot(pA - pB, pA - pB);
}

void ClipLOD(Fragment fragment, float fade)
{
#if defined(LOD_FADE_CROSSFADE)
    float dither = InterleavedGradientNoise(fragment.positionSS, 0.0);
    clip(fade + (fade < 0.0 ? dither : -dither));
#endif
}

// Decode normal data, and get original normal vector
float3 DecodeNormal(float4 sample, float scale)
{
#if defined(UNITY_NO_DXT5nm)
    return UnpackNormalRGB(sample, scale);
#else
    return UnpackNormalmapRGorAG(sample, scale);
#endif
}

// Transform normal from tangent space to world space
float3 NormalTangentToWorld(float3 normalTS, float3 normalWS, float4 tangentWS)
{
    // Build tangent to world space matrix
    float3x3 tangentToWorld = CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
    return normalize(TransformTangentToWorld(normalTS, tangentToWorld));
}

#endif