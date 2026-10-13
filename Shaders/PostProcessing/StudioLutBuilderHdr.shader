Shader "Hidden/Tsukuyomi/StudioLutBuilderHdr"
{
    HLSLINCLUDE
        #pragma multi_compile_local _ _TONEMAP_ACES _TONEMAP_NEUTRAL
        #pragma multi_compile_local_fragment _ HDR_COLORSPACE_CONVERSION

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ACES.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#if defined(HDR_COLORSPACE_CONVERSION)
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/HDROutput.hlsl"
#endif

        float4 _Lut_Params;         // x: lut_height, y: 0.5 / lut_width, z: 0.5 / lut_height, w: lut_height / lut_height - 1
        float4 _ColorBalance;       // xyz: LMS coeffs, w: unused
        float4 _ColorFilter;        // xyz: color, w: unused
        float4 _ChannelMixerRed;    // xyz: rgb coeffs, w: unused
        float4 _ChannelMixerGreen;  // xyz: rgb coeffs, w: unused
        float4 _ChannelMixerBlue;   // xyz: rgb coeffs, w: unused
        float4 _HueSatCon;          // x: hue shift, y: saturation, z: contrast, w: unused
        float4 _Lift;               // xyz: color, w: unused
        float4 _Gamma;              // xyz: color, w: unused
        float4 _Gain;               // xyz: color, w: unused
        float4 _Shadows;            // xyz: color, w: unused
        float4 _Midtones;           // xyz: color, w: unused
        float4 _Highlights;         // xyz: color, w: unused
        float4 _ShaHiLimits;        // xy: shadows min/max, zw: highlight min/max
        float4 _SplitShadows;       // xyz: color, w: balance
        float4 _SplitHighlights;    // xyz: color, w: unused
        float4 _HDROutputLuminanceParams; // xy: brightness min/max, z: paper white brightness, w: 1.0 / brightness max
        float4 _HDROutputGradingParams; // x: eetf/range reduction mode, y: hue shift, zw: unused

        TEXTURE2D(_CurveMaster);
        TEXTURE2D(_CurveRed);
        TEXTURE2D(_CurveGreen);
        TEXTURE2D(_CurveBlue);

        TEXTURE2D(_CurveHueVsHue);
        TEXTURE2D(_CurveHueVsSat);
        TEXTURE2D(_CurveSatVsSat);
        TEXTURE2D(_CurveLumVsSat);

        #define MinNits                 _HDROutputLuminanceParams.x
        #define MaxNits                 _HDROutputLuminanceParams.y
        #define PaperWhite              _HDROutputLuminanceParams.z
        #define RangeReductionMode      (int)_HDROutputGradingParams.x
        #define HueShift                _HDROutputGradingParams.y

        float EvaluateCurve(TEXTURE2D(curve), float t)
        {
            float x = SAMPLE_TEXTURE2D(curve, sampler_LinearClamp, float2(t, 0.0)).x;
            return saturate(x);
        }

        float3 RotateToColorGradeOutputSpace(float3 gradedColor)
        {
            #ifdef _TONEMAP_ACES
                // In ACES workflow we return graded color in ACEScg, we move to ACES (AP0) later on
                return gradedColor;
            #elif defined(HDR_COLORSPACE_CONVERSION) // HDR but not ACES workflow
                // If we are doing HDR we expect grading to finish at Rec2020. Any supplemental rotation is done inside the various options.
                return RotateRec709ToRec2020(gradedColor);
            #else // Nor ACES or HDR
                // We already graded in sRGB
                return gradedColor;
            #endif
        }

        // Note: when the ACES tonemapper is selected the grading steps will be done using ACES spaces
        float3 ColorGrade(float3 colorLutSpace)
        {
            float3 colorLinear = LogCToLinear(colorLutSpace);
            #if _TONEMAP_ACES
            float3 colorLog = ACES_to_ACEScc(unity_to_ACES(colorLinear));
            #else
            float3 colorLog = colorLutSpace;
            #endif
            colorLog = (colorLog - ACEScc_MIDGRAY) * _HueSatCon.z + ACEScc_MIDGRAY;
            #if _TONEMAP_ACES
            colorLinear = ACES_to_ACEScg(ACEScc_to_ACES(colorLog));
            #else
            colorLinear = LogCToLinear(colorLog);
            #endif
            colorLinear = LMSToLinear(LinearToLMS(colorLinear) * _ColorBalance.xyz);
            colorLinear *= _ColorFilter.xyz;
            colorLinear = float3(dot(colorLinear, _ChannelMixerRed.xyz),
                dot(colorLinear, _ChannelMixerGreen.xyz), dot(colorLinear, _ChannelMixerBlue.xyz));
            colorLinear = colorLinear * _Gain.xyz + _Lift.xyz;
            colorLinear = sign(colorLinear) * pow(abs(colorLinear), _Gamma.xyz);
            colorLinear = max(0.0, colorLinear);
            float3 hsv = RgbToHsv(colorLinear);
            float satMult = EvaluateCurve(_CurveHueVsSat, hsv.x) * 2.0;
            satMult *= EvaluateCurve(_CurveSatVsSat, hsv.y) * 2.0;
            satMult *= EvaluateCurve(_CurveLumVsSat, Luminance(colorLinear)) * 2.0;
            float hue = hsv.x + _HueSatCon.x;
            hue += EvaluateCurve(_CurveHueVsHue, hue) - 0.5;
            hsv.x = RotateHue(hue, 0.0, 1.0);
            colorLinear = HsvToRgb(hsv);
            // PPSv2 uses Rec709 luminance here even while grading in ACEScg.
            float luma = Luminance(colorLinear);
            colorLinear = luma.xxx + _HueSatCon.y * satMult * (colorLinear - luma.xxx);
            // The original HDR path does not apply the LDR YRGB curves.
            return colorLinear;
        }

float3 StudioAcesTonemap(float3 aces)
{
#if TONEMAPPING_USE_FULL_ACES

    float3 oces = RRT(half3(aces));
    float3 odt = ODT_RGBmonitor_100nits_dim(oces);
    return odt;

#else

    // --- Glow module --- //
    half saturation = rgb_2_saturation(half3(aces));
    half ycIn = rgb_2_yc(half3(aces));
    half s = sigmoid_shaper((saturation - 0.4) / 0.2);
    float addedGlow = 1.0 + glow_fwd(ycIn, RRT_GLOW_GAIN * s, RRT_GLOW_MID);
    aces *= addedGlow;

    // --- Red modifier --- //
    half hue = rgb_2_hue(half3(aces));
    float centeredHue = center_hue(hue, RRT_RED_HUE); // UUM-125596 Must be float for subsequent calculations
    float hueWeight;
    {
        //hueWeight = cubic_basis_shaper(centeredHue, RRT_RED_WIDTH);
        hueWeight = smoothstep(0.0, 1.0, 1.0 - abs(2.0 * centeredHue / RRT_RED_WIDTH));
        hueWeight *= hueWeight;
    }

    aces.r += hueWeight * saturation * (RRT_RED_PIVOT - aces.r) * (1.0 - RRT_RED_SCALE);

    // --- ACES to RGB rendering space --- //
    float3 acescg = max(0.0, ACES_to_ACEScg(aces));

    // --- Global desaturation --- //
    //acescg = mul(RRT_SAT_MAT, acescg);
    acescg = lerp(dot(acescg, AP1_RGB2Y).xxx, acescg, RRT_SAT_FACTOR.xxx);

    // Original Unity PPSv2 RRT/ODT fit (Unity-Technologies/PostProcessing).
    const float a = 278.5085;
    const float b = 10.7772;
    const float c = 293.6045;
    const float d = 88.7122;
    const float e = 80.6889;
    float3 rgbPost = (acescg * (a * acescg + b)) / (acescg * (c * acescg + d) + e);

    // Scale luminance to linear code value
    // float3 linearCV = Y_2_linCV(rgbPost, CINEMA_WHITE, CINEMA_BLACK);

    // Apply gamma adjustment to compensate for dim surround
    float3 linearCV = darkSurround_to_dimSurround(half3(rgbPost));

    // Apply desaturation to compensate for luminance difference
    //linearCV = mul(ODT_SAT_MAT, color);
    linearCV = lerp(dot(linearCV, AP1_RGB2Y).xxx, linearCV, ODT_SAT_FACTOR.xxx);

    // Convert to display primary encoding
    // Rendering space RGB to XYZ
    float3 XYZ = mul(AP1_2_XYZ_MAT, linearCV);

    // Apply CAT from ACES white point to assumed observer adapted white point
    XYZ = mul(D60_2_D65_CAT, XYZ);

    // CIE XYZ to display primaries
    linearCV = mul(XYZ_2_REC709_MAT, XYZ);

    return linearCV;

#endif
}

        float3 Tonemap(float3 colorLinear)
        {
            #if _TONEMAP_NEUTRAL
            {
                colorLinear = NeutralTonemap(colorLinear);
            }
            #elif _TONEMAP_ACES
            {
                // Note: input is actually ACEScg (AP1 w/ linear encoding)
                float3 aces = ACEScg_to_ACES(colorLinear);
                colorLinear = StudioAcesTonemap(aces);
            }
            #endif

            return colorLinear;
        }

        float3 ProcessColorForHDR(float3 colorLinear)
        {
            #ifdef HDR_COLORSPACE_CONVERSION
                #ifdef _TONEMAP_ACES
                float3 aces = ACEScg_to_ACES(colorLinear);
                return HDRMappingACES(aces.rgb, PaperWhite, MinNits, MaxNits, RangeReductionMode, true);
                #elif _TONEMAP_NEUTRAL
                return HDRMappingFromRec2020(colorLinear.rgb, PaperWhite, MinNits, MaxNits, RangeReductionMode, HueShift, true);
                #else
                // Grading finished in Rec2020, converting to the expected color space and [0, 10k] nits range
                return RotateRec2020ToOutputSpace(colorLinear) * PaperWhite;
                #endif
            #endif

            return colorLinear;
        }

        float4 FragLutBuilderHdr(Varyings input) : SV_Target
        {
            // Lut space
            // We use Alexa LogC (El 1000) to store the LUT as it provides a good enough range
            // (~58.85666) and is good enough to be stored in fp16 without losing precision in the
            // darks
            float3 colorLutSpace = GetLutStripValue(input.texcoord, _Lut_Params);

            // Color grade & tonemap
            float3 gradedColor = ColorGrade(colorLutSpace);

            #ifdef HDR_COLORSPACE_CONVERSION
            gradedColor = ProcessColorForHDR(gradedColor);
            #else
            gradedColor = Tonemap(gradedColor);
            #endif

            return float4(gradedColor, 1.0);
        }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "LutBuilderHdr"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragLutBuilderHdr
            ENDHLSL
        }
    }
}
