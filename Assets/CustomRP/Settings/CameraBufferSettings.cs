﻿using System;
using UnityEngine;

namespace CustomRP.Settings
{
    [Serializable]
    public class CameraBufferSettings
    {
        public bool allowHDR;
        public bool copyDepth;
        public bool copyDepthReflection;
        public bool copyColor;
        public bool copyColorReflection;

        [Range(0.1f, 2.0f)] public float renderScale;

        public enum BicubicRescalingMode
        {
            Off,
            UpOnly,
            UpAndDown
        }

        public BicubicRescalingMode bicubicRescaling;
        
        [Serializable]
        public struct FXAA
        {
            public bool enabled;

            [Range(0.0312f, 0.0833f)] public float fixedThreshold;
            [Range(0.063f, 0.333f)] public float relativeThreshold;
            [Range(0.0f, 1.0f)] public float subpixelBlending;
            
            public enum Quality
            {
                Low,
                Medium,
                High
            }

            public Quality quality;
        }

        public FXAA fxaa;
    }
}