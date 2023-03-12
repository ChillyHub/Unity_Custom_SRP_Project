using CustomRP.Common;
using CustomRP.Settings;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime
{
    public class Lighting
    {
        private const string bufferName = "Lighting";

        private CommandBuffer buffer = new CommandBuffer
        {
            name = bufferName
        };

        private const int maxDirLightCount = 4;
        private const int maxOtherLightCount = 64;

        private static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
        private static int dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
        private static int dirLightDirectionsAndMasksId = 
            Shader.PropertyToID("_DirectionalLightDirectionsAndMasks");
        private static int dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
        private static int otherLightCountId = Shader.PropertyToID("_OtherLightCount");
        private static int otherLightColorsId = Shader.PropertyToID("_OtherLightColors");
        private static int otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions");
        private static int otherLightDirectionsAndMasksId = 
            Shader.PropertyToID("_OtherLightDirectionsAndMasks");
        private static int otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles");
        private static int otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");

        private static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
        private static Vector4[] dirLightDirectionsAndMasks = new Vector4[maxDirLightCount];
        private static Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];
        private static Vector4[] otherLightColors = new Vector4[maxOtherLightCount];
        private static Vector4[] otherLightPositions = new Vector4[maxOtherLightCount];
        private static Vector4[] otherLightDirectionsAndMasks = new Vector4[maxOtherLightCount];
        private static Vector4[] otherLightSpotAngles = new Vector4[maxOtherLightCount];
        private static Vector4[] otherLightShadowData = new Vector4[maxOtherLightCount];

        private static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";

        private ScriptableRenderContext context;

        private CullingResults cullingResults;

        private Shadows shadows = new Shadows();

        public void Setup(ScriptableRenderContext context, CullingResults cullingResults, 
            ShadowSettings shadowSettings, bool useLightsPerObject, int renderingLayerMask)
        {
            this.context = context;
            this.cullingResults = cullingResults;
            buffer.BeginSample(bufferName);
            // Submit shadow data
            shadows.Setup(context, cullingResults, shadowSettings);
            // Submit light data
            SetupLights(useLightsPerObject, renderingLayerMask);
            
            shadows.Render();
            buffer.EndSample(bufferName);
            ExecuteBuffer();
        }

        public void Cleanup()
        {
            shadows.Cleanup();
        }
        
        void ExecuteBuffer()
        {
            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        private void SetupDirectionalLight(int index, int visibleIndex, ref VisibleLight visibleLight,
            Light light)
        {
            dirLightColors[index] = visibleLight.finalColor;
            Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
            dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
            dirLightDirectionsAndMasks[index] = dirAndMask;
            
            // Storage shadow data
            dirLightShadowData[index] = 
                shadows.ReserveDirectionalShadows(light, visibleIndex);
        }

        private void SetupPointLight(int index, int visibleIndex, ref VisibleLight visibleLight,
            Light light)
        {
            otherLightColors[index] = visibleLight.finalColor;
            // Position data is in the last column of the ObjectToWorld matrix
            Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
            // Storage light range square invert in position.w
            position.w = 1.0f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
            otherLightPositions[index] = position;
            otherLightSpotAngles[index] = new Vector4(0.0f, 1.0f);

            Vector4 dirAndMask = Vector4.zero;
            dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
            otherLightDirectionsAndMasks[index] = dirAndMask;
            otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
        }

        private void SetupSpotLight(int index, int visibleIndex, ref VisibleLight visibleLight,
            Light light)
        {
            otherLightColors[index] = visibleLight.finalColor;
            Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
            position.w = 1.0f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
            otherLightPositions[index] = position;
            Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
            dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
            otherLightDirectionsAndMasks[index] = dirAndMask;
            
            float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
            float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
            float angleRangeInv = 1.0f / Mathf.Max(innerCos - outerCos, 0.001f);
            otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
            otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
        }

        private void SetupLights(bool useLightsPerObject, int renderingLayerMask)
        {
            // Get lights index table
            NativeArray<int> indexMap = useLightsPerObject ? 
                cullingResults.GetLightIndexMap(Allocator.Temp) : default;

            // Get all visible lights
            NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;

            int i;
            int dirLightCount = 0, otherLightCount = 0;
            for (i = 0; i < visibleLights.Length; i++)
            {
                int newIndex = -1;
                VisibleLight visibleLight = visibleLights[i];
                Light light = visibleLight.light;
                if ((light.renderingLayerMask & renderingLayerMask) != 0)
                {
                    switch (visibleLight.lightType)
                    {
                        case LightType.Directional:
                            if (dirLightCount < maxDirLightCount)
                            {
                                SetupDirectionalLight(dirLightCount++, i, ref visibleLight, light);
                            }
                            break;
                        case LightType.Point:
                            if (otherLightCount < maxOtherLightCount)
                            {
                                newIndex = otherLightCount;
                                SetupPointLight(otherLightCount++, i, ref visibleLight, light);
                            }
                            break;
                        case LightType.Spot:
                            if (otherLightCount < maxOtherLightCount)
                            {
                                newIndex = otherLightCount;
                                SetupSpotLight(otherLightCount++, i, ref visibleLight, light);
                            }
                            break;
                    }
                }

                if (useLightsPerObject)
                {
                    indexMap[i] = newIndex;
                }
            }
            
            // Delete all un visible light's index
            if (useLightsPerObject)
            {
                for (; i < indexMap.Length; i++)
                {
                    indexMap[i] = -1;
                }
                
                cullingResults.SetLightIndexMap(indexMap);
                indexMap.Dispose();
                
                Shader.EnableKeyword(lightsPerObjectKeyword);
            }
            else
            {
                Shader.DisableKeyword(lightsPerObjectKeyword);
            }
            
            buffer.SetGlobalInt(dirLightCountId, dirLightCount);
            if (dirLightCount > 0)
            {
                buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
                buffer.SetGlobalVectorArray(dirLightDirectionsAndMasksId, dirLightDirectionsAndMasks);
                buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
            }
            
            buffer.SetGlobalInt(otherLightCountId, otherLightCount);
            if (otherLightCount > 0)
            {
                buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
                buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
                buffer.SetGlobalVectorArray(otherLightDirectionsAndMasksId, 
                    otherLightDirectionsAndMasks);
                buffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
                buffer.SetGlobalVectorArray(otherLightShadowDataId, otherLightShadowData);
            }
        }
    }
}