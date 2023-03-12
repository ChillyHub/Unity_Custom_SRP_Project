using CustomRP.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime
{
    [CreateAssetMenu(menuName = "Rendering/CreateCustomRenderPipeline")]
    public partial class CustomRenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField] 
        private bool useDynamicBatching = true, 
            useGPUInstancing = true, 
            useSRPBatcher = true,
            useLightsPerObject = true;

        [SerializeField] private Shader cameraRendererShader = default;
        
        // Camera buffer setting
        [SerializeField] private CameraBufferSettings cameraBuffer = 
            new CameraBufferSettings
            {
                allowHDR = true,
                renderScale = 1.0f,
                fxaa = new CameraBufferSettings.FXAA
                {
                    fixedThreshold = 0.0833f,
                    relativeThreshold = 0.166f,
                    subpixelBlending = 0.75f
                }
            };

        [SerializeField] private ShadowSettings shadows = default;

        // Post FX asset setting
        [SerializeField] private PostFXSettings postFXSettings = default;

        public enum ColorLUTResolution
        {
            _16 = 16,
            _32 = 32,
            _64 = 64
        }
        
        // LUT resolution
        [SerializeField] 
        private ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;
        
        protected override RenderPipeline CreatePipeline()
        {
            return new CustomRenderPipeline(cameraBuffer, useDynamicBatching, useGPUInstancing, 
                useSRPBatcher, useLightsPerObject, shadows, postFXSettings, (int)colorLUTResolution,
                cameraRendererShader);
        }
    }
}

