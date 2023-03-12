#ifndef LIGHT_INCLUDED
#define LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64

struct Light
{
    float3 color;
    float3 direction;
    float attenuation;
    uint renderingLayerMask;
};

CBUFFER_START(_CustomLight)
    int _DirectionalLightCount;
    float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightDirectionsAndMasks[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];
    int _OtherLightCount;
    float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightDirectionsAndMasks[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END

int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

int GetOtherLightCount()
{
    return _OtherLightCount;
}

DirectionalShadowData GetDirectionalShadowData(int lightIndex, ShadowData shadowData)
{
    DirectionalShadowData data;
    data.strength = _DirectionalLightShadowData[lightIndex].x;
    data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
    data.normalBias = _DirectionalLightShadowData[lightIndex].z;
    data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;
    return data;
}

OtherShadowData GetOtherShadowData(int lightIndex)
{
    OtherShadowData data;
    data.strength = _OtherLightShadowData[lightIndex].x;
    data.tileIndex = _OtherLightShadowData[lightIndex].y;
    data.isPoint = _OtherLightShadowData[lightIndex].z == 1.0;
    data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
    data.lightPositionWS = 0.0;
    data.lightDirectionWS = 0.0;
    data.spotDirectionWS = 0.0;
    return data;
}

Light GetDirectionalLight(int index, Surface surface, ShadowData shadowData)
{
    Light light;
    DirectionalShadowData dirShadowData = GetDirectionalShadowData(index, shadowData);
    light.color = _DirectionalLightColors[index].rgb;
    light.direction = _DirectionalLightDirectionsAndMasks[index].xyz;
    light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, surface);
    light.renderingLayerMask = asuint(_DirectionalLightDirectionsAndMasks[index].w);
    return light;
}

Light GetOtherLight(int index, Surface surface, ShadowData shadowData)
{
    Light light;
    float3 position = _OtherLightPositions[index].xyz;
    float3 ray = position - surface.position;
    light.color = _OtherLightColors[index].rgb;
    light.direction = normalize(ray);
    
    // Light attenuation
    float distanceSqr = max(dot(ray, ray), 0.00001);
    // Get point light attenuation
    float rangeAttenuation = Squr(saturate(1.0 - Squr(distanceSqr * _OtherLightPositions[index].w)));
    // Get Spot light attenuation
    float4 spotAngles = _OtherLightSpotAngles[index];
    float3 spotDirection = _OtherLightDirectionsAndMasks[index].xyz;
    float spotAttenuation = Squr(saturate(dot(spotDirection, light.direction)
        * spotAngles.x + spotAngles.y));
    OtherShadowData otherShadowData = GetOtherShadowData(index);
    otherShadowData.lightPositionWS = position;
    otherShadowData.lightDirectionWS = light.direction;
    otherShadowData.spotDirectionWS = spotDirection;
    light.attenuation = GetOtherShadowAttenuation(otherShadowData, shadowData, surface) *
        rangeAttenuation * spotAttenuation / distanceSqr;
    light.renderingLayerMask = asuint(_OtherLightDirectionsAndMasks[index].w);
    return light;
}

#endif