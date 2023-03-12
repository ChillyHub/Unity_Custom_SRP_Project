#ifndef BRDF_INCLUDED
#define BRDF_INCLUDED

#define NON_METALLIC_REFLECTIVITY 0.04

struct BRDF
{
    float3 diffuse;
    float3 specular;
    float roughness;
    float perceptualRoughness;
    float fresnel;
};

BRDF GetBRDF(Surface surface)
{
    BRDF brdf;
    brdf.specular = lerp(NON_METALLIC_REFLECTIVITY, surface.color, surface.metallic);
    brdf.diffuse = surface.color - brdf.specular;
    brdf.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
    brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);
    brdf.fresnel = saturate(surface.smoothness + lerp(NON_METALLIC_REFLECTIVITY, 1.0, surface.metallic));
    
#if defined(_PREMULTIPLY_ALPHA)
    brdf.diffuse *= surface.alpha;
#endif
    
    return brdf;
}

float NormalDistributionFunction(Surface surface, Light light)
{
    return 1.0;
}

float SpecularStrength(Surface surface, BRDF brdf, Light light)
{
    float3 H = SafeNormalize(light.direction + surface.viewDirection);
    float NDotH2 = Squr(saturate(dot(surface.normal, H)));
    float LDotH2 = Squr(saturate(dot(light.direction, H)));
    float r2 = Squr(brdf.roughness);
    float d2 = Squr(NDotH2 * (r2 - 1) + 1.00001);
    float normalization = brdf.roughness * 4.0 + 2.0;
    return r2 / (d2 * max(0.1, LDotH2) * normalization);
}

// Directional light's surface color
float3 DirectBRDF(Surface surface, BRDF brdf, Light light)
{
    return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}

float3 IndirectBRDF(Surface surface, BRDF brdf, float3 diffuse, float3 specular)
{
    float fresnelStrength = surface.fresnelStrength *
        Pow4(1.0 - saturate(dot(surface.normal, surface.viewDirection)));
    float3 reflection = specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);
    reflection /= brdf.roughness * brdf.roughness + 1.0;
    return (diffuse * brdf.diffuse + reflection) * surface.occlusion;
}

#endif