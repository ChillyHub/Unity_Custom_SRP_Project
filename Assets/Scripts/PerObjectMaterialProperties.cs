using System;
using UnityEngine;

namespace Scripts
{
    [DisallowMultipleComponent]
    public class PerObjectMaterialProperties : MonoBehaviour
    {
        private static int baseColorId = Shader.PropertyToID("_BaseColor");
        private static int metallicId = Shader.PropertyToID("_Metallic");
        private static int smoothnessId = Shader.PropertyToID("_Smoothness");
        private static int cutoffId = Shader.PropertyToID("_Cutoff");
        private static int emissionColorId = Shader.PropertyToID("_EmissionColor");
        
        private static MaterialPropertyBlock block;

        [SerializeField] private Color baseColor = Color.white;
        [SerializeField, Range(0.0f, 1.0f)] private float metallic = 0.0f;
        [SerializeField, Range(0.0f, 1.0f)] private float smoothness = 1.0f;
        [SerializeField, Range(0.0f, 1.0f)] private float cutoff = 0.5f;
        [SerializeField, ColorUsage(false, true)]
        Color emissionColor = Color.black;

        private void OnValidate()
        {
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            
            // Set Material property
            block.SetColor(baseColorId, baseColor);
            block.SetFloat(metallicId, metallic);
            block.SetFloat(smoothnessId, smoothness);
            block.SetFloat(cutoffId, cutoff);
            block.SetColor(emissionColorId, emissionColor);
            
            GetComponent<Renderer>().SetPropertyBlock(block);
        }

        private void Awake()
        {
            OnValidate();
        }
    }
}