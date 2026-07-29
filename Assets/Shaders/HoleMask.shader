Shader "Custom/HoleMask"
{
    Properties {}
    SubShader
    {
        // Renders before ground (Geometry = 2000).
        // Writes stencil=1 at hole pixels — the ground then skips those pixels via stencil test.
        // ZWrite Off: intentional — we must NOT write depth here. If we wrote depth at Y=0,
        // falling objects (Y<0) would fail the depth test inside the hole and turn invisible.
        // The stencil alone is enough to cut the hole in the ground.
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-10" }

        Pass
        {
            ZWrite Off
            ColorMask 0
            Cull Off

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return half4(0,0,0,0); }
            ENDHLSL
        }
    }
}
