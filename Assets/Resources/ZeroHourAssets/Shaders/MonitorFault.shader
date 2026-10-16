Shader "ZeroDuty/MonitorFault"
{
    Properties
    {
        _BaseMap ("Live Feed", 2D) = "black" {}
        _FaultTime ("Fault Time", Float) = 0
        _CueAmount ("Interference", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+30" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float _FaultTime;
                float _CueAmount;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
            half4 Frag(Varyings input) : SV_Target
            {
                float tick = floor(_FaultTime * 24);
                float line = floor(input.uv.y * 130);
                float band = step(.91, Hash(float2(floor(line / 6), tick)));
                float2 uv = input.uv;
                uv.x = saturate(uv.x + (Hash(float2(line, tick)) - .5) * .06 * band * _CueAmount);
                half3 feed = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                float grain = Hash(float2(floor(uv.x * 320) + tick, line));
                float stripe = step(.87, Hash(float2(line, tick)));
                // Gray-white mechanical interference, without a colored wash
                // or hiding the room the player is trying to inspect.
                feed *= 1 - .25 * _CueAmount;
                feed = lerp(feed, half3(.65, .65, .65) * grain, stripe * .48 * _CueAmount);
                return half4(feed, 1);
            }
            ENDHLSL
        }
    }
}
