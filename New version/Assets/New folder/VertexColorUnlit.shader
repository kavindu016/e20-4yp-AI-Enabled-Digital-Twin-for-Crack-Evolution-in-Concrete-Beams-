Shader "Custom/VertexColorUnlit"
{
    Properties
    {
        _CrackTex ("Crack Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _CrackTex;

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

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.col = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 crack = tex2D(_CrackTex, i.uv);

                // If crack pixel is black → draw crack
                if (crack.r < 0.5)
                    return fixed4(0, 0, 0, 1);

                // Otherwise show heatmap
                return i.col;
            }
            ENDCG
        }
    }
}