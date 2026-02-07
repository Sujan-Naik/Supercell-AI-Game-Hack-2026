Shader "Hidden/URP/ScreenSpaceOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _Thickness ("Thickness (px)", Float) = 1.5

        _DepthThreshold ("Depth Threshold", Float) = 0.001
        _NormalThreshold ("Normal Threshold", Float) = 0.20

        // 软边（越大线越柔和、越不抖；0 表示硬边）
        _Softness ("Edge Softness", Range(0, 2)) = 0.75

        // 深度边缘对距离敏感：用来抑制远处噪点
        _DepthFade ("Depth Fade", Range(0, 5)) = 1.0

        // ===== Toon (post) =====
        _ToonLevels ("Toon Levels", Range(2, 8)) = 4
        _ToonStrength ("Toon Strength", Range(0, 1)) = 0.8
        _ToonGamma ("Toon Gamma", Range(0.5, 2.0)) = 1.0

        // 内部折线强度（宝可梦风一般调低：0~0.3）
        _InternalLineStrength ("Internal Line Strength", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalRenderPipeline"
            "Queue"="Overlay"
        }

        Pass
        {
            Name "ScreenSpaceOutline"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Fullscreen source color (URP Blit binds this)
            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            // Depth
            TEXTURE2D_X(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            // Normals (requires DepthNormals / SSAO etc.)
            TEXTURE2D_X(_CameraNormalsTexture);
            SAMPLER(sampler_CameraNormalsTexture);

            float4 _OutlineColor;
            float  _Thickness;
            float  _DepthThreshold;
            float  _NormalThreshold;
            float  _Softness;
            float  _DepthFade;

            float  _ToonLevels;
            float  _ToonStrength;
            float  _ToonGamma;

            float  _InternalLineStrength;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv          = GetFullScreenTriangleTexCoord(v.vertexID);
                return o;
            }

            float SampleLinear01Depth(float2 uv)
            {
                float raw = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
                return Linear01Depth(raw, _ZBufferParams);
            }

            float3 SampleViewNormal(float2 uv)
            {
                float4 n = SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv);
                float3 normal = n.xyz * 2.0 - 1.0;
                return normalize(normal);
            }

            // 硬边/软边统一
            float EdgeResponse(float value, float threshold, float softness)
            {
                float s = max(softness, 1e-6);
                float a = threshold;
                float b = threshold * (1.0 + s);
                return (softness <= 1e-4) ? step(a, value) : smoothstep(a, b, value);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // 原图
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, i.uv);

                // ===== Toon (post) - 稳定台阶（比 floor 更不闪）=====
                float luma = dot(col.rgb, float3(0.299, 0.587, 0.114));
                luma = pow(max(luma, 1e-5), _ToonGamma);

                float levels = max(_ToonLevels, 2.0);
                float x = luma * (levels - 1.0);
                float baseStep = floor(x);
                float fracStep = frac(x);

                // 把 _Softness 映射成 toon 台阶的过渡宽度
                float tSoft = saturate(_Softness * 0.25); // 0..0.5 左右
                float f = smoothstep(0.5 - tSoft, 0.5 + tSoft, fracStep);
                float q = (baseStep + f) / (levels - 1.0);

                float scale = q / max(luma, 1e-5);
                float3 toonRgb = saturate(col.rgb * scale);
                col.rgb = lerp(col.rgb, toonRgb, _ToonStrength);

                // ===== Outline =====
                float2 texel = _ScreenParams.zw;
                float2 offset = texel * _Thickness;

                float dC = SampleLinear01Depth(i.uv);
                float3 nC = SampleViewNormal(i.uv);

                float dL = SampleLinear01Depth(i.uv + float2(-offset.x, 0));
                float dR = SampleLinear01Depth(i.uv + float2( offset.x, 0));
                float dU = SampleLinear01Depth(i.uv + float2(0,  offset.y));
                float dD = SampleLinear01Depth(i.uv + float2(0, -offset.y));

                float3 nL = SampleViewNormal(i.uv + float2(-offset.x, 0));
                float3 nR = SampleViewNormal(i.uv + float2( offset.x, 0));
                float3 nU = SampleViewNormal(i.uv + float2(0,  offset.y));
                float3 nD = SampleViewNormal(i.uv + float2(0, -offset.y));

                float depthEdge =
                    max(max(abs(dC - dL), abs(dC - dR)),
                        max(abs(dC - dU), abs(dC - dD)));

                // 深度阈值：用 dC^2 更快压远处噪点
                float depthScale = lerp(1.0, 1.0 + _DepthFade, dC * dC);
                float depthTh = _DepthThreshold * depthScale;
                float eDepth = EdgeResponse(depthEdge, depthTh, _Softness);

                // 法线边缘：用 1-dot 更干净（比 length(nC-nX) 少噪点）
                float normalEdge =
                    max(max(1.0 - dot(nC, nL), 1.0 - dot(nC, nR)),
                        max(1.0 - dot(nC, nU), 1.0 - dot(nC, nD)));
                float eNormal = EdgeResponse(normalEdge, _NormalThreshold, _Softness);

                // 外轮廓 + 内部折线可控（宝可梦风内部线少）
                float edge = saturate(max(eDepth, eNormal * _InternalLineStrength));

                // 合成：线条覆盖
                return lerp(col, _OutlineColor, edge);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
