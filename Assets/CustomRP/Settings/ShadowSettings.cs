using UnityEngine;

namespace CustomRP.Settings
{
    // Shadow properties settings
    [System.Serializable]
    public class ShadowSettings
    {
        // Max distance of shadow
        [Min(0.0f)] public float maxDistance = 100.0f;
        // Fade distance of Shadow
        [Range(0.001f, 1.0f)] public float distanceFade = 0.1f;
        // Shadow texture size
        public enum TextureSize
        {
            _256 = 256,
            _512 = 512,
            _1024 = 1024,
            _2048 = 2048,
            _4096 = 4096,
            _8192 = 8192
        }
        // Percentage-Close Filtering (PCF) Mode
        public enum FilterMode
        {
            PCF2x2, PCF3x3, PCF5x5, PCF7x7
        }

        // Directional light shadow atlas setting
        [System.Serializable]
        public struct Directional
        {
            public enum CascadeBlendMode
            {
                Hard, Soft, Dither
            }
            
            public TextureSize atlasSize;
            public FilterMode filter;
            public CascadeBlendMode cascadeBlend;
            // Cascaded count
            [Range(1, 4)] public int cascadeCount;
            // Fade of cascade
            [Range(0.001f, 1.0f)] public float cascadeFade;
            // Cascaded ratio
            [Range(0.0f, 1.0f)] public float cascadeRatio1, cascadeRatio2, cascadeRatio3;

            public Vector3 CascadeRatios => new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);
        }

        public Directional directional = new Directional
        {
            atlasSize = TextureSize._1024,
            filter = FilterMode.PCF2x2,
            cascadeBlend = Directional.CascadeBlendMode.Hard,
            cascadeCount = 4,
            cascadeFade = 0.1f,
            cascadeRatio1 = 0.1f,
            cascadeRatio2 = 0.25f,
            cascadeRatio3 = 0.5f
        };
        
        // Non-directional light shadow atlas setting
        [System.Serializable]
        public struct Other
        {
            public TextureSize atlasSize;
            public FilterMode filter;
        }

        public Other other = new Other
        {
            atlasSize = TextureSize._1024,
            filter = FilterMode.PCF2x2
        };
    }
}