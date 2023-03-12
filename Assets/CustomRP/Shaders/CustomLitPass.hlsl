﻿#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

#include "./ShaderLibrary/Common.hlsl"
#include "./ShaderLibrary/Surface.hlsl"
#include "./ShaderLibrary/Shadows.hlsl"
#include "./ShaderLibrary/Light.hlsl"
#include "./ShaderLibrary/BRDF.hlsl"
#include "./ShaderLibrary/GI.hlsl"
#include "./ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS_SS : SV_POSITION;
    float3 positionWS : VAR_POSITION;
    float2 baseUV : VAR_BASE_UV;
#if defined(_DETAIL_MAP)
    float2 detailUV : VAR_DETAIL_UV;
#endif
    float3 normalWS : VAR_NORMAL;
#if defined(_NORMAL_MAP)
    float4 tangentWS : VAR_TANGENT;
#endif
    GI_VARYINGS_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    TRANSFER_GI_DATA(input, output);
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS_SS = TransformWorldToHClip(output.positionWS);
    output.baseUV = TransformBaseUV(input.baseUV);
#if defined(_DETAIL_MAP)
    output.detailUV = TransformDetailUV(input.baseUV);
#endif
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
#if defined(_NORMAL_MAP)
    output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS), input.tangentOS.w);
#endif
    return output;
}

float4 LitPassFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
    ClipLOD(config.fragment, unity_LODFade.x);
#if defined(_MASK_MAP)
    config.useMask = true;
#endif
#if defined(_DETAIL_MAP)
    config.detailUV = input.detailUV;
    config.useDetail = true;
#endif
    float4 base = GetBase(config);

#if defined(_CLIPPING)
    clip(base.a - GetCutoff(config));
#endif

    Surface surface;
    surface.position = input.positionWS;
#if defined(_NORMAL_MAP)
    surface.normal = NormalTangentToWorld(GetNormalTS(config), input.normalWS, input.tangentWS);
    surface.interpolatedNormal = input.normalWS;
#else
    surface.normal = normalize(input.normalWS);
    surface.interpolatedNormal = surface.normal;
#endif
    surface.color = base.rgb;
    surface.alpha = base.a;
    surface.metallic = GetMetallic(config);
    surface.smoothness = GetSmoothness(config);
    surface.occlusion = GetOcclusion(config);
    surface.fresnelStrength = GetFresnel(config);
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.depth = -TransformWorldToView(input.positionWS).z;
    surface.dither = InterleavedGradientNoise(config.fragment.positionSS, 0);
    surface.renderingLayerMask = asuint(unity_RenderingLayer.x);

    BRDF brdf = GetBRDF(surface);
    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
    float3 color = GetLighting(surface, brdf, gi);
    color += GetEmission(config);
    return float4(color, GetFinalAlpha(surface.alpha));
}

#endif