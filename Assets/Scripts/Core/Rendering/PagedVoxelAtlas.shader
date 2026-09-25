Shader "Custom/PagedVoxelAtlas"
{
    Properties
    {
        _MainTex("Texture Atlas", 2D) = "white" {}
        _AtlasTiles("Tiles Per Row", Float) = 16
        [Range(1.0, 3.0)] _LightCurve("Light Curve", Float) = 1.5
        [Range(0.0, 1.0)] _HemisphereStrength("Hemisphere Strength", Float) = 0.3
        [Range(0.0, 1.0)] _GroundAmbient("Downward Face Brightness", Float) = 0.65
        [Range(1.0, 1.25)] _TopAmbient("Upward Face Brightness", Float) = 1.08
        [Toggle(_DEBUG_NORMALS)] _DebugNormals("Debug World-Space Normals", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _DEBUG_NORMALS
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _AtlasTiles;
            float _SunLight;
            float _LightingDebugMode;
            float _NormalsDebugMode;
            float _LightCurve;
            float _HemisphereStrength;
            float _GroundAmbient;
            float _TopAmbient;

            struct PackedChunkVertex
            {
                uint positionXY;
                uint positionZW;
                uint normal;
                uint color;
                uint uv;
                uint atlasTile;
            };

            struct ChunkGpuData
            {
                float4 origin;
                float4 boundsCenter;
                float4 boundsExtents;
            };

            StructuredBuffer<PackedChunkVertex> _Vertices;
            StructuredBuffer<uint> _VertexInstance;
            StructuredBuffer<ChunkGpuData> _ChunkData;

            struct v2f
            {
                float4 posCS : SV_POSITION;
                float2 uvLocal : TEXCOORD0;
                float4 atlasMeta : TEXCOORD1;
                half2 light : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                PackedChunkVertex vertex = _Vertices[vertexID];
                uint chunkSlot = _VertexInstance[vertexID];
                ChunkGpuData chunk = _ChunkData[chunkSlot];

                uint positionX = vertex.positionXY & 0xffffu;
                uint positionY = vertex.positionXY >> 16;
                uint positionZ = vertex.positionZW & 0xffffu;
                float3 positionWS = float3(positionX, positionY, positionZ) / 100.0 + chunk.origin.xyz;

                v2f output;
                output.posCS = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
                output.positionWS = positionWS;

                uint packedU = vertex.uv & 0xffffu;
                uint packedV = vertex.uv >> 16;
                output.uvLocal = float2(packedU, packedV) / 100.0;

                float tileSize = 1.0 / _AtlasTiles;
                float tileIndex = (float)(vertex.atlasTile & 0xffffu);
                float tileColumn = fmod(tileIndex, _AtlasTiles);
                float tileRow = floor(tileIndex / _AtlasTiles);
                float uMin = tileColumn * tileSize;
                float vMax = 1.0 - tileRow * tileSize;
                output.atlasMeta = float4(uMin, vMax - tileSize, tileSize, tileSize);

                output.light = half2(
                    (half)(vertex.color & 0xffu),
                    (half)((vertex.color >> 8) & 0xffu)) / 255.0h;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                #if defined(_DEBUG_NORMALS)
                    const bool debugNormals = true;
                #else
                    bool debugNormals = _NormalsDebugMode > 0.5;
                #endif

                float3 normalWS = cross(ddy(input.positionWS), ddx(input.positionWS));
                float normalLengthSq = dot(normalWS, normalWS);

                if (debugNormals)
                {
                    if (normalLengthSq < 1e-12)
                        return fixed4(1.0, 0.25, 0.0, 1.0);

                    normalWS *= rsqrt(normalLengthSq);
                    float3 axis = abs(normalWS);
                    if (axis.x >= axis.y && axis.x >= axis.z)
                        return normalWS.x >= 0.0
                            ? fixed4(1.0, 0.0, 0.0, 1.0)
                            : fixed4(0.0, 1.0, 1.0, 1.0);
                    if (axis.y >= axis.z)
                        return normalWS.y >= 0.0
                            ? fixed4(0.0, 1.0, 0.0, 1.0)
                            : fixed4(1.0, 0.0, 1.0, 1.0);
                    return normalWS.z >= 0.0
                        ? fixed4(0.0, 0.0, 1.0, 1.0)
                        : fixed4(1.0, 1.0, 0.0, 1.0);
                }

                normalWS *= rsqrt(max(normalLengthSq, 1e-12));
                float2 tileLocal = frac(input.uvLocal);
                float2 baseUV = input.atlasMeta.xy;
                float2 tileSize = input.atlasMeta.zw;

                if (_LightingDebugMode > 0.5)
                {
                    float lightLevel = round(saturate(max(input.light.r, input.light.g)) * 15.0);
                    float debugTileIndex = 26.0 + lightLevel;
                    float debugTileColumn = fmod(debugTileIndex, _AtlasTiles);
                    float debugTileRow = floor(debugTileIndex / _AtlasTiles);
                    baseUV = float2(debugTileColumn * tileSize.x,
                        1.0 - (debugTileRow + 1.0) * tileSize.y);
                    float2 debugUV = baseUV + float2(1.0 - tileLocal.x, tileLocal.y) * tileSize;
                    return tex2D(_MainTex, debugUV);
                }

                fixed4 color = tex2D(_MainTex, baseUV + tileLocal * tileSize);
                half hemisphere = (half)normalWS.y * 0.5h + 0.5h;
                half hemisphereLight = lerp((half)_GroundAmbient, (half)_TopAmbient, hemisphere);
                half faceBrightness = lerp(1.0h, hemisphereLight, (half)_HemisphereStrength);
                half skyLight = input.light.r * saturate(_SunLight) * faceBrightness;
                half blockLight = input.light.g;
                color.rgb *= pow(max(skyLight, blockLight), _LightCurve);
                return color;
            }
            ENDHLSL
        }
    }
}
