Shader "Custom/UnlitVertexColorPulse"
{
    Properties
    {
        _PulsePos ("Pulse Position", Range(0,1)) = 0
        _PulseWidth ("Pulse Width", Range(0.01,0.5)) = 0.12
        _PulseIntensity ("Pulse Intensity", Range(0,5)) = 1
        _PulseColor ("Pulse Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _PulsePos;
            float _PulseWidth;
            float _PulseIntensity;
            fixed4 _PulseColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
                float2 uv    : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Base vertex color
                fixed4 c = i.color;

                // Use vertex alpha as a pulse mask (1 = pulse on, 0 = pulse off).
                // This lets us selectively animate only some connections within a single mesh.
                float pulseMask = saturate(c.a);
                c.a = 1.0;

                // Pulse travels along uv.x (0=start, 1=end)
                float d = i.uv.x - _PulsePos;
                d = abs(d);

                float sigma = max(1e-4, _PulseWidth);
                float pulse = exp(- (d * d) / (2.0 * sigma * sigma));

                pulse *= pulseMask;

                // Make the pulse VERY visible: add a colored streak + force alpha up.
                c.rgb = saturate(c.rgb + _PulseColor.rgb * (pulse * _PulseIntensity));
                c.a = saturate(max(c.a, pulse));

                return c;
            }
            ENDCG
        }
    }
}
