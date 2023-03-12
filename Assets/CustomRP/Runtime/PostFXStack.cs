using CustomRP.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime
{
    using static PostFXSettings;
    
    public partial class PostFXStack
    {
        private const string bufferName = "Post FX";

        private CommandBuffer buffer = new CommandBuffer
        {
            name = bufferName
        };

        private ScriptableRenderContext context;


        private Camera camera;
        private PostFXSettings settings;

        private bool keepAlpha, useHDR;
        private int colorLUTResolution;

        private Vector2Int bufferSize;

        private CameraSettings.FinalBlendMode finalBlendMode;
        private CameraBufferSettings.BicubicRescalingMode bicubicRescaling;
        private CameraBufferSettings.FXAA fxaa;

        enum Pass
        {
            Copy,
            BloomHorizontal,
            BloomVertical,
            BloomAdd,
            BloomScatter,
            BloomScatterFinal,
            BloomPrefilter,
            BloomPrefilterFireflies,
            ColorGradingNone,
            ColorGradingACES,
            ColorGradingNeutral,
            ColorGradingReinhard,
            ApplyColorGrading,
            ApplyColorGradingWithLuma,
            FXAA,
            FXAAWithLuma,
            FinalRescale
        }

        private int fxSourceId = Shader.PropertyToID("_PostFXSource");
        private int fxSource2Id = Shader.PropertyToID("_PostFXSource2");
        private int finalSrcBlendId = Shader.PropertyToID("_FinalSrcBlend");
        private int finalDstBlendId = Shader.PropertyToID("_FinalDstBlend");
        private int copyBicubicId = Shader.PropertyToID("_CopyBicubic");

        public bool IsActive => settings != null && settings.isActive;

        public PostFXStack()
        {
            bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
            for (int i = 1; i < maxBloomPayramidLevels * 2; i++)
            {
                Shader.PropertyToID("_BloomPyramid" + i);
            }
        }

        public void Setup(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, 
            PostFXSettings settings, bool keepAlpha, bool useHDR, int colorLUTResolution, 
            CameraSettings.FinalBlendMode finalBlendMode, 
            CameraBufferSettings.BicubicRescalingMode bicubicRescaling,
            CameraBufferSettings.FXAA fxaa)
        {
            this.context = context;
            this.camera = camera;
            this.bufferSize = bufferSize;
            this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
            this.keepAlpha = keepAlpha;
            this.useHDR = useHDR;
            this.colorLUTResolution = colorLUTResolution;
            this.finalBlendMode = finalBlendMode;
            this.bicubicRescaling = bicubicRescaling;
            this.fxaa = fxaa;
            
            ApplySceneViewState();
        }

        public void Render(int sourceId)
        {
            if (DoBloom(sourceId))
            {
                DoFinal(bloomResultId);
                buffer.ReleaseTemporaryRT(bloomResultId);
            }
            else
            {
                DoFinal(sourceId);
            }
            
            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
        {
            buffer.SetGlobalTexture(fxSourceId, from);
            buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, 
                MeshTopology.Triangles, 3);
        }
        
        void DrawFinal(RenderTargetIdentifier from, Pass pass)
        {
            buffer.SetGlobalFloat(finalSrcBlendId, (float)finalBlendMode.source);
            buffer.SetGlobalFloat(finalDstBlendId, (float)finalBlendMode.destination);
            buffer.SetGlobalTexture(fxSourceId, from);
            buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, 
                finalBlendMode.destination == BlendMode.Zero ? RenderBufferLoadAction.DontCare : 
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            // Set viewport
            buffer.SetViewport(camera.pixelRect);
            buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, 
                MeshTopology.Triangles, 3);
        }
        
        #region Bloom

        private const int maxBloomPayramidLevels = 16;

        private int bloomPyramidId;
        
        private int bloomBucubicUpSamplingId = Shader.PropertyToID("_BloomBicubicUpSampling");
        private int bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter");
        private int bloomThresholdId = Shader.PropertyToID("_BloomThreshold");
        private int bloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        private int bloomResultId = Shader.PropertyToID("_BloomResult");

        bool DoBloom(int sourceId)
        {
            PostFXSettings.BloomSettings bloom = settings.Bloom;
            int width, height;
            if (bloom.ignoreRenderScale)
            {
                width = camera.pixelWidth / 2;
                height = camera.pixelHeight / 2;
            }
            else
            {
                width = bufferSize.x / 2;
                height = bufferSize.y / 2;
            }
            if (bloom.maxIterations == 0 || bloom.intensity <= 0.0f ||
                height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2)
            {
                return false;
            }
            buffer.BeginSample("Bloom");

            Vector4 threshold;
            threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
            threshold.y = threshold.x * bloom.thresholdKnee;
            threshold.z = 2.0f * threshold.y;
            threshold.w = 0.25f / (threshold.y + 0.0000025f);
            threshold.y -= threshold.x;
            buffer.SetGlobalVector(bloomThresholdId, threshold);
            
            RenderTextureFormat format = 
                useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            buffer.GetTemporaryRT(bloomPrefilterId, bufferSize.x, bufferSize.y, 0, 
                FilterMode.Bilinear, format);
            Draw(sourceId, bloomPrefilterId, 
                bloom.fadeFireflies ? Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);
            width /= 2;
            height /= 2;
            
            int fromId = bloomPrefilterId;
            int toId = bloomPyramidId + 1;
            int i;
            for (i = 0; i < bloom.maxIterations; i++)
            {
                if (height < bloom.downscaleLimit || width < bloom.downscaleLimit)
                    break;

                int midId = toId - 1;
                buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);
                buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);
                Draw(fromId, midId, Pass.BloomHorizontal);
                Draw(midId, toId, Pass.BloomVertical);
                fromId = toId;
                toId += 2;
                width /= 2;
                height /= 2;
            }
            buffer.ReleaseTemporaryRT(bloomPrefilterId);
            buffer.SetGlobalFloat(bloomBucubicUpSamplingId, bloom.bicubicUpSampling ? 1.0f : 0.0f);
            //buffer.SetGlobalFloat(bloomIntensityId, 1.0f);
            Pass combinePass, finalPass;
            float finalIntensity;
            if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive)
            {
                combinePass = finalPass = Pass.BloomAdd;
                buffer.SetGlobalFloat(bloomIntensityId, 1.0f);
                finalIntensity = bloom.intensity;
            }
            else
            {
                combinePass = Pass.BloomScatter;
                finalPass = Pass.BloomScatterFinal;
                buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
                finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
            }
            if (i > 1)
            {
                buffer.ReleaseTemporaryRT(fromId - 1);
                toId -= 5;
                for (i -= 1; i > 0; i--)
                {
                    buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                    Draw(fromId, toId, combinePass);
                    buffer.ReleaseTemporaryRT(fromId);
                    buffer.ReleaseTemporaryRT(toId + 1);
                    fromId = toId;
                    toId -= 2;
                }
            }
            else
            {
                buffer.ReleaseTemporaryRT(bloomPyramidId);
            }
            buffer.SetGlobalFloat(bloomIntensityId, finalIntensity);
            buffer.SetGlobalTexture(fxSource2Id, sourceId);
            buffer.GetTemporaryRT(bloomResultId, camera.pixelWidth, camera.pixelHeight, 0,
                FilterMode.Bilinear, format);
            Draw(fromId, bloomResultId, finalPass);
            buffer.ReleaseTemporaryRT(fromId);
            buffer.EndSample("Bloom");
            return true;
        }

        #endregion

        #region Color Grading, Tone Mapping and FXAA

        private int colorAdjustmentsId = Shader.PropertyToID("_ColorAdjustments");
        private int colorFilterId = Shader.PropertyToID("_ColorFilter");
        private int whiteBalanceId = Shader.PropertyToID("_WhiteBalance");
        private int splitToningShadowsId = Shader.PropertyToID("_SplitToningShadows");
        private int splitToningHighlightsId = Shader.PropertyToID("_SplitToningHighlights");
        private int channelMixerRedId = Shader.PropertyToID("_ChannelMixerRed");
        private int channelMixerGreenId = Shader.PropertyToID("_ChannelMixerGreen");
        private int channelMixerBlueId = Shader.PropertyToID("_ChannelMixerBlue");
        private int smhShadowsId = Shader.PropertyToID("_SMHShadows");
        private int smhMidTonesId = Shader.PropertyToID("_SMHMidTones");
        private int smhHighlightsId = Shader.PropertyToID("_SMHHighlights");
        private int smhRangeId = Shader.PropertyToID("_SMHRange");
        private int colorGradingLUTId = Shader.PropertyToID("_ColorGradingLUT");
        private int colorGradingLUTParametersId = Shader.PropertyToID("_ColorGradingLUTParameters");
        private int colorGradingLUTInLogId = Shader.PropertyToID("_ColorGradingLUTInLogC");
        private int colorGradingResultId = Shader.PropertyToID("_ColorGradingResult");
        private int fxaaConfigId = Shader.PropertyToID("_FXAAConfig");
        private int finalResultId = Shader.PropertyToID("_FinalResultId");

        private const string fxaaQualityLowKeyword = "FXAA_QUALITY_LOW";
        private const string fxaaQualityMediumKeyword = "FXAA_QUALITY_MEDIUM";

        void DoFinal(int sourceId)
        {
            ConfigureColorAdjustments();
            ConfigureWhiteBalance();
            ConfigureSplitToning();
            ConfigureChannelMixer();
            ConfigureShadowsMidTonesHighlights();
            int lutHeight = colorLUTResolution;
            int lutWidth = lutHeight * lutHeight;
            buffer.GetTemporaryRT(colorGradingLUTId, lutWidth, lutHeight, 0, 
                FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
            buffer.SetGlobalVector(colorGradingLUTParametersId, new Vector4(
                lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1.0f)));
            ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
            Pass pass = Pass.ColorGradingNone + (int)mode;
            buffer.SetGlobalFloat(colorGradingLUTInLogId, 
                useHDR && pass != Pass.ColorGradingNone ? 1.0f : 0.0f);
            Draw(sourceId, colorGradingLUTId, pass);
            
            buffer.SetGlobalVector(colorGradingLUTParametersId, 
                new Vector4(1.0f / lutWidth, 1.0f / lutHeight, lutHeight - 1.0f));
            buffer.SetGlobalFloat(finalSrcBlendId, 1.0f);
            buffer.SetGlobalFloat(finalDstBlendId, 0.0f);
            if (fxaa.enabled)
            {
                ConfigureFXAA();
                buffer.GetTemporaryRT(colorGradingResultId, bufferSize.x, bufferSize.y, 0,
                    FilterMode.Bilinear, RenderTextureFormat.Default);
                Draw(sourceId, colorGradingResultId, 
                    keepAlpha ? Pass.ApplyColorGrading : Pass.ApplyColorGradingWithLuma);
            }
            
            if (bufferSize.x == camera.pixelWidth)
            {
                if (fxaa.enabled)
                {
                    DrawFinal(colorGradingResultId, keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma);
                    buffer.ReleaseTemporaryRT(colorGradingResultId);
                }
                else
                {
                    DrawFinal(sourceId, Pass.ApplyColorGrading);
                }
            }
            else
            {
                buffer.GetTemporaryRT(finalResultId, bufferSize.x, bufferSize.y, 0, 
                    FilterMode.Bilinear, RenderTextureFormat.Default);

                if (fxaa.enabled)
                {
                    Draw(colorGradingResultId, finalResultId, keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma);
                    buffer.ReleaseTemporaryRT(colorGradingResultId);
                }
                else
                {
                    Draw(sourceId, finalResultId, Pass.ApplyColorGrading);
                }
                
                bool bicubicSampling =
                    bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UpAndDown ||
                    bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UpOnly &&
                    bufferSize.x < camera.pixelWidth;
                buffer.SetGlobalFloat(copyBicubicId, bicubicSampling ? 1.0f : 0.0f);
                DrawFinal(finalResultId, Pass.FinalRescale);
                buffer.ReleaseTemporaryRT(finalResultId);
            }
            buffer.ReleaseTemporaryRT(colorGradingLUTId);
        }
        
        // Get color grading configure
        void ConfigureColorAdjustments()
        {
            ColorAdjustmentsSettings colorAdjustments = settings.ColorAdjustments;
            buffer.SetGlobalVector(colorAdjustmentsId, new Vector4(
                Mathf.Pow(2.0f, colorAdjustments.postExposure),
                colorAdjustments.contrast * 0.01f + 1.0f,
                colorAdjustments.hueShift * (1.0f / 360.0f),
                colorAdjustments.saturation * 0.01f + 1.0f));
            buffer.SetGlobalColor(colorFilterId, colorAdjustments.colorFilter.linear);
        }

        void ConfigureWhiteBalance()
        {
            WhiteBalanceSettings whiteBalance = settings.WhiteBalance;
            buffer.SetGlobalVector(whiteBalanceId, ColorUtils.ColorBalanceToLMSCoeffs(
                whiteBalance.temperature, whiteBalance.tint));
        }

        void ConfigureSplitToning()
        {
            SplitToningSettings splitToning = settings.SplitToning;
            Color splitColor = splitToning.shadows;
            splitColor.a = splitToning.balance * 0.01f;
            buffer.SetGlobalColor(splitToningShadowsId, splitColor);
            buffer.SetGlobalColor(splitToningHighlightsId, splitToning.highlights);
        }

        void ConfigureChannelMixer()
        {
            ChannelMixerSettings channelMixer = settings.ChannelMixer;
            buffer.SetGlobalVector(channelMixerRedId, channelMixer.red);
            buffer.SetGlobalVector(channelMixerGreenId, channelMixer.green);
            buffer.SetGlobalVector(channelMixerBlueId, channelMixer.blue);
        }

        void ConfigureShadowsMidTonesHighlights()
        {
            ShadowsMidTonesHighlightsSettings smh = settings.ShadowsMidTonesHighlights;
            buffer.SetGlobalColor(smhShadowsId, smh.shadows.linear);
            buffer.SetGlobalColor(smhMidTonesId, smh.midTones.linear);
            buffer.SetGlobalColor(smhHighlightsId, smh.highlights.linear);
            buffer.SetGlobalVector(smhRangeId, 
                new Vector4(smh.shadowsStart, smh.shadowsEnd, smh.highlightsStart, smh.highLightsEnd));
        }

        void ConfigureFXAA()
        {
            if (fxaa.quality == CameraBufferSettings.FXAA.Quality.Low)
            {
                buffer.EnableShaderKeyword(fxaaQualityLowKeyword);
                buffer.DisableShaderKeyword(fxaaQualityMediumKeyword);
            }
            else if (fxaa.quality == CameraBufferSettings.FXAA.Quality.Medium)
            {
                buffer.EnableShaderKeyword(fxaaQualityMediumKeyword);
                buffer.DisableShaderKeyword(fxaaQualityLowKeyword);
            }
            else
            {
                buffer.DisableShaderKeyword(fxaaQualityLowKeyword);
                buffer.DisableShaderKeyword(fxaaQualityMediumKeyword);
            }
            buffer.SetGlobalVector(fxaaConfigId,
                new Vector4(fxaa.fixedThreshold, fxaa.relativeThreshold, fxaa.subpixelBlending));
        }

        #endregion
    }
}