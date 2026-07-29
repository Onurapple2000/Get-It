Shader "Custom/GroundHole"
{
    Properties
    {
        _BaseColor   ("Base Color",   Color)   = (0.65, 0.65, 0.65, 1)
        _BaseMap     ("Albedo",       2D)       = "white" {}
        _Smoothness  ("Smoothness",   Range(0,1)) = 0.15
        _Metallic    ("Metallic",     Range(0,1)) = 0.0
    }
    SubShader
    {
        // Same queue as normal opaque — renders AFTER HoleMask (Geometry-10).
        // Stencil skips pixels where the hole mask wrote 1.
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Stencil
            {
                Ref 1
                Comp NotEqual   // Draw only where stencil != 1 (outside hole)
                Pass Keep
            }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4   _BaseColor;
                float4  _BaseMap_ST;
                half    _Smoothness;
                half    _Metallic;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   vni = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = vpi.positionCS;
                OUT.positionWS  = vpi.positionWS;
                OUT.normalWS    = vni.normalWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor   = ComputeFogFactor(vpi.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texCol  = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                InputData   inputData  = (InputData)0;
                inputData.positionWS   = IN.positionWS;
                inputData.normalWS     = normalize(IN.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord  = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord     = IN.fogFactor;
                inputData.bakedGI      = SampleSH(inputData.normalWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo      = texCol.rgb;
                surfaceData.alpha       = 1.0;
                surfaceData.metallic    = _Metallic;
                surfaceData.smoothness  = _Smoothness;
                surfaceData.occlusion   = 1.0;
                surfaceData.normalTS    = half3(0,0,1);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, IN.fogFactor);
                return color;
            }
            ENDHLSL
        }

        // Shadow caster pass so ground casts shadows correctly.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
}
