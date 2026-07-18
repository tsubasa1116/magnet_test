// カメラとプレイヤーの間に入った遮蔽物用の「ディザ半透明」シェーダー(URP)。
// アルファブレンドではなく、画面のピクセルを網点状に切り抜いて半透明に見せる。
//
// 抜き方は「視線の円筒切り取り」方式:
//   ・カメラ→プレイヤーの線分に近いピクセルほど強く抜ける(_HoleRadius以内で最強)
//   ・線分から離れたピクセルは抜かない
//   → オブジェクト全体ではなく「視線を邪魔している部分だけ」に穴が開く
// さらに _FadeT (0→1) をスクリプトが時間で動かし、じわっとかかる/戻る。
//
// PlayerAim が Shader.Find("Custom/ObstacleDitherFade") で使う(Resources配下に置くこと)。
// _ObstacleFadePlayerPos は PlayerAim が毎フレーム SetGlobalVector で設定する。
Shader "Custom/ObstacleDitherFade"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Alpha("Alpha (穴の中心の不透明度)", Range(0, 1)) = 0.15
        _HoleRadius("Hole Radius (視線からこの距離まで最強)", Float) = 1.2
        _HoleSoftness("Hole Softness (穴の縁のぼかし幅)", Float) = 1.5
        _FadeT("Fade T (0=効果なし → 1=フル。スクリプトが時間で動かす)", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Alpha;
            float _HoleRadius;
            float _HoleSoftness;
            half _FadeT;
        CBUFFER_END

        // カメラ→プレイヤーの視線(グローバル。PlayerAimが毎フレーム設定)
        float3 _ObstacleFadePlayerPos;

        // ピクセルの「視線(カメラ→プレイヤーの線分)からの距離」→ 不透明度。
        // 線分に近いほど _Alpha まで下がり、離れるほど1(抜かない)。
        // _FadeT で時間方向にもなめらかに効かせる
        half FadeAlpha(float3 positionWS)
        {
            float3 a = _WorldSpaceCameraPos;
            float3 b = _ObstacleFadePlayerPos;
            float3 ab = b - a;
            float t = saturate(dot(positionWS - a, ab) / max(dot(ab, ab), 0.0001));
            float d = distance(positionWS, a + ab * t);

            half hole = saturate((d - _HoleRadius) / max(_HoleSoftness, 0.001)); // 0=穴の中心, 1=穴の外
            half alpha = lerp(_Alpha, 1.0, hole);
            return lerp(1.0, alpha, _FadeT);
        }

        // 4x4 Bayerディザ: 画面ピクセルごとに異なるしきい値でclipし、網点で抜く
        void DitherClip(float2 pixelPos, half alpha)
        {
            static const half thresholds[16] =
            {
                 0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
                12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
                 3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
                15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
            };
            uint2 p = uint2(pixelPos) & 3;
            clip(alpha - thresholds[p.y * 4 + p.x] - 0.001);
        }
        ENDHLSL

        // メイン描画(ハーフランバート+環境光の簡易ライティング。壁・敵の一時表示用途には十分)
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                DitherClip(i.positionCS.xy, FadeAlpha(i.positionWS));

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                Light mainLight = GetMainLight();
                half3 n = normalize(i.normalWS);
                half ndl = saturate(dot(n, mainLight.direction) * 0.5 + 0.5);
                half3 lighting = mainLight.color * ndl + SampleSH(n);
                return half4(albedo.rgb * lighting, 1);
            }
            ENDHLSL
        }

        // 影(抜けた部分は影も薄くなる)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct AttributesS
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct VaryingsS
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            VaryingsS vertShadow(AttributesS v)
            {
                VaryingsS o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(o.positionWS, normalWS, _LightDirection));
                return o;
            }

            half4 fragShadow(VaryingsS i) : SV_Target
            {
                DitherClip(i.positionCS.xy, FadeAlpha(i.positionWS));
                return 0;
            }
            ENDHLSL
        }

        // 深度(SSAO等の深度参照用)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            struct AttributesD
            {
                float4 positionOS : POSITION;
            };

            struct VaryingsD
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            VaryingsD vertDepth(AttributesD v)
            {
                VaryingsD o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half fragDepth(VaryingsD i) : SV_Target
            {
                DitherClip(i.positionCS.xy, FadeAlpha(i.positionWS));
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
