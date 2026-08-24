Shader "MechaFind3D/MechaRevealOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.0, 1.0, 0.85, 1.0)
        _OutlineWidth ("Outline Width", Range(0.0005, 0.03)) = 0.008
        _GlowIntensity ("Glow Intensity", Range(1.0, 3.0)) = 1.5
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Overlay"
            "Queue"="Overlay"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "REVEAL_OUTLINE"
            Tags { "LightMode"="UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _GlowIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 extrudedPos = input.positionOS.xyz + normalize(input.normalOS) * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(extrudedPos);
                output.color = _OutlineColor * _GlowIntensity;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
