using CustomRP.Runtime;
using CustomRP.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime
{
    public partial class CustomRenderPipeline : RenderPipeline
    {
        private CameraRenderer renderer;
        private bool useDynamicBatching, useGPUInstancing, useLightsPerObject;
        private ShadowSettings shadowSettings;
        private PostFXSettings postFXSettings;
        private CameraBufferSettings cameraBufferSettings;
        private int colorLUTResolution;
        
        // Use SRP Batching when construct pipeline
        public CustomRenderPipeline(CameraBufferSettings cameraBufferSettings, bool useDynamicBatching, 
            bool useGPUInstancing, bool useSRPBatcher, bool useLightsPerObject, 
            ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLutResolution, 
            Shader cameraRendererShader)
        {
            this.cameraBufferSettings = cameraBufferSettings;
            this.useDynamicBatching = useDynamicBatching;
            this.useGPUInstancing = useGPUInstancing;
            this.useLightsPerObject = useLightsPerObject;
            this.shadowSettings = shadowSettings;
            this.postFXSettings = postFXSettings;
            this.colorLUTResolution = colorLutResolution;
            GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;
            
            InitializeForEditor();

            renderer = new CameraRenderer(cameraRendererShader);
        }
    
        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach (Camera camera in cameras)
            {
                renderer.Render(context, camera, cameraBufferSettings, useDynamicBatching, 
                    useGPUInstancing, useLightsPerObject, shadowSettings, postFXSettings, 
                    colorLUTResolution);
            }
        }
    }
}
