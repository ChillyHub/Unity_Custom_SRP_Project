using System;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace Scripts
{
    public class RandomMeshBalls : MonoBehaviour
    {
        private static int baseColorId = Shader.PropertyToID("_BaseColor");
        private static int metallicId = Shader.PropertyToID("_Metallic");
        private static int smoothnessId = Shader.PropertyToID("_Smoothness");
        private static int cutoffId = Shader.PropertyToID("_Cutoff");

        [SerializeField] private Mesh mesh = default;
        [SerializeField] private Material material = default;
        [SerializeField, Range(0, 1023)] private int meshCounts = 1023;
        [SerializeField, Range(0, 1)] private float cutoff = 0.5f;
        [SerializeField] private LightProbeProxyVolume lightProbeVolume = null;

        private Matrix4x4[] matrices;
        private Vector4[] baseColors;
        private float[] metallic;
        private float[] smoothness;

        private MaterialPropertyBlock block;

        private void Awake()
        {
            meshCounts = 1023;
            matrices = new Matrix4x4[meshCounts];
            baseColors = new Vector4[meshCounts];
            metallic = new float[meshCounts];
            smoothness = new float[meshCounts];
            
            for (int i = 0; i < matrices.Length; i++)
            {
                matrices[i] = Matrix4x4.TRS(
                    Random.insideUnitSphere * 10.0f,
                    Quaternion.Euler(Random.value * 360.0f, Random.value * 360.0f, Random.value * 360.0f),
                    Vector3.one * Random.Range(0.5f, 1.5f));
                baseColors[i] = new Vector4(
                    Random.value, Random.value, Random.value, Random.Range(0.5f, 1.0f));
                metallic[i] = Random.value < 0.25f ? 1.0f : 0.0f;
                smoothness[i] = Random.Range(0.05f, 0.95f);
            }
        }

        private void Update()
        {
            if (block == null)
            {
                block = new MaterialPropertyBlock();
                block.SetVectorArray(baseColorId, baseColors);
                block.SetFloatArray(metallicId, metallic);
                block.SetFloatArray(smoothnessId, smoothness);

                if (!lightProbeVolume)
                {
                    var positions = new Vector3[meshCounts];
                    for (int i = 0; i < matrices.Length; i++)
                    {
                        positions[i] = matrices[i].GetColumn(3);
                    }

                    var lightProbes = new SphericalHarmonicsL2[meshCounts];
                    var occlusionProbes = new Vector4[meshCounts];
                    LightProbes.CalculateInterpolatedLightAndOcclusionProbes(
                        positions, lightProbes, occlusionProbes);
                    block.CopySHCoefficientArraysFrom(lightProbes);
                    block.CopyProbeOcclusionArrayFrom(occlusionProbes);
                }
                block.SetFloat(cutoffId, cutoff);
            }
            
            Graphics.DrawMeshInstanced(mesh, 0, material, matrices, meshCounts, block, 
                ShadowCastingMode.On, true, 0, null, 
                lightProbeVolume ? LightProbeUsage.UseProxyVolume : LightProbeUsage.CustomProvided, 
                lightProbeVolume);
        }

        private void OnApplicationQuit()
        {
            meshCounts = 1023;
        }
    }
}