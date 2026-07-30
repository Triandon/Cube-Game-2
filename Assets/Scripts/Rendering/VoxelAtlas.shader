Shader "Custom/VoxelAtlas"
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
            float _SunLight;
            
            static const float VERTEX_POS_SCALE = 100.0;

            struct appdata
            {
                uint4 vertex : POSITION;
                uint2 uv     : TEXCOORD0; // fixed-point block-space UV, scale 100
                uint2 uv1    : TEXCOORD1; // x = atlas tile index
                half4 vertexColor : COLOR0;
            };

            struct v2f
            {
                float4 posCS : SV_POSITION;
                float2 uvLocal : TEXCOORD0;
                float4 atlasMeta : TEXCOORD1;
                half light : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                
                // Packed chunk positions remain compatible with every LOD scale.
                float3 positionOS = (float3)v.vertex.xyz / VERTEX_POS_SCALE;
                o.posCS = UnityObjectToClipPos(float4(positionOS, 1.0));

                // Greedy quads store UVs in block units, so frac() in frag repeats
                // the selected texture once per block regardless of merged size.
                o.uvLocal = (float2)v.uv / VERTEX_POS_SCALE;
                float tileSize = 1.0 / _AtlasTiles;
                float tileIndex = (float)v.uv1.x;
                float tileCol = fmod(tileIndex, _AtlasTiles);
                float tileRow = floor(tileIndex / _AtlasTiles);
                float uMin = tileCol * tileSize;
                float vMax = 1.0 - tileRow * tileSize;
                float vMin = vMax - tileSize;
                o.atlasMeta = float4(uMin, vMin, tileSize, tileSize);

                // MeshData stores the custom voxel light in COLOR0.r.
                o.light = v.vertexColor.r;
                return o;

            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 tileLocal = frac(i.uvLocal);
                float2 baseUV = i.atlasMeta.xy;
                float2 tileSize = i.atlasMeta.zw;
                float2 sampleUV = baseUV + tileLocal * tileSize;

                fixed4 col = tex2D(_MainTex, sampleUV);
                col.rgb *= saturate(i.light) * saturate(_SunLight);
                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
