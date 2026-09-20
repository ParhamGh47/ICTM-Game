// Radial speed blur: samples along the line from a focus point outwards, so the middle of the screen stays
// crisp and the picture smears more and more towards the edges - the "speed" look a racing game uses.
//
// The streak is built from _MainTex, which is a half-resolution copy of the frame, and mixed back over the
// crisp _SourceTex through a radial mask. That is what lets the blur be long and wide without going ghosty:
// sampling the full-resolution frame at long range shows the taps as separate copies, whereas the softened
// copy gives one continuous smear - and the mask keeps the middle of the picture untouched by it.
//
// Everything that decides how much and where comes from SpeedMotionBlur.cs; this only does the taps.
//
// Lives in Resources so the shader is always in a build: a shader that is only ever reached through
// Shader.Find gets stripped by the build's shader stripping, and the effect would silently do nothing.
Shader "Hidden/Freebuff/SpeedMotionBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;             // the half-resolution copy the streak is built from
            float4 _MainTex_TexelSize;      // (1/width, 1/height, width, height) of that copy
            sampler2D _SourceTex;           // the crisp frame, for the middle of the picture

            float _Blur;                    // longest streak, as a fraction of the screen
            float2 _Focus;                  // where the blur radiates from, in UV
            float _Clear;                   // radius around the focus left alone
            float _Falloff;                 // how sharply the blur ramps in past that
            float _SideBoost;               // how much more the sides streak than the top and bottom
            float _Outward;                 // leans the streak away from the focus
            float _Darken;                  // a touch of darkening at the edges
            float _Samples;
            float _Mask;                    // how much of this frame is taken from the streak at all
            float _Tint;                    // diagnostic: wash the streaked part of the picture red

            #define MAX_SAMPLES 24

            fixed4 frag (v2f_img i) : SV_Target
            {
                // The screen is not square, so the radial distance is measured in screen shape rather than in
                // UV - otherwise the "radius" would be an ellipse and the sides would blur on their own.
                float aspect = _MainTex_TexelSize.z / max(1e-5, _MainTex_TexelSize.w);

                float2 delta = i.uv - _Focus;
                delta.x *= aspect;

                float radius = length(delta);

                // Where this pixel is, as a unit direction on screen, back in UV.
                float2 direction = radius > 1e-5 ? delta / radius : float2(0.0, 0.0);
                direction.x /= aspect;

                float edge = saturate((radius - _Clear) / max(1e-3, 1.0 - _Clear));
                edge = pow(edge, max(0.05, _Falloff));

                // A racing game reads speed mostly out of the sides of the picture, so a pixel level with the
                // focus streaks the most and one directly above or below it the least.
                float sideways = radius > 1e-5 ? abs(delta.x) / radius : 0.0;
                sideways = lerp(1.0, _SideBoost, sideways);

                // NOT called "step": that is an HLSL intrinsic, and some compilers take a local of the same
                // name badly.
                float2 streak = direction * (_Blur * edge * sideways);

                int count = (int)clamp(_Samples, 2.0, (float)MAX_SAMPLES);

                // A per-pixel nudge, the same for every tap, so the streak's own edges are dithered rather
                // than banded.
                float noise = frac(sin(dot(i.uv * _ScreenParams.xy, float2(12.9898, 78.233))) * 43758.5453);
                float2 dither = (noise - 0.5) * _MainTex_TexelSize.xy * 0.5;

                fixed4 smear = fixed4(0.0, 0.0, 0.0, 0.0);
                float total = 0.0;

                for (int s = 0; s < MAX_SAMPLES; s++)
                {
                    if (s >= count) break;

                    // Taps run from behind the pixel to in front of it, leaning outward, and are weighted
                    // towards the pixel itself so the blur stays a smear rather than a double image. The
                    // floor matters at the lowest tap counts, where the two ends are the whole streak and a
                    // triangle that reached zero at both of them would leave nothing to divide by.
                    float t = (float)s / (float)(count - 1) - 0.5 + _Outward;
                    float weight = max(0.02, 1.0 - 2.0 * abs(t - _Outward));

                    smear += tex2D(_MainTex, i.uv + streak * t + dither) * weight;
                    total += weight;
                }

                smear /= max(total, 1e-4);

                fixed4 sharp = tex2D(_SourceTex, i.uv);

                // Both the streak's length (above) and how much of the pixel it takes over are driven by how
                // far out this pixel is, so the change from crisp middle to streaming edge is a gradient
                // rather than a ring.
                float mix = saturate(_Mask * edge);

                fixed4 colour = lerp(sharp, smear, mix);

                // Pulling a little light out of the edges keeps the eye on the road, and it follows the blur
                // so a slow lap is untouched.
                colour.rgb *= lerp(1.0, 1.0 - _Darken, edge);

                // Turning the effect down and watching where the red is (and is not) is the quickest way to
                // tell whether it is running at all, and whether the sharp middle is where you want it.
                colour.rgb = lerp(colour.rgb, float3(1.0, 0.0, 0.0), _Tint * mix);

                return colour;
            }
            ENDCG
        }
    }

    Fallback Off
}
