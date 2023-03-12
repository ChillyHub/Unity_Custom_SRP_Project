using UnityEngine;

namespace CustomRP.Settings
{
    [CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
    public class PostFXSettings : ScriptableObject
    {
        [SerializeField] private Shader shader = default;

        [System.NonSerialized] private Material material;
        
        public Material Material
        {
            get
            {
                if (material == null && shader != null)
                {
                    material = new Material(shader);
                    material.hideFlags = HideFlags.HideAndDontSave;
                }

                return material;
            }
        }

        [SerializeField] public bool isActive = true;

        #region Bloom

        [System.Serializable]
        public struct BloomSettings
        {
            [Range(0.0f, 16.0f)] public int maxIterations;
            [Min(1)] public int downscaleLimit;
            public bool bicubicUpSampling;
            [Min(0.0f)] public float threshold;
            [Range(0.0f, 1.0f)] public float thresholdKnee;
            [Range(0.0f, 1.0f)] public float intensity;
            // Fade flicker
            public bool fadeFireflies;
            
            public enum Mode
            {
                Additive, Scattering
            }

            public Mode mode;
            [Range(0.05f, 0.95f)] public float scatter;
            
            // Whether use render scale
            public bool ignoreRenderScale;
        }

        [SerializeField] private BloomSettings bloom = new BloomSettings
        {
            scatter = 0.7f
        };

        public BloomSettings Bloom => bloom;

        #endregion

        #region Tone Mapping

        [System.Serializable]
        public struct ToneMappingSettings
        {
            public enum Mode
            {
                None,
                ACES,
                Neutral,
                Reinhard
            }

            public Mode mode;
        }

        [SerializeField] private ToneMappingSettings toneMapping = default;

        public ToneMappingSettings ToneMapping => toneMapping;

        #endregion

        #region Color Grading

        [System.Serializable]
        public struct ColorAdjustmentsSettings
        {
            // Post exposure, adjust scene exposure
            public float postExposure;
            // Contrast, adjust color grading range
            [Range(-100.0f, 100.0f)] public float contrast;
            // Color filter, multiply source color and filter color
            [ColorUsage(false, true)] public Color colorFilter;
            // Hue shift, change all color tones
            [Range(-180.0f, 180.0f)] public float hueShift;
            // Saturation, change color intensity
            [Range(-100.0f, 100.0f)] public float saturation;
        }

        [SerializeField] private ColorAdjustmentsSettings colorAdjustments = 
            new ColorAdjustmentsSettings
        {
            colorFilter = Color.white
        };

        public ColorAdjustmentsSettings ColorAdjustments => colorAdjustments;
        
        
        [System.Serializable]
        public struct WhiteBalanceSettings
        {
            // Color temperature, adjust white balance warn or cold
            [Range(-100.0f, 100.0f)] public float temperature;
            // Color hue, adjust color after temperature change
            [Range(-100.0f, 100.0f)] public float tint;
        }

        [SerializeField] private WhiteBalanceSettings whiteBalance = default;

        public WhiteBalanceSettings WhiteBalance => whiteBalance;
        
        
        [System.Serializable]
        public struct SplitToningSettings
        {
            // Shade to shadow and highlight
            [ColorUsage(false)] public Color shadows, highlights;
            // Set balance between shadow and highlight
            [Range(-100.0f, 100.0f)] public float balance;
        }

        [SerializeField] private SplitToningSettings splitToning = 
            new SplitToningSettings
        {
            shadows = Color.gray,
            highlights = Color.gray
        };

        public SplitToningSettings SplitToning => splitToning;
        
        
        [System.Serializable]
        public struct ChannelMixerSettings
        {
            public Vector3 red, green, blue;
        }

        [SerializeField] private ChannelMixerSettings channelMixer = 
            new ChannelMixerSettings
        {
            red = Vector3.right,
            green = Vector3.up,
            blue = Vector3.forward
        };

        public ChannelMixerSettings ChannelMixer => channelMixer;
        
        
        [System.Serializable]
        public struct ShadowsMidTonesHighlightsSettings
        {
            [ColorUsage(false, true)] public Color shadows, midTones, highlights;
            [Range(0.0f, 2.0f)] 
            public float shadowsStart, shadowsEnd, highlightsStart, highLightsEnd;
        }
        
        [SerializeField]
        ShadowsMidTonesHighlightsSettings shadowsMidTonesHighlights = 
            new ShadowsMidTonesHighlightsSettings
        {
            shadows = Color.white,
            midTones = Color.white,
            highlights = Color.white,
            shadowsEnd = 0.3f,
            highlightsStart = 0.55f,
            highLightsEnd = 1.0f
        };

        public ShadowsMidTonesHighlightsSettings ShadowsMidTonesHighlights => shadowsMidTonesHighlights;

        #endregion
    }
}