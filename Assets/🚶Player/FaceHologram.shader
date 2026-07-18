// 顔用ホログラムシェーダー。
// 顔テクスチャ(表情差分込み)をティント色で発光表示し、
// 走査線・フレネル(輪郭発光)・ちらつきでホログラム感を出す。
Shader "Magnet/FaceHologram"
{
	Properties
	{
		_MainTex ("Face Texture", 2D) = "white" {}
		_Color ("Hologram Tint", Color) = (0.4, 0.9, 1, 1)
		_Alpha ("Base Alpha", Range(0, 1)) = 0.8
		_ScanlineDensity ("Scanline Density", Float) = 200
		_ScanlineSpeed ("Scanline Speed", Float) = 1.5
		_ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.35
		_FresnelPower ("Fresnel Power", Float) = 2.5
		_FresnelStrength ("Fresnel Strength", Float) = 1.2
		_FlickerSpeed ("Flicker Speed", Float) = 6
		_FlickerStrength ("Flicker Strength", Range(0, 1)) = 0.12
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
		Blend SrcAlpha One   // 加算寄り(光っている感じ)
		ZWrite Off
		Cull Back

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _Color;
			float _Alpha;
			float _ScanlineDensity;
			float _ScanlineSpeed;
			float _ScanlineStrength;
			float _FresnelPower;
			float _FresnelStrength;
			float _FlickerSpeed;
			float _FlickerStrength;

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
				float3 worldNormal : TEXCOORD2;
				float3 viewDir : TEXCOORD3;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				o.worldNormal = UnityObjectToWorldNormal(v.normal);
				o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 tex = tex2D(_MainTex, i.uv);

				// 走査線(ワールド高さ基準で下へ流れる)
				float scan = sin(i.worldPos.y * _ScanlineDensity - _Time.y * _ScanlineSpeed * 10.0);
				float scanMask = 1.0 - _ScanlineStrength * (0.5 + 0.5 * scan);

				// フレネル(視線に対して斜めの面=輪郭ほど光る)
				float fres = pow(1.0 - saturate(dot(normalize(i.worldNormal), i.viewDir)), _FresnelPower);

				// ちらつき(2つの周期を混ぜて不規則っぽく)
				float flicker = 1.0 - _FlickerStrength
					* (0.5 + 0.5 * sin(_Time.y * _FlickerSpeed * 7.3) * sin(_Time.y * _FlickerSpeed * 2.9));

				fixed3 col = tex.rgb * _Color.rgb;
				col += _Color.rgb * fres * _FresnelStrength;

				float alpha = _Alpha * scanMask * flicker;
				return fixed4(col, alpha);
			}
			ENDCG
		}
	}
	Fallback Off
}
