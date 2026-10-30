Shader "ZeroDuty/NativeMenuScreen"
{
    Properties { _BaseMap("Live Feed",2D)="black"{} _FaultTime("Clock",Float)=0 _CueAmount("Interference",Float)=0 _Brightness("Brightness",Float)=1 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+40"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float _FaultTime,_CueAmount,_Brightness;
            CBUFFER_END
            struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            V Vert(A v){V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
            float Hash(float2 p){return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453);}
            half4 Frag(V v):SV_Target
            {
                float tick=floor(_FaultTime*24),scanRow=floor(v.uv.y*140);
                float2 uv=v.uv;uv.x+= (Hash(float2(scanRow,tick))-.5)*.09*_CueAmount*step(.65,Hash(float2(floor(scanRow/5),tick)));
                half3 feed=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,saturate(uv)).rgb;
                float noise=Hash(float2(floor(uv.x*400)+tick,scanRow));
                feed=lerp(feed,noise.xxx*.72,step(.78,Hash(float2(scanRow,tick)))*_CueAmount*.75);
                feed*=lerp(.94,1,step(.4,frac(uv.y*300)));
                return half4(feed*_Brightness,1);
            }
            ENDHLSL
        }
    }
}
