using CustomRP.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime
{
    public class Shadows
    {
        private const string bufferName = "Shadows Cast";

        private CommandBuffer buffer = new CommandBuffer
        {
            name = bufferName
        };

        private ScriptableRenderContext context;

        private CullingResults cullingResults;

        private ShadowSettings settings;
        
        // The number of lights that can cast shadow
        private const int maxShadowedDirectionalLightCount = 4;
        private const int maxShadowedOtherLightCount = 16;
        private int shadowedDirectionalLightCount;
        private int shadowedOtherLightCount;
        
        // Max Cascaded count
        private const int maxCascades = 4;

        struct ShadowedDirectionalLight
        {
            public int VisibleLightIndex;
            public float SlopeScaleBias;
            // Shadow frustum near clip plane bias
            public float NearPlaneOffset;
        }

        struct ShadowedOtherLight
        {
            public int VisibleLightIndex;
            public float SlopeScaleBias;
            public float NormalBias;
            public bool IsPoint;
        }
        
        // Storage index of lights that can cast shadow
        private ShadowedDirectionalLight[] shadowedDirectionalLights =
            new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

        private ShadowedOtherLight[] shadowedOtherLights =
            new ShadowedOtherLight[maxShadowedOtherLightCount];

        private static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
        private static int dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
        private static int otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas");
        private static int otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices");
        private static int otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles");
        private static int cascadeCountId = Shader.PropertyToID("_CascadeCount");
        private static int cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
        private static int shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");
        private static int cascadeDataId = Shader.PropertyToID("_CascadeData");
        private static int shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
        private static int shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");

        // Storage shadow transform matrix
        private static Matrix4x4[] dirShadowMatrices = 
            new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];

        private static Matrix4x4[] otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];

        private static Vector4[] otherShadowTiles = new Vector4[maxShadowedOtherLightCount];
        
        private static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades];
        private static Vector4[] cascadeData = new Vector4[maxCascades];
        
        // PCF mode
        private static string[] directionalFilterKeywords =
        {
            "_DIRECTIONAL_PCF3",
            "_DIRECTIONAL_PCF5",
            "_DIRECTIONAL_PCF7"
        };

        private static string[] otherFilterKeywords =
        {
            "_OTHER_PCF3",
            "_OTHER_PCF5",
            "_OTHER_PCF7"
        };

        private static string[] cascadeBlendKeywords =
        {
            "_CASCADE_BLEND_SOFT",
            "_CASCADE_BLEND_DITHER"
        };

        private static string[] shadowMaskKeywords =
        {
            "_SHADOW_MASK_ALWAYS",
            "_SHADOW_MASK_DISTANCE"
        };

        private bool useShadowMask;

        private Vector4 atalsSizes;

        public void Setup(ScriptableRenderContext context, CullingResults cullingResults,
            ShadowSettings settings)
        {
            this.context = context;
            this.cullingResults = cullingResults;
            this.settings = settings;

            shadowedDirectionalLightCount = 0;
            shadowedOtherLightCount = 0;
            useShadowMask = false;
        }
        
        // Storage shadow data of directional light
        public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
        {
            // Storage index of directional light, where shadow cast is opened and 
            // shadow intensity is not 0.
            if (shadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
                light.shadows != LightShadows.None && light.shadowStrength > 0.0f)
            {
                float maskChannel = -1.0f;
                // If use shadow mask
                LightBakingOutput lightBaking = light.bakingOutput;
                if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                    lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
                {
                    useShadowMask = true;
                    maskChannel = lightBaking.occlusionMaskChannel;
                }

                if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
                {
                    return new Vector4(-light.shadowStrength, 0.0f, 0.0f, maskChannel);
                }
                
                shadowedDirectionalLights[shadowedDirectionalLightCount] =
                    new ShadowedDirectionalLight
                    {
                        VisibleLightIndex = visibleLightIndex,
                        SlopeScaleBias = light.shadowBias, 
                        NearPlaneOffset = light.shadowNearPlane
                    };
                // Return shadow intensity and tile offset
                return new Vector4(light.shadowStrength, 
                    settings.directional.cascadeCount * shadowedDirectionalLightCount++, 
                    light.shadowNormalBias, maskChannel);
            }
            return new Vector4(0.0f, 0.0f, 0.0f, -1.0f);
        }
        
        // Storage shadow data of other light
        public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
        {
            if (light.shadows != LightShadows.None && light.shadowStrength > 0.0f)
            {
                float maskChannel = -1.0f;
                LightBakingOutput lightBaking = light.bakingOutput;
                if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                    lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
                {
                    useShadowMask = true;
                    maskChannel = lightBaking.occlusionMaskChannel;
                }

                bool isPoint = light.type == LightType.Point;
                int newLightCount = shadowedOtherLightCount + (isPoint ? 6 : 1);
                // Non-directional count greater than max count or no shadow need to render
                if (newLightCount > maxShadowedOtherLightCount ||
                    !cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
                {
                    return new Vector4(-light.shadowStrength, 0.0f, 0.0f, maskChannel);
                }

                shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight
                {
                    VisibleLightIndex = visibleLightIndex,
                    SlopeScaleBias = light.shadowBias,
                    NormalBias = light.shadowNormalBias,
                    IsPoint = isPoint
                };
                Vector4 data = new Vector4(light.shadowStrength, shadowedOtherLightCount, 
                    isPoint ? 1.0f : 0.0f, maskChannel);
                shadowedOtherLightCount = newLightCount;
                return data;
            }

            return new Vector4(0.0f, 0.0f, 0.0f, -1.0f);
        }
        
        // Render shadow
        public void Render()
        {
            if (shadowedDirectionalLightCount > 0)
            {
                RenderDirectionalShadows();
            }
            else
            {
                buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear,
                    RenderTextureFormat.Shadowmap);
            }

            if (shadowedOtherLightCount > 0)
            {
                RenderOtherShadows();
            }
            else
            {
                buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
            }
            // Whether of not to use shadow mask
            buffer.BeginSample(bufferName);
            SetKeywords(shadowMaskKeywords, useShadowMask ? 
                QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1);
            
            // Send cascade count to GPU
            buffer.SetGlobalInt(cascadeCountId, shadowedDirectionalLightCount > 0 ? 
                settings.directional.cascadeCount : 0);
            // Send shadow distance fade data to GPU
            float f = 1.0f - settings.directional.cascadeFade;
            buffer.SetGlobalVector(shadowDistanceFadeId, 
                new Vector4(1.0f / settings.maxDistance, 1.0f / settings.distanceFade, 
                    1.0f / (1.0f - f * f)));
            // Send atlas size and texel size
            buffer.SetGlobalVector(shadowAtlasSizeId, atalsSizes);
            buffer.EndSample(bufferName);
            ExecuteBuffer();
        }
        
        // Release temporary render texture
        public void Cleanup()
        {
            buffer.ReleaseTemporaryRT(dirShadowAtlasId);
            if (shadowedOtherLightCount > 0)
            {
                buffer.ReleaseTemporaryRT(otherShadowAtlasId);
            }
            ExecuteBuffer();
        }

        void ExecuteBuffer()
        {
            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }
        
        // Render all directional lights shadow
        void RenderDirectionalShadows()
        {
            // Create RenderTexture, and set it as shadow map
            int atlasSize = (int)settings.directional.atlasSize;
            atalsSizes.x = atlasSize;
            atalsSizes.y = 1.0f / atlasSize;
            buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, 
                FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            
            // Storage render target data into render texture
            buffer.SetRenderTarget(dirShadowAtlasId, 
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            
            // Clear depth buffer
            buffer.ClearRenderTarget(true, false, Color.clear);
            buffer.SetGlobalFloat(shadowPancakingId, 1.0f);
            buffer.BeginSample(bufferName);
            ExecuteBuffer();
            // Size and count of split texture
            int tiles = shadowedDirectionalLightCount * settings.directional.cascadeCount;
            int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
            int tileSize = atlasSize / split;
            // Iterate all dir lights
            for (int i = 0; i < shadowedDirectionalLightCount; i++)
            {
                RenderDirectionalShadows(i, split, tileSize);
            }
            
            buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
            buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
            buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
            // Set PCF mode keywords
            SetKeywords(directionalFilterKeywords, (int)settings.directional.filter - 1);
            SetKeywords(cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1);
            buffer.EndSample(bufferName);
            ExecuteBuffer();
        }
        
        // Render single directional light shadow
        void RenderDirectionalShadows(int index, int split, int tileSize)
        {
            ShadowedDirectionalLight light = shadowedDirectionalLights[index];
            var shadowSettings = new ShadowDrawingSettings(cullingResults, light.VisibleLightIndex)
            {
                useRenderingLayerMaskTest = true
            };
            
            // Get cascade shadow map parameter
            int cascadeCount = settings.directional.cascadeCount;
            int tileOffset = index * cascadeCount;
            Vector3 ratios = settings.directional.CascadeRatios;
            float cullingFactor = Mathf.Max(0.0f, 0.8f - settings.directional.cascadeFade);
            for (int i = 0; i < cascadeCount; i++)
            {
                // Calculate view and projection matrices and clip space cube
                cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                    light.VisibleLightIndex, i, cascadeCount, ratios, tileSize, light.NearPlaneOffset,
                    out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);
                
                // Get the first light bounding sphere data
                if (index == 0)
                {
                    SetCascadeData(i, splitData.cullingSphere, tileSize);
                }

                // Culling bias
                splitData.shadowCascadeBlendCullingFactor = cullingFactor;
                shadowSettings.splitData = splitData;
                // Adjust atlas index, equal light offset add cascade offset
                int tileIndex = tileOffset + i;
                float tileScale = 1.0f / split;
                // Projection matrix multiply view matrix, get WorldToLight(Camera) matrix
                dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projMatrix * viewMatrix, 
                    SetTileViewport(tileIndex, split, tileSize), tileScale);
                buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
                // Set slope scale bias
                buffer.SetGlobalDepthBias(0.0f, light.SlopeScaleBias);
                ExecuteBuffer();
            
                context.DrawShadows(ref shadowSettings);
                buffer.SetGlobalDepthBias(0.0f, 0.0f);
            }
        }
        
        // Render all non-directional lights shadow
        void RenderOtherShadows()
        {
            // Create RenderTexture, and set it as shadow map
            int atlasSize = (int)settings.other.atlasSize;
            atalsSizes.z = atlasSize;
            atalsSizes.w = 1.0f / atlasSize;
            buffer.GetTemporaryRT(otherShadowAtlasId, atlasSize, atlasSize, 32, 
                FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            
            // Storage render target data into render texture
            buffer.SetRenderTarget(otherShadowAtlasId, 
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            
            // Clear depth buffer
            buffer.ClearRenderTarget(true, false, Color.clear);
            buffer.SetGlobalFloat(shadowPancakingId, 0.0f);
            buffer.BeginSample(bufferName);
            ExecuteBuffer();
            // Size and count of split texture
            int tiles = shadowedOtherLightCount;
            int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
            int tileSize = atlasSize / split;
            // Iterate all dir lights
            for (int i = 0; i < shadowedOtherLightCount;)
            {
                if (shadowedOtherLights[i].IsPoint)
                {
                    RenderPointShadows(i, split, tileSize);
                    i += 6;
                }
                else
                {
                    RenderSpotShadows(i, split, tileSize);
                    i++;
                }
            }
            
            // Shadow transform matrices sen to GPU
            buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
            buffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);
            // Set PCF mode keywords
            SetKeywords(otherFilterKeywords, (int)settings.other.filter - 1);
            
            buffer.EndSample(bufferName);
            ExecuteBuffer();
        }

        void RenderPointShadows(int index, int split, int tileSize)
        {
            ShadowedOtherLight light = shadowedOtherLights[index];
            var shadowSettings = new ShadowDrawingSettings(cullingResults, light.VisibleLightIndex)
            {
                useRenderingLayerMaskTest = true
            };
            float texelSize = 2.0f / tileSize;
            float filterSize = texelSize * ((float)settings.other.filter + 1.0f);
            // Calculate normal bias
            float bias = light.NormalBias * filterSize * 1.4142136f;
            float tileScale = 1.0f / split;

            for (int i = 0; i < 6; i++)
            {
                float fovBias = Mathf.Atan(1.0f + bias + filterSize) * Mathf.Rad2Deg * 2.0f - 90.0f;
                cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
                    light.VisibleLightIndex, (CubemapFace)i, fovBias, 
                    out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);
                viewMatrix.m11 = -viewMatrix.m11;
                viewMatrix.m12 = -viewMatrix.m12;
                viewMatrix.m13 = -viewMatrix.m13;
                shadowSettings.splitData = splitData;
                int tileIndex = index + i;
                Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
                SetOtherTileData(tileIndex, offset, tileScale, bias);
                otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                    projMatrix * viewMatrix, offset, tileScale);
                // Set view projection matrices
                buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
                // Set slope scale bias
                buffer.SetGlobalDepthBias(0.0f, light.SlopeScaleBias);
                // Draw shadow
                ExecuteBuffer();
                context.DrawShadows(ref shadowSettings);
                buffer.SetGlobalDepthBias(0.0f, 0.0f);
            }
        }

        void RenderSpotShadows(int index, int split, int tileSize)
        {
            ShadowedOtherLight light = shadowedOtherLights[index];
            var shadowSettings = new ShadowDrawingSettings(cullingResults, light.VisibleLightIndex)
            {
                useRenderingLayerMaskTest = true
            };
            cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(light.VisibleLightIndex,
                out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);
            shadowSettings.splitData = splitData;
            // Calculate normal bias
            float texelSize = 2.0f / (tileSize * projMatrix.m00);
            float filterSize = texelSize * ((float)settings.other.filter + 1.0f);
            float bias = light.NormalBias * filterSize * 1.4142136f;
            float tileScale = 1.0f / split;
            Vector2 offset = SetTileViewport(index, split, tileSize);
            SetOtherTileData(index, offset, tileScale, bias);
            otherShadowMatrices[index] = 
                ConvertToAtlasMatrix(projMatrix * viewMatrix, offset, tileScale);
            // Set view projection matrices
            buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
            // Set slope scale bias
            buffer.SetGlobalDepthBias(0.0f, light.SlopeScaleBias);
            // Draw shadow
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0.0f, 0.0f);
        }
        
        // Change render viewport to render single tile
        Vector2 SetTileViewport(int index, int split, float tileSize)
        {
            // Get tile offset
            Vector2 offset = new Vector2(index % split, index / split);
            // Set render viewport
            buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
            return offset;
        }
        
        // Return a transform matrix from world space to shadow map viewport space
        Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float scale)
        {
            // If use reversed Z buffer (DirectX)
            if (SystemInfo.usesReversedZBuffer)
            {
                m.m20 = -m.m20;
                m.m21 = -m.m21;
                m.m22 = -m.m22;
                m.m23 = -m.m23;
            }
            
            m.m00 = ((m.m00 + m.m30) * 0.5f + m.m30 * offset.x) * scale;
            m.m01 = ((m.m01 + m.m31) * 0.5f + m.m31 * offset.x) * scale;
            m.m02 = ((m.m02 + m.m32) * 0.5f + m.m32 * offset.x) * scale;
            m.m03 = ((m.m03 + m.m33) * 0.5f + m.m33 * offset.x) * scale;
            m.m10 = ((m.m10 + m.m30) * 0.5f + m.m30 * offset.y) * scale;
            m.m11 = ((m.m11 + m.m31) * 0.5f + m.m31 * offset.y) * scale;
            m.m12 = ((m.m12 + m.m32) * 0.5f + m.m32 * offset.y) * scale;
            m.m13 = ((m.m13 + m.m33) * 0.5f + m.m33 * offset.y) * scale;
            m.m20 = (m.m20 + m.m30) * 0.5f;
            m.m21 = (m.m21 + m.m31) * 0.5f;
            m.m22 = (m.m22 + m.m32) * 0.5f;
            m.m23 = (m.m23 + m.m33) * 0.5f;
            
            return m;
        }

        void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
        {
            // Get pixel size
            float texelSize = 2.0f * cullingSphere.w / tileSize;
            float filterSize = texelSize * ((float)settings.directional.filter + 1.0f);
            cullingSphere.w -= filterSize;
            // Get square of sphere radius
            cullingSphere.w *= cullingSphere.w;
            cascadeCullingSpheres[index] = cullingSphere;
            cascadeData[index] = new Vector4(1.0f / cullingSphere.w, filterSize * 1.4142136f);
        }
        
        // Storage non-directional light shadow atlas data
        void SetOtherTileData(int index, Vector2 offset, float scale, float bias)
        {
            float border = atalsSizes.w * 0.5f;
            Vector4 data;
            data.x = offset.x * scale + border;
            data.y = offset.y * scale + border;
            data.z = scale - border - border;
            data.w = bias;
            otherShadowTiles[index] = data;
        }
        
        // Set keyword to choose which PCF mode will be opened
        void SetKeywords(string[] keywords, int enableIndex)
        {
            for (int i = 0; i < keywords.Length; i++)
            {
                if (i == enableIndex)
                {
                    buffer.EnableShaderKeyword(keywords[i]);
                }
                else
                {
                    buffer.DisableShaderKeyword(keywords[i]);
                }
            }
        }
    }
}