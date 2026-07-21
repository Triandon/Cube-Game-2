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
            #pragma  target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _AtlasTiles;

            struct appdata
            {
                uint4 vertex : POSITION;
                uint2 uv     : TEXCOORD0; // fixed-point block-space UV, scale 100
                uint2 uv1    : TEXCOORD1; // x = atlas tile index
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
                o.pos = UnityObjectToClipPos(float4((float3)v.vertex.xyz / 100.0, 1.0));
                o.uv0 = (float2)v.uv / 100.0;
                float tileSize = 1.0 / _AtlasTiles;
                float tileIndex = (float)v.uv1.x;
                float tileCol = fmod(tileIndex, _AtlasTiles);
                float tileRow = floor(tileIndex / _AtlasTiles);
                float uMin = tileCol * tileSize;
                float vMax = 1.0 - tileRow * tileSize;
                float vMin = vMax - tileSize;
                o.meta = float4(uMin, vMin, tileSize, tileSize);
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
