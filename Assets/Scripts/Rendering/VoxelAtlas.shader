Shader "Custom/VoxelAtlas_Repeating"
{
    Properties
    {
        _MainTex("Texture Atlas", 2D) = "white" {}
        _AtlasTiles("Tiles Per Row", Float) = 16
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _AtlasTiles;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0; // UV0: block-space coords (0..width,0..height)
                float4 uv1    : TEXCOORD1; // UV1: (uMin, vMin, tileSizeX, tileSizeY)
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0; // block-space coords (interpolated)
                float4 meta : TEXCOORD1; // atlas meta
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv0 = v.uv;
                o.meta = v.uv1;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // i.uv0 contains values in block units across the face.
                // frac(i.uv0) repeats every 1.0 -> per-block repetition
                float2 tileLocal = frac(i.uv0);

                // atlas meta:
                // meta.xy = base (uMin, vMin)
                // meta.zw = tileSize (width, height)
                float2 baseUV = i.meta.xy;
                float2 tileSize = i.meta.zw;

                float2 sampleUV = baseUV + tileLocal * tileSize;

                fixed4 col = tex2D(_MainTex, sampleUV);
                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
