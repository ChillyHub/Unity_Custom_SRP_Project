#ifndef SHADOWS_INCLUDED
#define SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_SHADOWED_OTHER_LIGHT_COUNT 16
#define MAX_CASCADE_COUNT 4
#define SHADOW_SAMPLER sampler_linear_clamp_compare

#if defined(_DIRECTIONAL_PCF3)
    #define DIRECTIONAL_FILTER_SAMPLES 4
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
    #define DIRECTIONAL_FILTER_SAMPLES 9
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
    #define DIRECTIONAL_FILTER_SAMPLES 16
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#if defined(_OTHER_PCF3)
    #define OTHER_FILTER_SAMPLES 4
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
    #define OTHER_FILTER_SAMPLES 9
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
    #define OTHER_FILTER_SAMPLES 16
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

// Baked shadow data
struct ShadowMask
{
    bool always;
    bool distance;
    float4 shadows;
};

// Shadow data
struct ShadowData
{
    int cascadeIndex;
    // Whether or not sample shadow
    float strength;
    float cascadeBlend;
    ShadowMask shadowMask;
};

struct DirectionalShadowData
{
    float strength;
    int tileIndex;
    float normalBias;
    int shadowMaskChannel;
};

struct OtherShadowData
{
    float strength;
    int tileIndex;
    bool isPoint;
    int shadowMaskChannel;
    float3 lightPositionWS;
    float3 lightDirectionWS;
    float3 spotDirectionWS;
};

// Shadow atlas
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherShadowAtlas);
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
    // Cascade count and bounding sphere data
    int _CascadeCount;
    float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
    // Cascade data
    float4 _CascadeData[MAX_CASCADE_COUNT];
    // Shadow transform matrix
    float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
    float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
    float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT];
    // Shadow fade distance
    float4 _ShadowDistanceFade;
    float4 _ShadowAtlasSize;                                         
CBUFFER_END

static const float3 pointShadowPlanes[6] =
{
    float3(-1.0,  0.0,  0.0),
    float3( 1.0,  0.0,  0.0),
    float3( 0.0, -1.0,  0.0),
    float3( 0.0,  1.0,  0.0),
    float3( 0.0,  0.0, -1.0),
    float3( 0.0,  0.0,  1.0)
};

// Calculate strength of shadow fade
float FadedShadowStrength(float distance, float scale, float fade)
{
    return saturate((1.0 - distance * scale) * fade);
}

// Get world space surface shadow data
ShadowData GetShadowData(Surface surface)
{
    ShadowData data;
    data.cascadeBlend = 1.0;
    data.shadowMask.always = false;
    data.shadowMask.distance = false;
    data.shadowMask.shadows = 1.0;
    // Get linear faded shadow strength
    data.strength = FadedShadowStrength(surface.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
    int i;
    // If squared distance between surface and bounding sphere center, less than sphere radius,
    // the object is in the shadow cascade.
    for (i = 0; i < _CascadeCount; i++)
    {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquared(surface.position, sphere.xyz);
        // If object in the last cascade, get fade cascade shadow strength
        if (distanceSqr < sphere.w)
        {
            // Calculate transition strength of cascade shadow
            float fade = FadedShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z);
            // If object in the last cascade range
            if (i == _CascadeCount - 1)
            {
                data.strength *= fade;
            }
            else
            {
                data.cascadeBlend = fade;
            }
            break;
        }
    }
    // If beyond the last cascade and cascade count greater than 0,
    // set strength as 0, in order not to sample shadow
    if (i == _CascadeCount && _CascadeCount > 0)
    {
        data.strength = 0.0;
    }
    
#if defined(_CASCADE_BLEND_DITHER)
    else if (data.cascadeBlend < surface.dither)
    {
        i += 1;
    }
#endif
    
#if !defined(_CASCADE_BLEND_SOFT)
    data.cascadeBlend = 1.0;
#endif
    
    data.cascadeIndex = i;
    return data;
}

// Sampler Shadow Atlas
float SampleDirectionalShadowAtlas(float3 positionSTS)
{
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float SampleOtherShadowAtlas(float3 positionSTS, float3 bounds)
{
    positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.zz);
    return SAMPLE_TEXTURE2D_SHADOW(_OtherShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float FilterDirectionalShadow(float3 positionSTS)
{
#if defined(DIRECTIONAL_FILTER_SETUP)
    // Sampler weight
    float weights[DIRECTIONAL_FILTER_SAMPLES];
    // Sampler position
    float2 positions[DIRECTIONAL_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.yyxx;
    DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
    float shadow = 0;
    for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++)
    {
        // Iterate all samplers to get sum of weights
        shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy, positionSTS.z));
    }
    return shadow;
#else
    return SampleDirectionalShadowAtlas(positionSTS);
#endif
}

float FilterOtherShadow(float3 positionSTS, float3 bounds)
{
#if defined(OTHER_FILTER_SETUP)
    // Sampler weight
    real weights[OTHER_FILTER_SAMPLES];
    // Sampler position
    real2 positions[OTHER_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.wwzz;
    OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
    float shadow = 0;
    for (int i = 0; i < OTHER_FILTER_SAMPLES; i++)
    {
        // Iterate all samplers to get sum of weights
        shadow += weights[i] * SampleOtherShadowAtlas(float3(positions[i].xy, positionSTS.z), bounds);
    }
    return shadow;
#else
    return SampleOtherShadowAtlas(positionSTS, bounds);
#endif
}

float GetCascadedShadow(DirectionalShadowData directional, ShadowData global, Surface surface)
{
    // Calculate normal bias
    float3 normalBias = surface.interpolatedNormal * directional.normalBias *
        _CascadeData[global.cascadeIndex].y;
    // Get clip space position by transform matrix from world position
    float3 positionSTS =
        mul(_DirectionalShadowMatrices[directional.tileIndex],
            float4(surface.position + normalBias, 1.0)).xyz;
    float shadow = FilterDirectionalShadow(positionSTS);
    // If cascade blend < 1, object in transition range of cascade shadow, need's to sample and blend
    // the next cascade shadow
    if (global.cascadeBlend < 1.0)
    {
        normalBias = surface.interpolatedNormal * directional.normalBias *
            _CascadeData[global.cascadeIndex + 1].y;
        positionSTS =
            mul(_DirectionalShadowMatrices[directional.tileIndex + 1],
                float4(surface.position + normalBias, 1.0)).xyz;
        shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend);
    }
    return shadow;
}

// Get non directional light realtime shadow attenuation
float GetOtherShadow(OtherShadowData other, ShadowData global, Surface surface)
{
    float tileIndex = other.tileIndex;
    float3 lightPlane = other.spotDirectionWS;
    if (other.isPoint)
    {
        float faceOffset = CubeMapFaceID(-other.lightDirectionWS);
        tileIndex += faceOffset;
        lightPlane = pointShadowPlanes[faceOffset];
    }
    float4 tileData = _OtherShadowTiles[tileIndex];
    float3 surfaceToLight = other.lightPositionWS - surface.position;
    float distanceToLightPlane = dot(surfaceToLight, lightPlane);
    float3 normalBias = surface.interpolatedNormal * distanceToLightPlane * tileData.w;
    float4 positionSTS = mul(_OtherShadowMatrices[tileIndex],
        float4(surface.position + normalBias, 1.0));
    // Projection, need to / w
    return FilterOtherShadow(positionSTS.xyz / positionSTS.w, tileData.xyz);
}

// Get baked shadow attenuation
float GetBakedShadow(ShadowMask mask, int channel)
{
    float shadow = 1.0;
    if (mask.always || mask.distance)
    {
        if (channel >= 0)
        {
            shadow = mask.shadows[channel];
        }
    }
    return shadow;
}

float GetBakedShadow(ShadowMask mask, int channel, float strength)
{
    if (mask.always || mask.distance)
    {
        return lerp(1.0, GetBakedShadow(mask, channel), strength);
    }
    return 1.0;
}

// Mix baked and real time shadow
float MixBakedAndRealtimeShadows(ShadowData global, float shadow, int shadowMaskChannel, float strength)
{
    float baked = GetBakedShadow(global.shadowMask, shadowMaskChannel);
    if (global.shadowMask.always)
    {
        shadow = lerp(1.0, shadow, global.strength);
        shadow = min(baked, shadow);
        return lerp(1.0, shadow, strength);
    }
    if (global.shadowMask.distance)
    {
        shadow = lerp(baked, shadow, global.strength);
        return lerp(1.0, shadow, strength);
    }
    return  lerp(1.0, shadow, strength * global.strength);
}

// Calculate shadow attenuation
float GetDirectionalShadowAttenuation(DirectionalShadowData data, ShadowData global, Surface surface)
{
    // If do not receive shadow, attenuation equal 1
#if !defined(_RECEIVE_SHADOWS)
    return 1.0;
#endif

    float shadow;
    if (data.strength * global.strength <= 0.0)
    {
        shadow = GetBakedShadow(global.shadowMask, data.shadowMaskChannel, abs(data.strength));
    }
    else
    {
        shadow = GetCascadedShadow(data, global, surface);
        // Shadow blend
        shadow = MixBakedAndRealtimeShadows(global, shadow, data.shadowMaskChannel, data.strength);
    }
    return shadow;
}

float GetOtherShadowAttenuation(OtherShadowData other, ShadowData global, Surface surface)
{
#if !defined(_RECEIVE_SHADOWS)
    return 1.0;
#endif

    float shadow;
    if (other.strength * global.strength <= 0.0)
    {
        shadow = GetBakedShadow(global.shadowMask, other.shadowMaskChannel, abs(other.strength));
    }
    else
    {
        shadow = GetOtherShadow(other, global, surface);
        shadow = MixBakedAndRealtimeShadows(global, shadow, other.shadowMaskChannel, other.strength);
    }
    return shadow;
}

#endif