using CustomRP.Settings;
using UnityEditor;
using UnityEngine;

namespace CustomRP.Runtime
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public class CustomRenderPipelineCamera : MonoBehaviour
    {
        [SerializeField] private CameraSettings settings = default;

        public CameraSettings Settings
        {
            get
            {
                if (settings == null)
                {
                    settings = new CameraSettings();
                }

                return settings;
            }
        }
    }
}