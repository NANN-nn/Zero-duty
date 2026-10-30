Shader "EndOfShift/Native Shadow"
{
    Properties { _FilmTime("Film Time", Float) = 0 _FloorHeight("Floor Height", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            float _FilmTime;
            float _FloorHeight;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.world=TransformObjectToWorld(input.positionOS.xyz);
                float ripple=sin(o.world.y*17+_FilmTime*2)*sin(o.world.z*23+o.world.x*31)*.006;
                o.world+=TransformObjectToWorldNormal(input.normalOS)*ripple;
                o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float rim=pow(1-saturate(abs(dot(normalize(i.normal),normalize(GetCameraPositionWS()-i.world)))),3);
                float fold=.5+.5*sin(i.world.y*6+i.world.x*14+sin(i.world.z*8));
                float fringe=sin(i.world.x*57+sin(i.world.z*31)+_FilmTime*.8)*.06;
                float alpha=smoothstep(.04,.58,i.world.y-_FloorHeight+fringe);
                return half4(half3(.002,.003,.004)+rim*half3(.022,.031,.036)+fold*.0015,alpha);
            }
            ENDHLSL
        }
    }
}
