Shader "Custom/VoxelAtlas"
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
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma  target 4.5
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
                half2 light : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                
                // Packed chunk positions remain compatible with every LOD scale.
                float3 positionOS = (float3)v.vertex.xyz / VERTEX_POS_SCALE;
                o.posCS = UnityObjectToClipPos(float4(positionOS, 1.0));
                o.positionWS = mul(unity_ObjectToWorld, float4(positionOS, 1.0)).xyz;

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
                o.light = v.vertexColor.rg;
                return o;

            }

            fixed4 frag(v2f i) : SV_Target
            {
                #if defined(_DEBUG_NORMALS)
                    const bool debugNormals = true;
                #else
                    bool debugNormals = _NormalsDebugMode > 0.5;
                #endif
                
                // Voxel meshes do not carry normals, so derive one flat normal per
                // rendered triangle. This is shared by normal debug and lighting.
                float3 normalWS = cross(ddy(i.positionWS), ddx(i.positionWS));
                float normalLengthSq = dot(normalWS, normalWS);

                if (debugNormals)
                {
                    if (normalLengthSq < 1e-12)
                    {
                        // Orange is reserved for missing or corrupt normals.
                        return fixed4(1.0, 0.25, 0.0, 1.0);
                    }

                    normalWS *= rsqrt(normalLengthSq);
                    float3 axis = abs(normalWS);

                    // Voxel faces use cardinal normals. Give every signed axis a
                    // distinct solid color instead of the hard-to-read gray tints
                    // produced by the usual normal * 0.5 + 0.5 visualization.
                    if (axis.x >= axis.y && axis.x >= axis.z)
                        return normalWS.x >= 0.0
                            ? fixed4(1.0, 0.0, 0.0, 1.0)   // +X: red
                            : fixed4(0.0, 1.0, 1.0, 1.0);  // -X: cyan

                    if (axis.y >= axis.z)
                        return normalWS.y >= 0.0
                            ? fixed4(0.0, 1.0, 0.0, 1.0)   // +Y: green
                            : fixed4(1.0, 0.0, 1.0, 1.0);  // -Y: magenta

                    return normalWS.z >= 0.0
                        ? fixed4(0.0, 0.0, 1.0, 1.0)       // +Z: blue
                        : fixed4(1.0, 1.0, 0.0, 1.0);      // -Z: yellow
                }
                
                normalWS *= rsqrt(max(normalLengthSq, 1e-12));
                
                float2 tileLocal = frac(i.uvLocal);
                float2 baseUV = i.atlasMeta.xy;
                float2 tileSize = i.atlasMeta.zw;
                
                if (_LightingDebugMode > 0.5)
                {
                    float lightLevel = round(saturate(max(i.light.r, i.light.g)) * 15.0);
                    float debugTileIndex = 26.0 + lightLevel;
                    float debugTileColumn = fmod(debugTileIndex, _AtlasTiles);
                    float debugTileRow = floor(debugTileIndex / _AtlasTiles);
                    baseUV = float2(
                        debugTileColumn * tileSize.x,
                        1.0 - (debugTileRow + 1.0) * tileSize.y
                    );

                    float2 debugTileLocal = float2(1.0 - tileLocal.x, tileLocal.y);
                    float2 debugUV = baseUV + debugTileLocal * tileSize;
                    return tex2D(_MainTex, debugUV);
                }
                
                float2 sampleUV = baseUV + tileLocal * tileSize;

                fixed4 col = tex2D(_MainTex, sampleUV);
                // Bias propagated skylight by face direction. Upward faces can be
                // slightly brighter than the old 100% value, sides stay close to
                // neutral, and downward faces receive less sky illumination.
                half hemisphere = (half)normalWS.y * 0.5h + 0.5h;
                half hemisphereLight = lerp((half)_GroundAmbient, (half)_TopAmbient, hemisphere);
                half faceBrightness = lerp(1.0h, hemisphereLight, (half)_HemisphereStrength);
                half skyLight = i.light.r * saturate(_SunLight) * faceBrightness;
                half blockLight = i.light.g;
                // Do not clamp the combined value: the small >1 upward-face value
                // is intentional. Block light itself remains direction-independent.
                half light = max(skyLight, blockLight);
                half curvedLight = pow(light, _LightCurve);
                col.rgb *= curvedLight;
                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
