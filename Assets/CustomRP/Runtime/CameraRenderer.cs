using CustomRP.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime
{
    public partial class CameraRenderer
    {
        private ScriptableRenderContext context;
        private Camera camera;

        private const string bufferName = "Render Camera";

        private CommandBuffer buffer = new CommandBuffer
        {
            name = bufferName
        };

        private CullingResults cullingResults;

        private Material material;
        private Lighting lighting = new Lighting();
        private PostFXStack postFXStack = new PostFXStack();

        private Texture2D missingTexture;

        private bool useHDR;
        private bool useDepthTexture;
        private bool useColorTexture;
        private bool useIntermediateBuffer;
        private bool useScaledRendering;
        
        // Final used buffer size
        private Vector2Int bufferSize;

        private static int colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment");
        private static int depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment");
        private static int depthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        private static int colorTextureId = Shader.PropertyToID("_CameraColorTexture");
        private static int sourceTextureId = Shader.PropertyToID("_SourceTexture");
        private static int srcBlendId = Shader.PropertyToID("_CameraSrcBlend");
        private static int dstBlendId = Shader.PropertyToID("_CameraDstBlend");
        private static int bufferSizeId = Shader.PropertyToID("_CameraBufferSize");

        private static CameraSettings defaultCameraSettings = new CameraSettings();

        private static bool copyTextureSuppoted = 
            SystemInfo.copyTextureSupport > CopyTextureSupport.None;

        public CameraRenderer(Shader shader)
        {
            material = CoreUtils.CreateEngineMaterial(shader);
            missingTexture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "Missing"
            };
            missingTexture.SetPixel(0, 0, Color.white * 0.5f);
            missingTexture.Apply(true, true);
        }

        public void Render(ScriptableRenderContext context, Camera camera, 
            CameraBufferSettings bufferSettings, bool useDynamicBatching, bool useGPUInstancing, 
            bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings, 
            int colorLUTResolution)
        {
            this.context = context;
            this.camera = camera;
            var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
            CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : defaultCameraSettings;
            if (camera.cameraType == CameraType.Reflection)
            {
                useDepthTexture = bufferSettings.copyDepthReflection;
                useColorTexture = bufferSettings.copyColorReflection;
            }
            else
            {
                useDepthTexture = bufferSettings.copyDepth && cameraSettings.copyDepth;
                useColorTexture = bufferSettings.copyColor && cameraSettings.copyColor;
            }
            // If need to override post FX setting, change it
            if (cameraSettings.overridePostFX)
            {
                postFXSettings = cameraSettings.postFXSettings;
            }
            
            // Set command buffer name
            float renderScale = cameraSettings.GetRenderScale(bufferSettings.renderScale);
            useScaledRendering = renderScale < 0.99f || renderScale > 1.01f;
            PrepareBuffer();
            PrepareForSceneWindow();
            
            if (!Cull(shadowSettings.maxDistance))
                return;

            useHDR = bufferSettings.allowHDR && camera.allowHDR;
            // Zoom screen size by scale
            if (useScaledRendering)
            {
                renderScale = Mathf.Clamp(renderScale, 0.1f, 2.0f);
                bufferSize.x = (int)(camera.pixelWidth * renderScale);
                bufferSize.y = (int)(camera.pixelHeight * renderScale);
            }
            else
            {
                bufferSize.x = camera.pixelWidth;
                bufferSize.y = camera.pixelHeight;
            }
            
            buffer.BeginSample(SampleName);
            buffer.SetGlobalVector(bufferSizeId, 
                new Vector4(1.0f / bufferSize.x, 1.0f / bufferSize.y, bufferSize.x, bufferSize.y));
            ExecuteBuffer();
            lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject,
                cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);
            bufferSettings.fxaa.enabled &= cameraSettings.allowFXAA;
            postFXStack.Setup(context, camera, bufferSize, postFXSettings, cameraSettings.keepAlpha, 
                useHDR, colorLUTResolution, cameraSettings.finalBlendMode, 
                bufferSettings.bicubicRescaling, bufferSettings.fxaa);
            buffer.EndSample(SampleName);
            
            Setup();
            DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject,
                cameraSettings.renderingLayerMask);
            DrawUnsupportedShaders();
            
            DrawGizmosBeforeFX();
            if (postFXStack.IsActive)
            {
                postFXStack.Render(colorAttachmentId);
            }
            else if (useIntermediateBuffer)
            {
                DrawFinal(cameraSettings.finalBlendMode);
                ExecuteBuffer();
            }
            DrawGizmosAfterFX();
            
            Cleanup();
            
            Submit();
        }
        
        public void Dispose()
        {
            CoreUtils.Destroy(material);
            CoreUtils.Destroy(missingTexture);
        }
        
        // Set Camera's properties and Matrix
        private void Setup()
        {
            context.SetupCameraProperties(camera);
            CameraClearFlags flags = camera.clearFlags;

            useIntermediateBuffer = useScaledRendering || useDepthTexture || useColorTexture || 
                                    postFXStack.IsActive;
            if (useIntermediateBuffer)
            {
                if (flags > CameraClearFlags.Color)
                {
                    flags = CameraClearFlags.Color;
                }
                buffer.GetTemporaryRT(
                    colorAttachmentId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, 
                    useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
                buffer.GetTemporaryRT(depthAttachmentId, bufferSize.x, bufferSize.y, 32,
                    FilterMode.Point, RenderTextureFormat.Depth);
                buffer.SetRenderTarget(
                    colorAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    depthAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            }
            buffer.ClearRenderTarget(
                flags <= CameraClearFlags.Depth, 
                flags == CameraClearFlags.Color,
                flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
            buffer.BeginSample(SampleName);
            buffer.SetGlobalTexture(depthTextureId, missingTexture);
            buffer.SetGlobalTexture(colorTextureId, missingTexture);
            ExecuteBuffer();
        }
        
        private void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing, 
            bool useLightsPerObject, int renderingLayerMask)
        {
            PerObjectData lightsPerObjectFlags = useLightsPerObject
                ? PerObjectData.LightData | PerObjectData.LightIndices
                : PerObjectData.None;
            // Set draw sorting and set render camera
            var sortingSettings = new SortingSettings(camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };
            
            // Set render pass and sorting mode
            var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
            {
                // Set state of batching when rendering
                enableDynamicBatching = useDynamicBatching,
                enableInstancing = useGPUInstancing, 
                perObjectData = PerObjectData.Lightmaps | PerObjectData.ShadowMask | 
                                PerObjectData.LightProbe | PerObjectData.OcclusionProbe | 
                                PerObjectData.LightProbeProxyVolume | 
                                PerObjectData.OcclusionProbeProxyVolume | 
                                PerObjectData.ReflectionProbes | lightsPerObjectFlags
            };
            drawingSettings.SetShaderPassName(1, litShaderTagId);
            
            // Set drawable in render queue
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque,
                renderingLayerMask: (uint)renderingLayerMask);
            
            // 1. Draw Opaque
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
            
            // 2. Draw Skybox
            context.DrawSkybox(camera);

            if (useDepthTexture || useColorTexture)
            {
                CopyAttachments();
            }

            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.transparent;
            
            // 3. Draw Transparent
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        }

        void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, bool isDepth = false)
        {
            buffer.SetGlobalTexture(sourceTextureId, from);
            buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.DrawProcedural(Matrix4x4.identity, material, isDepth ? 1 : 0, 
                MeshTopology.Triangles, 3);
        }

        void DrawFinal(CameraSettings.FinalBlendMode finalBlendMode)
        {
            buffer.SetGlobalFloat(srcBlendId, (float)finalBlendMode.source);
            buffer.SetGlobalFloat(dstBlendId, (float)finalBlendMode.destination);
            buffer.SetGlobalTexture(sourceTextureId, colorAttachmentId);
            buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,
                finalBlendMode.destination == BlendMode.Zero ? 
                    RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
                RenderBufferStoreAction.Store);
            buffer.SetViewport(camera.pixelRect);
            buffer.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
            buffer.SetGlobalFloat(srcBlendId, 1.0f);
            buffer.SetGlobalFloat(dstBlendId, 0.0f);
        }

        private void Submit()
        {
            buffer.EndSample(SampleName);
            ExecuteBuffer();
            context.Submit();
        }

        private void ExecuteBuffer()
        {
            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        private bool Cull(float maxShadowDistance)
        {
            ScriptableCullingParameters p;

            if (camera.TryGetCullingParameters(out p))
            {
                p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
                cullingResults = context.Cull(ref p);
                return true;
            }

            return false;
        }

        private void Cleanup()
        {
            lighting.Cleanup();
            
            if (useIntermediateBuffer)
            {
                // Release color and depth texture
                buffer.ReleaseTemporaryRT(colorAttachmentId);
                buffer.ReleaseTemporaryRT(depthAttachmentId);
                // Release tmp depth texture
                if (useDepthTexture)
                {
                    buffer.ReleaseTemporaryRT(depthTextureId);
                }

                if (useColorTexture)
                {
                    buffer.ReleaseTemporaryRT(colorTextureId);
                }
            }
        }

        private void CopyAttachments()
        {
            if (useDepthTexture)
            {
                buffer.GetTemporaryRT(depthTextureId, bufferSize.x, bufferSize.y, 32,
                    FilterMode.Point, RenderTextureFormat.Depth);
                if (copyTextureSuppoted)
                {
                    buffer.CopyTexture(depthAttachmentId, depthTextureId);
                }
                else
                {
                    Draw(depthAttachmentId, depthTextureId, true);
                }
            }

            if (useColorTexture)
            {
                buffer.GetTemporaryRT(
                    colorTextureId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, 
                    useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
                if (copyTextureSuppoted)
                {
                    buffer.CopyTexture(colorAttachmentId, colorTextureId);
                }
                else
                {
                    Draw(colorAttachmentId, colorTextureId);
                }
            }

            if (!copyTextureSuppoted)
            {
                buffer.SetRenderTarget(
                    colorAttachmentId, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                    depthAttachmentId, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            }
            ExecuteBuffer();
        }
    }
}