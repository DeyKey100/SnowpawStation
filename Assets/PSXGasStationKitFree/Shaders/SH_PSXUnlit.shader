// The pack's look in Built-in and URP: one unlit pass (URP draws it as SRPDefaultUnlit), the texture times the baked vertex shading, with the PS1's wobbling vertices
// and swimming textures as sliders. The swim is off by default: Fab's review fails it as a visual defect seen close.
Shader "PSXGasStationKitFree/PSX Unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _SnapRows ("Vertex Snap (rows on screen, 0 = off)", Float) = 240
        _Affine ("Affine Texture Warp", Range(0, 1)) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            CBUFFER_START(UnityPerMaterial) // for URP's SRP Batcher
            float4 _MainTex_ST;
            fixed4 _Color;
            float _SnapRows, _Affine, _Cutoff;
            CBUFFER_END

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 affine : TEXCOORD1; // uv * w and w: their ratio interpolates linearly on screen
                fixed4 color : COLOR;
                UNITY_FOG_COORDS(2)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                if (_SnapRows > 0)
                {
                    float2 grid = float2(_SnapRows * _ScreenParams.x / _ScreenParams.y, _SnapRows) * 0.5;
                    o.pos.xy = round(o.pos.xy / o.pos.w * grid) / grid * o.pos.w;
                }
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.affine = float3(o.uv * o.pos.w, o.pos.w);
                o.color = v.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                o.color.rgb = GammaToLinearSpace(v.color.rgb); // the shading was baked in sRGB
                #endif
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = lerp(i.uv, i.affine.xy / i.affine.z, _Affine);
                fixed4 c = tex2D(_MainTex, uv) * i.color * _Color;
                clip(c.a - _Cutoff);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
