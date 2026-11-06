Shader "Custom/PagedVoxelAtlas"
{
    Properties
    {
        _MainTex("Texture Atlas", 2D) = "white" {}
        _AtlasTiles("Tiles Per Row", Float) = 16
        _EffectsAtlas("EffectsAtlas (4 x 4, RGBA)", 2D) = "" {}
        [HideInInspector] _EffectsEnabled("Effects Enabled", Float) = 0
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
            sampler2D _EffectsAtlas;
            float4 _EffectsAtlas_TexelSize;
            float _EffectsEnabled;
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
                uint positionZLightTile;
                uint uv;
                uint normal;
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
                nointerpolation float3 normalWS : TEXCOORD3;
                nointerpolation uint crackStage : TEXCOORD4;
            };
            
            float3 DecodePackedNormal(uint packedNormal)
            {
                // Three signed normal bytes; the high byte is the CPU-calculated crack stage.
                int3 components = int3(
                    (int)(packedNormal << 24) >> 24,
                    (int)(packedNormal << 16) >> 24,
                    (int)(packedNormal << 8) >> 24);
                float3 normal = (float3)components;
                // Normalize once per vertex to correct SNORM8 rounding on slopes.
                // Keep zero normals zero so the debug view can flag invalid data.
                return normal * rsqrt(max(dot(normal, normal), 1.0));
            }

            v2f vert(uint vertexID : SV_VertexID)
            {
                PackedChunkVertex vertex = _Vertices[vertexID];
                uint chunkSlot = _VertexInstance[vertexID];
                ChunkGpuData chunk = _ChunkData[chunkSlot];

                uint positionX = vertex.positionXY & 0xffffu;
                uint positionY = vertex.positionXY >> 16;
                uint positionZ = vertex.positionZLightTile & 0xffffu;
                float3 positionWS = float3(positionX, positionY, positionZ) / 100.0 + chunk.origin.xyz;

                v2f output;
                output.posCS = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
                output.normalWS = DecodePackedNormal(vertex.normal);
                output.crackStage = vertex.normal >> 24;

                uint packedU = vertex.uv & 0xffffu;
                uint packedV = vertex.uv >> 16;
                output.uvLocal = float2(packedU, packedV) / 100.0;

                float tileSize = 1.0 / _AtlasTiles;
                float tileIndex = (float)(vertex.positionZLightTile >> 24);
                float tileColumn = fmod(tileIndex, _AtlasTiles);
                float tileRow = floor(tileIndex / _AtlasTiles);
                float uMin = tileColumn * tileSize;
                float vMax = 1.0 - tileRow * tileSize;
                output.atlasMeta = float4(uMin, vMax - tileSize, tileSize, tileSize);

                output.light = half2(
                    (half)((vertex.positionZLightTile >> 16) & 0xfu),
                    (half)((vertex.positionZLightTile >> 20) & 0xfu)) / 15.0h;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                #if defined(_DEBUG_NORMALS)
                    const bool debugNormals = true;
                #else
                    bool debugNormals = _NormalsDebugMode > 0.5;
                #endif

                float3 normalWS = input.normalWS;

                if (debugNormals)
                {
                    if (dot(normalWS, normalWS) < 1e-12)
                        return fixed4(1.0, 0.25, 0.0, 1.0);
                    
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
                                [branch]
                if (input.crackStage > 0u && _EffectsEnabled > 0.5)
                {
                    // Stages 1-3 occupy the first three tiles, left to right in the top row.
                    // Reuse each face's existing UVs, including rotated/sloped/custom geometry.
                    float effectTile = (float)(min(input.crackStage, 3u) - 1u);
                    float2 inset = 0.5 * _EffectsAtlas_TexelSize.xy * 4.0;
                    float2 effectLocal = clamp(tileLocal, inset, 1.0 - inset);
                    float2 effectUV = (float2(effectTile, 3.0) + effectLocal) * 0.25;
                    // Explicit LOD avoids derivatives across stage branches and tile mip bleeding.
                    fixed4 cracks = tex2Dlod(_EffectsAtlas, float4(effectUV, 0, 0));
                    color.rgb = lerp(color.rgb, cracks.rgb, cracks.a);
                }
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
