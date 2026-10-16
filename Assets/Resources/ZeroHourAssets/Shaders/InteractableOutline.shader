Shader "ZeroDuty/InteractableOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.96, 0.97, 1, 0.86)
        _OutlineWidth ("Outline Width", Float) = 0.009
        _OutlinePixelOffset ("Screen Pixel Offset", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20" }
        Pass
        {
            Name "Visible Edge"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil { Ref 1 Comp NotEqual Pass Keep }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float4 _OutlinePixelOffset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                if (_OutlineWidth > 0.0)
                    positionWS += normalize(normalWS) * _OutlineWidth;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionCS.xy += _OutlinePixelOffset.xy * 2.0
                    / _ScreenParams.xy * output.positionCS.w;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
