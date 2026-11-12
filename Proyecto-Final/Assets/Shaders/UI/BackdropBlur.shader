// Backdrop blur for modals (see UIBackdrop). One shader, two uses of the same material:
//  - _Direction = (n,0) or (0,n): one separable 9-tap gaussian pass, n texels apart; UIBackdrop blits the world snapshot through it.
//  - _Direction = (0,0): plain display of the blurred texture on the RawImage (UI tint color and alpha applied).
Shader "MagicGarden/UI/BackdropBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Direction ("Blur direction in texels (0 = display only)", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "BackdropBlur"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _Direction;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                bool blurPass = abs(_Direction.x) + abs(_Direction.y) > 0;
                if (!blurPass) return tex2D(_MainTex, i.uv) * i.color;

                // Linear-sampled 9-tap gaussian (5 fetches).
                float2 step = _Direction.xy * _MainTex_TexelSize.xy;
                fixed3 sum = tex2D(_MainTex, i.uv).rgb * 0.2270270270;
                sum += tex2D(_MainTex, i.uv + step * 1.3846153846).rgb * 0.3162162162;
                sum += tex2D(_MainTex, i.uv - step * 1.3846153846).rgb * 0.3162162162;
                sum += tex2D(_MainTex, i.uv + step * 3.2307692308).rgb * 0.0702702703;
                sum += tex2D(_MainTex, i.uv - step * 3.2307692308).rgb * 0.0702702703;
                return fixed4(sum, 1);
            }
            ENDCG
        }
    }
}
