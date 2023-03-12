#ifndef LIGHTING_INCLUDED
#define LIGHTING_INCLUDED

// Test whether overlap between surface mask and light mask
bool RenderingLayersOverlap(Surface surface, Light light)
{
    return (surface.renderingLayerMask & light.renderingLayerMask) != 0;
}

// Get incoming light
float3 IncomingLight(Surface surface, Light light)
{
    return saturate(dot(surface.normal, light.direction)) * light.color * light.attenuation;
}

float3 GetDiffuseLighting(Surface surface, Light light)
{
    return IncomingLight(surface, light) * surface.color;
}

float3 GetDiffuseLighting(Surface surface)
{
    // Get surface shadow data
    ShadowData shadowData = GetShadowData(surface);
    // Final light accumulation
    float3 color = 0.0;
    for (int i = 0; i < GetDirectionalLightCount(); i++)
    {
        color += GetDiffuseLighting(surface, GetDirectionalLight(i, surface, shadowData));
    }
    return color;
}

float3 GetDiffuseLighting(Surface surface, BRDF brdf, Light light)
{
    return IncomingLight(surface, light) * brdf.diffuse;
}

float3 GetDiffuseLighting(Surface surface, BRDF brdf)
{
    // Get surface shadow data
    ShadowData shadowData = GetShadowData(surface);
    // Final light accumulation
    float3 color = 0.0;
    for (int i = 0; i < GetDirectionalLightCount(); i++)
    {
        color += GetDiffuseLighting(surface, brdf, GetDirectionalLight(i, surface, shadowData));
    }
    return color;
}

float3 GetLighting(Surface surface, BRDF brdf, Light light)
{
    return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

float3 GetLighting(Surface surface, BRDF brdf, GI gi)
{
    // Get surface shadow data
    ShadowData shadowData = GetShadowData(surface);
    shadowData.shadowMask = gi.shadowMask;
    // Final light accumulation
    float3 color = IndirectBRDF(surface, brdf, gi.diffuse, gi.specular);
    for (int i = 0; i < GetDirectionalLightCount(); i++)
    {
        Light light = GetDirectionalLight(i, surface, shadowData);
        if (RenderingLayersOverlap(surface, light))
        {
            color += GetLighting(surface, brdf, light);
        }
    }
    
#if defined(_LIGHTS_PER_OBJECT)
    for (int j = 0; j < min(unity_LightData.y, 8); j++)
    {
        int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
        Light light = GetOtherLight(lightIndex, surface, shadowData);
        if (RenderingLayersOverlap(surface, light))
        {
            color += GetLighting(surface, brdf, light);
        }
    }
#else
    for (int j = 0; j < GetOtherLightCount(); j++)
    {
        Light light = GetOtherLight(j, surface, shadowData);
        if (RenderingLayersOverlap(surface, light))
        {
            color += GetLighting(surface, brdf, light);
        }
    }
#endif
    
    return color;
}

#endif