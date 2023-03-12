using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace CustomRP.Runtime
{
    public partial class CameraRenderer
    {
        private static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
        private static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

        private partial void DrawUnsupportedShaders();
        private partial void DrawGizmosBeforeFX();
        private partial void DrawGizmosAfterFX();
        private partial void PrepareForSceneWindow();
        private partial void PrepareBuffer();
        
#if UNITY_EDITOR
        private static ShaderTagId[] legacyShaderTagIds =
        {
            new ShaderTagId("Always"),
            new ShaderTagId("ForwardBase"),
            new ShaderTagId("PrepassBase"),
            new ShaderTagId("Vertex"),
            new ShaderTagId("VertexLMRGBM"),
            new ShaderTagId("VertexLM")
        };

        private static Material errorMaterial;
        
        private string SampleName { get; set; }
        
        private partial void DrawUnsupportedShaders()
        {
            if (errorMaterial == null)
            {
                errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
            }
            
            // Use the first element to construct DrawingSettings
            var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], new SortingSettings(camera))
            {
                overrideMaterial = errorMaterial
            };
            
            for (int i = 1; i < legacyShaderTagIds.Length; i++)
            {
                drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
            }

            var filteringSettings = FilteringSettings.defaultValue;
            
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        }

        private partial void DrawGizmosBeforeFX()
        {
            if (Handles.ShouldRenderGizmos())
            {
                if (useIntermediateBuffer)
                {
                    Draw(depthAttachmentId, BuiltinRenderTextureType.CameraTarget, true);
                    ExecuteBuffer();
                }
                context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            }
        }

        private partial void DrawGizmosAfterFX()
        {
            if (Handles.ShouldRenderGizmos())
            {
                context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
            }
        }
        
        // Draw mesh in game view to scene view
        private partial void PrepareForSceneWindow()
        {
            if (camera.cameraType == CameraType.SceneView)
            {
                ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
                // Disable scaled rendering
                useScaledRendering = false;
            }
        }

        private partial void PrepareBuffer()
        {
            // Allocate memory only in editor
            Profiler.BeginSample("Editor Only");
            buffer.name = SampleName = camera.name;
            Profiler.EndSample();
        }
#else
        private const string SampleName = bufferName;
#endif
    }
}