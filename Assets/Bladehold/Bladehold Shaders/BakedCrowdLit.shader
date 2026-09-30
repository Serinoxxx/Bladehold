// URP Lit for baked crowd instances (BakedCrowdRenderer). Every pass is the stock URP Lit pass
// with a vertex wrapper that skins the vertex from the baked bone texture first, then hands a
// normal Attributes struct to URP's own vertex function, so lighting, Forward+, shadows, SSAO and
// decals match regular Lit materials. Pass pragmas are copied from the installed URP Lit.shader
// (URP 17.3); the DOTS instancing variant is left out (plain GPU instancing only).
Shader "Bladehold/Baked Crowd Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}
        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}
        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        [NoScaleOffset] _CrowdBoneTex("Baked Bone Matrices", 2D) = "black" {}

        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 1.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "Queue" = "AlphaTest"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CrowdLitVertex
            #pragma fragment LitPassFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #include "BakedCrowdSkinning.hlsl"

            struct CrowdAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 texcoord : TEXCOORD0;
                float4 boneIndices : TEXCOORD4;
                float4 boneWeights : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings CrowdLitVertex(CrowdAttributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Attributes attributes = (Attributes)0;
                CrowdSkin(input.positionOS.xyz, input.normalOS, input.tangentOS.xyz, input.boneIndices, input.boneWeights,
                          attributes.positionOS.xyz, attributes.normalOS, attributes.tangentOS.xyz);
                attributes.positionOS.w = 1.0;
                attributes.tangentOS.w = input.tangentOS.w;
                attributes.texcoord = input.texcoord;
                UNITY_TRANSFER_INSTANCE_ID(input, attributes);
                return LitPassVertex(attributes);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CrowdShadowVertex
            #pragma fragment ShadowPassFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            #include "BakedCrowdSkinning.hlsl"

            struct CrowdAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                float4 boneIndices : TEXCOORD4;
                float4 boneWeights : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings CrowdShadowVertex(CrowdAttributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Attributes attributes = (Attributes)0;
                float3 unusedTangent;
                CrowdSkin(input.positionOS.xyz, input.normalOS, float3(1, 0, 0), input.boneIndices, input.boneWeights,
                          attributes.positionOS.xyz, attributes.normalOS, unusedTangent);
                attributes.positionOS.w = 1.0;
                attributes.texcoord = input.texcoord;
                UNITY_TRANSFER_INSTANCE_ID(input, attributes);
                return ShadowPassVertex(attributes);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CrowdDepthVertex
            #pragma fragment DepthOnlyFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            #include "BakedCrowdSkinning.hlsl"

            struct CrowdAttributes
            {
                float4 position : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                float4 boneIndices : TEXCOORD4;
                float4 boneWeights : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings CrowdDepthVertex(CrowdAttributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Attributes attributes = (Attributes)0;
                float3 unusedNormal, unusedTangent;
                CrowdSkin(input.position.xyz, input.normalOS, float3(1, 0, 0), input.boneIndices, input.boneWeights,
                          attributes.position.xyz, unusedNormal, unusedTangent);
                attributes.position.w = 1.0;
                attributes.texcoord = input.texcoord;
                UNITY_TRANSFER_INSTANCE_ID(input, attributes);
                return DepthOnlyVertex(attributes);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CrowdDepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            #include "BakedCrowdSkinning.hlsl"

            struct CrowdAttributes
            {
                float4 positionOS : POSITION;
                float4 tangentOS : TANGENT;
                float2 texcoord : TEXCOORD0;
                float3 normal : NORMAL;
                float4 boneIndices : TEXCOORD4;
                float4 boneWeights : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings CrowdDepthNormalsVertex(CrowdAttributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Attributes attributes = (Attributes)0;
                CrowdSkin(input.positionOS.xyz, input.normal, input.tangentOS.xyz, input.boneIndices, input.boneWeights,
                          attributes.positionOS.xyz, attributes.normal, attributes.tangentOS.xyz);
                attributes.positionOS.w = 1.0;
                attributes.tangentOS.w = input.tangentOS.w;
                attributes.texcoord = input.texcoord;
                UNITY_TRANSFER_INSTANCE_ID(input, attributes);
                return DepthNormalsVertex(attributes);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
