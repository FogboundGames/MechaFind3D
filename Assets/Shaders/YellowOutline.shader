Shader "MechaFind3D/YellowOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1.0, 0.84, 0.0, 1.0) // Vibrant Gold Yellow
        _OutlineWidth ("Outline Width", Range(0.0005, 0.03)) = 0.005
        _GlowIntensity ("Glow Intensity", Range(1.0, 2.5)) = 1.25
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "Queue"="Transparent+100" 
            "RenderPipeline"="UniversalPipeline"
        }

        // Pass for Inverted Hull Outline
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="UniversalForward" }

            Cull Front
            ZWrite On
            ZTest LEqual
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
                
                // Extrude vertex position along normal vector
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
