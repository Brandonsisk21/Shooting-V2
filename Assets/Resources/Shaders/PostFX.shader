// Space Grunts camera post effects (built-in pipeline), driven by PostFX.cs.
// Pass 0: bloom bright-pass, 1: downsample, 2: additive upsample, 3: final composite
// (bloom + exposure + ACES tone mapping + saturation/contrast + vignette).
Shader "Hidden/SpaceGrunts/PostFX"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float _Threshold, _Knee, _Intensity, _Exposure, _Saturation, _Contrast, _Vignette;

    // Work in linear light even if the project uses Gamma color space.
    half3 ToLinear(half3 c)
    {
    #ifdef UNITY_COLORSPACE_GAMMA
        return GammaToLinearSpace(c);
    #else
        return c;
    #endif
    }

    half3 ToOutput(half3 c)
    {
    #ifdef UNITY_COLORSPACE_GAMMA
        return LinearToGammaSpace(c);
    #else
        return c;
    #endif
    }

    half3 Box(float2 uv, float delta)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-delta, -delta, delta, delta);
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }

    // Soft-knee threshold: only the brightest parts of the image glow.
    half3 Prefilter(half3 c)
    {
        half brightness = max(c.r, max(c.g, c.b));
        half soft = clamp(brightness - _Threshold + _Knee, 0, 2 * _Knee);
        soft = soft * soft / (4 * _Knee + 1e-5);
        half contribution = max(soft, brightness - _Threshold) / max(brightness, 1e-5);
        return c * contribution;
    }

    // Narkowicz ACES fit.
    half3 Aces(half3 x)
    {
        return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
    }

    half4 FragPrefilter(v2f_img i) : SV_Target
    {
        half3 c = min(ToLinear(Box(i.uv, 1)), 64.0); // clamp fireflies
        return half4(Prefilter(c), 1);
    }

    half4 FragDown(v2f_img i) : SV_Target { return half4(Box(i.uv, 1), 1); }

    half4 FragUp(v2f_img i) : SV_Target { return half4(Box(i.uv, 0.5), 1); }

    half4 FragFinal(v2f_img i) : SV_Target
    {
        half4 src = tex2D(_MainTex, i.uv);
        half3 c = ToLinear(src.rgb) + tex2D(_BloomTex, i.uv).rgb * _Intensity;
        c = Aces(c * _Exposure);

        half luma = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = max(0, lerp(luma.xxx, c, _Saturation));

        // Contrast around mid-gray in a perceptual-ish (square-root) space.
        half3 p = sqrt(c);
        p = saturate((p - 0.5) * _Contrast + 0.5);
        c = p * p;

        float2 d = i.uv - 0.5;
        c *= 1 - _Vignette * smoothstep(0.25, 0.9, dot(d, d) * 2.2);

        return half4(ToOutput(c), src.a);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragPrefilter
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragDown
            ENDCG
        }

        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragUp
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragFinal
            ENDCG
        }
    }
    Fallback Off
}
