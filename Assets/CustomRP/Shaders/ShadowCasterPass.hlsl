#ifndef SHADOW_CASTER_PASS_INCLUDED
#define SHADOW_CASTER_PASS_INCLUDED

#include "./ShaderLibrary/Common.hlsl"

struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 baseUV : VAR_BASE_UV;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

bool _ShadowPancaking;

Varyings ShadowCasterPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.baseUV = TransformBaseUV(input.baseUV);

    if (_ShadowPancaking)
    {
#if UNITY_REVERSED_Z
        // DirectX: UNITY_NEAR_CLIP_VALUE = 1, _FAR_... =  0, so CS.z <= 1
        // OpenGL:  UNITY_NEAR_CLIP_VALUE = 1, _FAR_... = -1, so CS.z <= 1
        // So that the vertex in front of near clip will align to near clip.
        output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
        // DirectX: UNITY_NEAR_CLIP_VALUE =  0, _FAR_... = 1, so CS.z >= 0
        // OpenGL:  UNITY_NEAR_CLIP_VALUE = -1, _FAR_... = 1, so CS.z >= -1
        // So that the vertex in front of near clip will align to near clip.
        output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif
    }
    
    return output;
}

void ShadowCasterPassFragment(Varyings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    InputConfig config = GetInputConfig(input.positionCS, input.baseUV);
    ClipLOD(config.fragment, unity_LODFade.x);
    float4 base = GetBase(config);
    
#if defined(_SHADOWS_CLIP)
    clip(base.a - GetCutoff(config));
#elif defined(_SHADOWS_DITHER)
    float dither = InterleavedGradientNoise(input.positionCS.xy, 0);
    clip(base.a - dither);
#endif
}

#endif