// Unlit glow overlays for the campaign map diorama, drawn over the terrain with no depth write.
//  _Shape 0 = route ribbon: uv.x runs along the road in world units, uv.y across it (0..1). Dashes march
//             along uv.x at _DashSpeed (0 = solid dashes), soft edges across.
//  _Shape 1 = ground ring under an open castle: uv is -1..1 over a quad, a soft ring that pulses.
// Colour and animation are per-renderer (MaterialPropertyBlock) from CampaignDioramaRoad / CampaignDioramaSite.
Shader "Bladehold/Diorama Glow FX"
{
    Properties
    {
        [HDR] _GlowColor("Colour", Color) = (1, 0.85, 0.3, 1)
        [Enum(Road,0,Ring,1)] _Shape("Shape", Float) = 0
        _DashLength("Dash Repeat (world units)", Float) = 0.6
        _DashFill("Dash Fill", Range(0, 1)) = 0.55
        _DashSpeed("Dash Speed", Float) = 0.6
        _PulseSpeed("Ring Pulse Speed", Float) = 2.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "DioramaGlow"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -2, -2

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                float _Shape;
                float _DashLength;
                float _DashFill;
                float _DashSpeed;
                float _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                half alpha;
                if (_Shape < 0.5)
                {
                    // Road: soft-edged dashes marching from the start of the road to its end.
                    float across = abs(i.uv.y - 0.5) * 2.0;
                    half edge = saturate((1.0 - across) * 3.0);
                    float phase = frac(i.uv.x / max(_DashLength, 0.01) - t * _DashSpeed);
                    half dash = smoothstep(0.0, 0.08, phase) * (1.0 - smoothstep(_DashFill, _DashFill + 0.08, phase));
                    alpha = edge * dash;
                }
                else
                {
                    // Ring: a thin bright band near the rim inside a soft halo, breathing over time.
                    float r = length(i.uv);
                    half pulse = 0.65 + 0.35 * sin(t * _PulseSpeed);
                    half band = exp(-pow((r - 0.8) * 9.0, 2.0));
                    half outer = exp(-pow((r - 0.8) * 3.5, 2.0)) * 0.18;
                    alpha = (band + outer) * pulse * step(r, 1.0);
                }
                return half4(_GlowColor.rgb, saturate(alpha * _GlowColor.a));
            }
            ENDHLSL
        }
    }
}
