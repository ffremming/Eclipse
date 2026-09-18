// The black motes drifting through the world.
//
// The problem this shader exists to solve: black particles in a dark world are invisible. Drawn
// naively they are a black sprite on a near-black background and the player sees nothing, so the
// atmosphere costs fill rate and returns no image. Two things make them read anyway.
//
// First, they are drawn as OCCLUDERS, not as emitters. Alpha-blended towards a colour darker than
// whatever is behind them, so a mote crossing the bruised horizon band or a patch of the player's
// own lit ground is visible as an absence. Against the darkest part of the sky it disappears,
// which is correct — the eye should catch them near light and lose them in the dark, the way real
// airborne grit behaves.
//
// Second, they catch the player's light. A mote inside the lantern's reach picks up a rim in the
// shared light palette, so drifting through your own light lights the air in front of you. That
// rim is the whole reason the effect is worth having: it turns the particles into a readout of
// where the player's light actually reaches, which in a game about carrying light is information,
// not decoration.
//
// The light position and reach arrive as shader globals rather than per-material properties.
// WorldAtmosphere writes them from whatever is currently the player's light source, so a mote
// system anywhere in the world responds without being wired to anything.
Shader "SpaceGame/AshMote"
{
    Properties
    {
        [Header(The mote)]
        _MoteColor    ("Mote Colour",            Color)         = (0.008, 0.008, 0.010, 1)
        _Opacity      ("Opacity",                Range(0, 1))   = 0.85
        _EdgeSoftness ("Edge Softness",          Range(0, 1))   = 0.35

        [Header(Catching the players light)]
        _CatchStrength("Light Catch Strength",   Range(0, 8))   = 2.2
        _CatchPower   ("Light Catch Falloff",    Range(0.5, 8)) = 2.5
        _CatchEnergy  ("Light Catch Energy Bias",Range(0, 1))   = 0.45

        [Header(Stylisation)]
        _Bands        ("Posterise Bands (0 = off)", Range(0, 16)) = 5
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "AshMote"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Light/LightPalette.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MoteColor;
                float  _Opacity;
                float  _EdgeSoftness;
                float  _CatchStrength;
                float  _CatchPower;
                float  _CatchEnergy;
                float  _Bands;
            CBUFFER_END

            // Written by WorldAtmosphere. xyz = world position of the player's light, w = reach in
            // metres. A reach of 0 means there is no light right now, and every catch term folds
            // to zero without a branch.
            float4 _PlayerLightPos;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = pos.positionCS;
                OUT.positionWS  = pos.positionWS;
                OUT.uv          = IN.uv;
                OUT.color       = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Round mote from the quad's UVs. No texture: a mote is a few pixels across and a
                // sprite sheet for that is memory spent on something no one can resolve.
                float2 centred = IN.uv * 2.0 - 1.0;
                float radius = length(centred);
                float mask = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, radius);
                if (mask <= 0.0) discard;

                // Distance into the player's light, 0 outside it and 1 at the source.
                float reach = max(_PlayerLightPos.w, 0.0);
                float toLight = distance(IN.positionWS, _PlayerLightPos.xyz);
                float catchT = reach > 0.0
                    ? pow(saturate(1.0 - toLight / reach), _CatchPower)
                    : 0.0;

                // Quantised so the motes pop between a few discrete brightnesses as they drift
                // through the light, rather than sliding smoothly. The stepping is what makes it
                // read as stylised instead of as a soft particle system.
                if (_Bands >= 1.0)
                {
                    catchT = floor(catchT * _Bands + 0.5) / _Bands;
                }

                // The rim colour comes from the shared palette, biased low so motes pick up the
                // turquoise fringe at the edge of the light and only go warm right at the source.
                // Same ramp as the orb and the beam, so ash lit by the player's light is visibly
                // the same light.
                float3 caught = LightEmission(catchT * (1.0 - _CatchEnergy) + _CatchEnergy * catchT * catchT,
                                             _CatchStrength * catchT);

                float3 rgb = _MoteColor.rgb + caught;

                // Opacity drops as the mote lights up: a mote that is catching light should read
                // as glowing grit, not as a black hole with a bright edge.
                float alpha = mask * _Opacity * IN.color.a * saturate(1.0 - catchT * 0.65);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
