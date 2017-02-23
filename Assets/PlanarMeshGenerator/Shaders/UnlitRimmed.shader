Shader "2DMeshGen/Unlit_Rimmed"
{
	Properties{
		_EdgeTexture ("EdgeTexture", 2D) = "white" {}
		_ColorEdge ("EdgeColor", Color) = (1,1,1,1)
		_CenterTexture("CenterTexture", 2D) = "white"{}
		_ColorCenter ("CenterColor",Color) = (1,1,1,1)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			struct appdata{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float3 normal:NORMAL;
				float2 isRim : TEXCOORD1;
			};

			struct v2f{
				float2 edgeUV : TEXCOORD0;
				float2 centerUV : TEXCOORD1;
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
				float2 normal: TEXCOORD2;
				float isRim : TEXCOORD3;
			};

			sampler2D _EdgeTexture;
			float4 _EdgeTexture_ST;
			float4 _ColorEdge;
			sampler2D _CenterTexture;
			float4 _CenterTexture_ST;
			float4 _ColorCenter;
			
			v2f vert (appdata v){
				v2f o;
				o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
				o.edgeUV = TRANSFORM_TEX(v.uv, _EdgeTexture);
				o.centerUV = TRANSFORM_TEX(v.uv, _CenterTexture);
				o.normal = normalize(float2(v.normal.x, v.normal.y));
				o.isRim = v.isRim.x;
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target{
				// sample the texture
				i.isRim = clamp(0.0,1.0,i.isRim);
				fixed4 col = tex2D(_EdgeTexture, i.edgeUV)*_ColorEdge;
				fixed4 col2 = tex2D(_CenterTexture, i.centerUV)*_ColorCenter;
				col = col*i.isRim + (1.0 - i.isRim)*col2;

				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}
	}
}
