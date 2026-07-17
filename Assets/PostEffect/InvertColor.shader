Shader "PostProcess/InvertColor"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Unity6のURPでポストエフェクトを作るための標準ライブラリ
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // 画面の色を加工する処理
            half4 Frag(Varyings input) : SV_Target
            {
                // _BlitTexture に、直前までのゲーム画面が入っています
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                
                // 実験として、画面のRGB（色）を反転させます！
                return half4(1.0 - color.rgb, color.a);
            }
            ENDHLSL
        }
    }
}