#ifndef SURFACE_INCLUDED
#define SURFACE_INCLUDED

struct Surface
{
    float3 position;
    float3 normal;
    float3 interpolatedNormal;
    float3 color;
    float alpha;
    float metallic;
    float smoothness;
    float occlusion;
    float fresnelStrength;
    float3 viewDirection;
    // Surface depth in view space (camera space)
    float depth;
    float dither;
    uint renderingLayerMask;
};

#endif
