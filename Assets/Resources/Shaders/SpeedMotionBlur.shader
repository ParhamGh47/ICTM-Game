// Radial speed blur: samples along the line from a focus point outwards, so the middle of the screen stays
// crisp and the picture smears more and more towards the edges - the "speed" look a racing game uses.
//
// The streak is built from _MainTex, which is a half-resolution copy of the frame, and mixed back over the
// crisp _SourceTex through a radial mask. That is what lets the blur be long and wide without going ghosty:
// sampling the full-resolution frame at long range shows the taps as separate copies, whereas the softened
// copy gives one continuous smear - and the mask keeps the middle of the picture untouched by it.
//
// Three things make it read as motion rather than as a smudge:
//
//  * two bands. The near band is the tight ramp around the focus (the truck's own surroundings stream hard);
//    the far band (_Wide) is a much gentler ramp that reaches most of the way into the picture, so at speed
//    the whole frame moves instead of a ring around the player. The far band is also where the streaks
//    stretch (_Stretch), because that is where the world is travelling fastest across the screen.
//
//  * colour is held (_HoldColour). A blur is an average, and the average of any large area is a colour of its
//    own - which is why a heavy speed blur over a green level under pale blue rain turns the whole picture
//    green. Each pixel therefore keeps its own hue and saturation while it takes the smeared brightness, so
//    the smear carries motion without carrying a new colour.
//
//  * bright things streak harder than dark ones (_Highlight). Lights, wet tarmac and rain are what the eye
//    actually reads as speed, and the headlights of a dark car should not drag a grey haze with them.
//
// _TruckRect / _TruckHole are the top-down camera's problem: from above there is no sky to leave sharp and no
// vanishing point to radiate from, so the smear covers the whole picture and the truck has to be cut out of
// it by shape instead of by a clear disc. The rectangle is the truck's own projected bounds, so it hugs the
// model however the camera is angled, with a soft edge (_TruckEdge) rather than a hard cut.
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

            #define LUMA float3(0.2126, 0.7152, 0.0722)

            sampler2D _MainTex;             // the half-resolution copy the streak is built from
            float4 _MainTex_TexelSize;      // (1/width, 1/height, width, height) of that copy
            sampler2D _SourceTex;           // the crisp frame, for the middle of the picture

            float _Blur;                    // the near streak, as a fraction of the screen height
            float _Wide;                    // how much of the picture the far band reaches into, 0 - 1
            float _Stretch;                 // how much longer the streak is out where the far band is full
            float2 _Focus;                  // where the blur radiates from, in UV
            float _Clear;                   // radius around the focus left alone
            float _Falloff;                 // how sharply the blur ramps in past that
            float _SideBoost;               // how much more the sides streak than the top and bottom
            float _Outward;                 // leans the streak away from the focus
            float _Darken;                  // a touch of darkening at the edges
            float _Samples;
            float _Mask;                    // how much of this frame is taken from the streak at all
            float _MaxMix;                  // the most of any one pixel the smear may ever take over
            float _Tint;                    // diagnostic: wash the streaked part of the picture red
            float _HoldColour;              // how much of the smear keeps this pixel's own colour
            float _Highlight;               // how much brighter taps are favoured over darker ones
            float4 _TruckRect;              // xy = the truck's centre, zw = its half size, aspect corrected
            float _TruckHole;               // 0 - 1, how completely the truck is cut out of the smear
            float _TruckEdge;               // the fade around that cut, in the same units

            #define MAX_SAMPLES 24

            float4 frag (v2f_img i) : SV_Target
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

                // The near band: nothing inside the clear disc, full strength at the edge of the picture. The
                // smoothstep keeps the onset a gradient rather than a visible ring.
                float edge = saturate((radius - _Clear) / max(1e-3, 1.0 - _Clear));
                edge = smoothstep(0.0, 1.0, edge);
                edge = pow(edge, max(0.05, _Falloff));

                // The far band: the same idea but starting much closer in, so the effect reaches across the
                // picture instead of hugging the player. _Wide says how much of it there is at all.
                float wide = smoothstep(_Clear * 0.3, 1.0, radius);

                float band = max(edge, wide * _Wide);

                // A racing game reads speed mostly out of the sides of the picture, so a pixel level with the
                // focus streaks the most and one directly above or below it the least.
                float sideways = radius > 1e-5 ? abs(delta.x) / radius : 0.0;
                sideways = lerp(1.0, _SideBoost, sideways);

                // The streak lengthens with the band as well as with the speed: the far band is where the
                // world is crossing the screen fastest, so that is where it should trail the most.
                //
                // NOT called "length": that is an HLSL intrinsic - and one this very function calls above -
                // and a local of the same name is taken badly by the compiler. Same reason the offset below
                // is not called "step" and the rectangle above is not called "distance".
                float streakLength = _Blur * lerp(1.0, _Stretch, wide * _Wide);

                float2 streak = direction * (streakLength * band * sideways);

                int count = (int)clamp(_Samples, 2.0, (float)MAX_SAMPLES);

                // A per-pixel nudge, the same for every tap, so the streak's own edges are dithered rather
                // than banded. Not called "noise", which is an intrinsic too.
                float grain = frac(sin(dot(i.uv * _ScreenParams.xy, float2(12.9898, 78.233))) * 43758.5453);
                float2 dither = (grain - 0.5) * _MainTex_TexelSize.xy * 0.5;

                float4 smear = float4(0.0, 0.0, 0.0, 0.0);
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

                    // Each tap is nudged a little further along the streak, by an amount that differs for
                    // every pixel and every tap - a low tap count then reads as one continuous smear instead
                    // of as a row of copies. The golden ratio keeps the offsets from lining up.
                    float jitter = (frac(grain + (float)s * 0.618034) - 0.5) * (1.2 / (float)count);

                    float4 tap = tex2D(_MainTex, i.uv + streak * (t + jitter) + dither);

                    // Bright taps count for more, so the lights, the wet road and the rain are what leave the
                    // trail rather than whatever happens to be in the way. Normalised by the same weights
                    // below, so this reshapes the streak rather than brightening it.
                    weight *= lerp(1.0, dot(tap.rgb, LUMA), _Highlight);

                    smear += tap * weight;
                    total += weight;
                }

                smear /= max(total, 1e-4);

                float4 sharp = tex2D(_SourceTex, i.uv);

                // Motion, not colour: the smear's brightness with this pixel's own hue and saturation. Without
                // it the blur hands back the average colour of the area it sampled, which is what turns a
                // green level under pale rain into a green wash.
                float3 smearColour = smear.rgb;
                float3 sharpColour = sharp.rgb;

                float lumSmear = dot(smearColour, LUMA);
                float lumSharp = max(dot(sharpColour, LUMA), 0.02);
                float3 held = sharpColour * clamp(lumSmear / lumSharp, 0.0, 3.0);
                float3 motion = lerp(smearColour, held, saturate(_HoldColour));

                // Both the streak's length (above) and how much of the pixel it takes over are driven by how
                // far out this pixel is, so the change from crisp middle to streaming edge is a gradient
                // rather than a ring.
                //
                // _MaxMix then caps that: the far band reaches most of the picture, and letting it take the
                // pixel over completely everywhere is what turns a speed effect into a smudge. Kept under 1,
                // some of the crisp frame always shows through, so the cost is detail rather than the picture
                // itself - a light smear over the whole of a race reads far better than a heavy one around the
                // player, which is what looked wrong from the top-down camera.
                float mix = saturate(_Mask * band) * saturate(_MaxMix);

                // The top-down view: cut the truck out of the smear by its own outline. Signed distance to
                // the rectangle, so the fade is even all the way round it and the corners are rounded.
                if (_TruckHole > 0.0)
                {
                    float2 p = float2(i.uv.x * aspect, i.uv.y);
                    float2 q = abs(p - _TruckRect.xy) - max(_TruckRect.zw, float2(0.0, 0.0));

                    // NOT called "distance": that is an HLSL intrinsic, and a local of the same name is
                    // taken badly by some compilers - the same reason the tap offset above avoids "step".
                    float gap = length(max(q, float2(0.0, 0.0))) + min(max(q.x, q.y), 0.0);

                    mix *= 1.0 - _TruckHole * (1.0 - smoothstep(0.0, max(1e-3, _TruckEdge), gap));
                }

                float3 colour = lerp(sharpColour, motion, mix);

                // Pulling a little light out of the edges keeps the eye on the road, and it follows the blur
                // so a slow lap is untouched.
                colour *= lerp(1.0, 1.0 - _Darken, band);

                // Turning the effect down and watching where the red is (and is not) is the quickest way to
                // tell whether it is running at all, and whether the sharp middle is where you want it.
                colour = lerp(colour, float3(1.0, 0.0, 0.0), _Tint * mix);

                // The source alpha, untouched: nothing downstream blends this buffer, but a screen capture or
                // a second effect in the chain should not be handed something invented here.
                return float4(colour, sharp.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
