Shader "EndOfShift/Native Film Grade"
{
    Properties { _MainTex("Source",2D)="black"{} _Grab("Grasp",Float)=0 _FilmTime("Time",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Grab, _FilmTime;
            half4 Frag(v2f_img i):SV_Target
            {
                float2 p=i.uv-.5;
                half3 col=tex2D(_MainTex,i.uv).rgb;
                float vignette=1-smoothstep(.17,.68,length(p*float2(1,.8)))*.76;
                float noise=frac(sin(dot(i.uv*float2(1344,756)+floor(_FilmTime*18),float2(12.9898,78.233)))*43758.5453);
                col=lerp(dot(col,half3(.2126,.7152,.0722)).xxx,col,.8)*half3(.69,.74,.79)*vignette;
                col+= (noise-.5)*.005;
                float edge=abs(p.y+.015)+sin(p.x*27+sin(p.x*62))*.009*_Grab;
                float cover=smoothstep(lerp(.62,.115,_Grab),lerp(.72,.21,_Grab),edge);
                return half4(max(0,col)*(1-cover),1);
            }
            ENDHLSL
        }
    }
}
