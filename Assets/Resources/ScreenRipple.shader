// 咆哮用の画面リップル(URP)。
// 画面中心から外側へ「波」が広がり、通過する場所の映像を radial に歪ませる。
// パーティクルではなくスクリーンスペースの歪みなので、カメラの画そのものが波打つ。
//
// 使い方: カメラの前に置いたQuadに貼る(頂点シェーダーで画面全体に引き伸ばすので
// Quadの位置/サイズはカリング回避用でしかない)。_Progress を 0→1 へ動かすと波が走る。
// ※URPアセットの「Opaque Texture」がONであること(SceneColorを使うため)。
Shader "Magnet/ScreenRipple"
{
    Properties
    {
        _Progress("Progress (0=中心 → 1=画面外)", Range(0, 1)) = 0
        _Amplitude("Amplitude (歪みの強さ)", Float) = 0.035
        _RingWidth("Ring Width (波の幅)", Float) = 0.08
        _MaxRadius("Max Radius (波が届く半径。0.5=画面端)", Float) = 0.8
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ScreenRipple"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Progress;
                float _Amplitude;
                float _RingWidth;
                float _MaxRadius;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                // メッシュ形状に関係なく画面全体を覆う(QuadのUVをそのままクリップ座標へ)
                float2 uv = v.uv;
                o.positionCS = float4(uv * 2.0 - 1.0, 0.0001, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                o.positionCS.y = -o.positionCS.y;
                #endif
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS.xy);

                // 画面中心からの距離(アスペクト補正して真円に)
                float2 d = uv - float2(0.5, 0.5);
                d.x *= _ScaledScreenParams.x / _ScaledScreenParams.y;
                float dist = length(d);

                // 現在の波の半径と、そこを中心にしたガウス窓
                float wavefront = _Progress * _MaxRadius;
                float band = (dist - wavefront) / max(_RingWidth, 0.001);
                float ring = exp(-band * band);

                // 終盤ほど減衰(波が遠くへ行くほど弱まる)
                float fade = saturate(1.0 - _Progress);

                // 波打ち: リング内でUVを radial 方向へ揺らして映像を歪ませる
                float disp = ring * fade * _Amplitude * sin(band * 6.2831853);
                float2 dir = dist > 0.0001 ? normalize(uv - float2(0.5, 0.5)) : float2(0, 0);
                half3 col = SampleSceneColor(uv - dir * disp);

                // リングの外はほぼ透明(元の画面がそのまま見える)
                return half4(col, saturate(ring * fade));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
