Shader "Hidden/CustomRP/Camera Renderer"
{
	Properties
	{
		// _MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Cull Off
		ZWrite Off 
		ZTest Always
		
		HLSLINCLUDE
		#include "./ShaderLibrary/Common.hlsl"
		#include "CameraRendererPasses.hlsl"
		ENDHLSL

		Pass
		{
			Name "Copy"
			
			Blend [_CameraSrcBlend] [_CameraDstBlend]
			
			HLSLPROGRAM

			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment CopyPassFragment
			
			ENDHLSL
		}
		Pass
		{
			Name "Copy Depth"
			
			ColorMask 0
			ZWrite On
			
			HLSLPROGRAM

			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment CopyDepthPassFragment
			
			ENDHLSL
		}
	}
}