Shader "Custom/BeamFullDT"
{
    Properties
    {
        _CurrentCrackTex ("Current Crack", 2D) = "white" {}
        _FutureCrackTex  ("Future Crack", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _CurrentCrackTex;
            sampler2D _FutureCrackTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 col : COLOR;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.col = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 current = tex2D(_CurrentCrackTex, i.uv);
                fixed4 future  = tex2D(_FutureCrackTex, i.uv);

                if (current.r < 0.5)
                    return fixed4(0,0,0,1);

                if (future.r < 0.5)
                    return fixed4(0.7,0,1,1);

                return i.col;
            }
            ENDCG
        }
    }
}