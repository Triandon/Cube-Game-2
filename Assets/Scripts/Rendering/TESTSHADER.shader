Shader "Custom/VoxelAtlas_Repeating_WithLODFix"
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

            // ===== LOD crack-fix params =====
            float _lodScale;
            float _neighborLodPosX;
            float3 _chunkWorldPos;
            float _chunkSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 uv1    : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float4 meta : TEXCOORD1;
            };

            float Snap(float v, float scale)
            {
                return round(v / scale) * scale;
            }

            v2f vert(appdata v)
            {
                v2f o;

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                // ---- +X LOD crack fix ----
                float localX = worldPos.x - _chunkWorldPos.x;

                bool onPosXBorder =
                    abs(localX - _chunkSize) < 0.001 &&
                    _neighborLodPosX > _lodScale;

                if (onPosXBorder)
                {
                    float snapScale = _neighborLodPosX;
                    worldPos.y = Snap(worldPos.y, snapScale);
                    worldPos.z = Snap(worldPos.z, snapScale);
                }

                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1));
                o.uv0 = v.uv;
                o.meta = v.uv1;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 tileLocal = frac(i.uv0);

                float2 baseUV = i.meta.xy;
                float2 tileSize = i.meta.zw;

                float2 sampleUV = baseUV + tileLocal * tileSize;
                return tex2D(_MainTex, sampleUV);
            }
            ENDCG
        }
    }
}
