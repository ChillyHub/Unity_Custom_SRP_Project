using CustomRP.Runtime;
using CustomRP.Settings;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace CustomRP.Editor
{
    [CanEditMultipleObjects]
    [CustomEditorForRenderPipeline(typeof(Light), typeof(CustomRenderPipelineAsset))]
    public class CustomLightEditor : LightEditor
    {
        private static GUIContent renderingLayerMaskLabel =
            new GUIContent("Rendering Layer Mask", "Functional version of above property");
        
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            RenderingLayerMaskDrawer.Draw(settings.renderingLayerMask, renderingLayerMaskLabel);
            if (!settings.lightType.hasMultipleDifferentValues &&
                (LightType)settings.lightType.enumValueIndex == LightType.Spot)
            {
                settings.DrawInnerAndOuterSpotAngle();
            }
            settings.ApplyModifiedProperties();
            // If light cullingMask is not Everything, show waring!
            // If light if non-directional, only affect shadow unless ...
            var light = target as Light;
            if (light.cullingMask != -1)
            {
                EditorGUILayout.HelpBox(light.type == LightType.Directional ?
                    "Culling Mask only affects shadows." :
                    "Culling Mask only affects shadow unless Use Lights Per Objects is on.",
                    MessageType.Warning);
            }
        }

        void DrawRenderingLayerMask()
        {
            SerializedProperty property = settings.renderingLayerMask;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int mask = property.intValue;
            if (mask == int.MaxValue)
            {
                mask = -1;
            }
            mask = EditorGUILayout.MaskField(renderingLayerMaskLabel, mask,
                GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames);
            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = mask == -1 ? int.MaxValue : mask;
            }

            EditorGUI.showMixedValue = false;
        }
    }
}