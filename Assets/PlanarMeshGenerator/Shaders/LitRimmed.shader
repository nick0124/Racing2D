Shader "2DMeshGen/Lit Surface Rimmed" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_EdgeTex ("EdgeTex (RGB)", 2D) = "white" {}
		_CenterTex("CenterTex (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM
		// Physically-based std. lighting, plus shadows on all light types
		#pragma surface surf Standard fullforwardshadows vertex:vert
		#pragma target 3.0 //for nicer lighting

		sampler2D _EdgeTex;
		sampler2D _CenterTex;
		struct Input {
			float2 uv_EdgeTex;
			float2 uv_CenterTex;
			float isRim;
		};

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;

		void vert(inout appdata_full v, out Input o) {
			UNITY_INITIALIZE_OUTPUT(Input, o);
			o.isRim =  v.texcoord1.x;
		}

		void surf (Input IN, inout SurfaceOutputStandard o) {
			// Albedo comes from a texture tinted by color
			fixed4 cEdge = tex2D (_EdgeTex, IN.uv_EdgeTex) * _Color;
			fixed4 cCent = tex2D(_CenterTex, IN.uv_CenterTex) * _Color;
			float t =  clamp(0.0, 1.0, IN.isRim);
			float4 color = t*cEdge + (1.0 - t)*cCent;

			o.Albedo = color.rgb;
			// Metallic and smoothness come from slider variables
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = color.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
