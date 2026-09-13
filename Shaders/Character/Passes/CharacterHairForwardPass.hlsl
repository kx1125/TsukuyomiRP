#ifndef CHARACTER_HAIR_FORWARD_PASS_INCLUDED
#define CHARACTER_HAIR_FORWARD_PASS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/CharacterHairInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/SSGI/TsukuyomiScreenSpaceGlobalIllumination.hlsl"

struct CharacterHairAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct CharacterHairVaryings
{
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord : TEXCOORD6;
#endif

    half3 vertexSH : TEXCOORD8;

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD10;
#endif

    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void CharacterHairInitializeInputData(
    CharacterHairVaryings input,
    CharacterHairSurfaceData surfaceData,
    half facing,
    out CharacterHairInputData characterInputData)
{
    characterInputData = (CharacterHairInputData)0;
    characterInputData.inputData.positionWS = input.positionWS;
    characterInputData.inputData.positionCS = input.positionCS;

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half tangentSign = input.tangentWS.w;
    half3 bitangentWS = tangentSign * cross(input.normalWS, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);

    characterInputData.inputData.tangentToWorld = tangentToWorld;
    characterInputData.inputData.normalWS = TransformTangentToWorld(surfaceData.surface.normalTS, tangentToWorld);
    characterInputData.inputData.normalWS = NormalizeNormalPerPixel(characterInputData.inputData.normalWS) * facing;

    characterInputData.specNormalWS = TransformTangentToWorld(surfaceData.specNormalTS, tangentToWorld);
    characterInputData.specNormalWS = NormalizeNormalPerPixel(characterInputData.specNormalWS);
    //characterInputData.specNormalWS = NormalizeNormalPerPixel(characterInputData.inputData.normalWS) * facing;
    characterInputData.inputData.viewDirectionWS = viewDirWS;
    characterInputData.inputData.shadowCoord = TransformWorldToShadowCoord(characterInputData.inputData.positionWS);

    characterInputData.inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
}

void CharacterHairInitializeGIData(CharacterHairVaryings input, inout InputData inputData)
{
#if defined(_TSUKUYOMI_SCREEN_SPACE_GLOBAL_ILLUMINATION)
    inputData.bakedGI = SampleTsukuyomiScreenSpaceGlobalIllumination(inputData.normalizedScreenSpaceUV);
#elif defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SampleScreenSpaceGI(input.positionCS.xy);
#elif defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
    float4 apvProbeOcclusion = 1.0;
#ifdef USE_APV_PROBE_OCCLUSION
    apvProbeOcclusion = input.probeOcclusion;
#endif
    inputData.bakedGI = SAMPLE_GI(
        input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        apvProbeOcclusion,
        apvProbeOcclusion);
#else
    inputData.bakedGI = SampleSHPixel(input.vertexSH, inputData.normalWS);
#endif
}

CharacterHairVaryings CharacterHairForwardVertex(CharacterHairAttributes input)
{
    CharacterHairVaryings output = (CharacterHairVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionWS = vertexInput.positionWS;
    output.normalWS = normalInput.normalWS;
    output.tangentWS = half4(normalInput.tangentWS.xyz, input.tangentOS.w * GetOddNegativeScale());

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    output.vertexSH = SampleProbeSHVertex(
        vertexInput.positionWS,
        output.normalWS.xyz,
        GetWorldSpaceNormalizeViewDir(vertexInput.positionWS),
        output.probeOcclusion);
#else
    output.vertexSH = SampleProbeSHVertex(
        vertexInput.positionWS,
        output.normalWS.xyz,
        GetWorldSpaceNormalizeViewDir(vertexInput.positionWS));
#endif

    output.positionCS = vertexInput.positionCS;
    return output;
}

float CharacterHairLobe(float3 shiftedTangent, float3 halfVector, float exponent)
{
    float tangentDotHalf = dot(shiftedTangent, halfVector);
    float sineTangentHalf = sqrt(saturate(1.0 - tangentDotHalf * tangentDotHalf));
    return pow(max(sineTangentHalf, 1.0e-4), max(exponent, 1.0));
}

half3 CharacterHairLighting(
    CharacterHairSurfaceData hairSurfaceData,
    CharacterHairInputData hairInputData,
    CharacterHairVaryings input,
    Light light,
    bool applySpecularLine)
{
    float3 anisotropyReferenceWS = TransformObjectToWorldDir(float3(_AnisotropyDirX, 1.0, 0.0), true);
    float3 specNormalPerpendicular = cross(hairInputData.specNormalWS, anisotropyReferenceWS);

    float3 normalWS = NormalizeNormalPerPixel(hairInputData.inputData.normalWS);
    float3 viewDirWS = SafeNormalize(hairInputData.inputData.viewDirectionWS);
    float3 tangentWS = SafeNormalize(input.tangentWS.xyz);
    float tangentSign = input.tangentWS.w;

    float3 strandReference = lerp(specNormalPerpendicular, tangentWS, hairSurfaceData.metallicGloss.r);
    float3 strandAxisWS = cross(hairInputData.specNormalWS, strandReference) *
        lerp(1.0, tangentSign, hairSurfaceData.metallicGloss.r);
    strandAxisWS = SafeNormalize(strandAxisWS);

    float3 halfVector = SafeNormalize(light.direction + viewDirWS);
    half attenuation = light.distanceAttenuation * light.shadowAttenuation;
    half normalDotLight = saturate(dot(normalWS, light.direction));
    half shadowFactor = saturate(attenuation * hairSurfaceData.metallicGloss.b);

    float diffuseCoord = saturate(dot(normalWS, light.direction) * 0.5 + 0.5 + _DiffuseOffset);
    half4 diffuseRamp = SAMPLE_TEXTURE2D_LOD(
        _DiffuseRampMap,
        sampler_DiffuseRampMap,
        float2(diffuseCoord, 0.5),
        0.0);

    half3 shadowAlbedo0 = hairSurfaceData.surface.albedo * _ShadowColorBrightness;
    half shadowLuminance = dot(shadowAlbedo0, half3(0.2126729, 0.7151522, 0.0721750));
    half3 shadowAlbedo = lerp(shadowLuminance.xxx, shadowAlbedo0, _ShadowColorSaturation);
    half3 litAlbedo = hairSurfaceData.surface.albedo * diffuseRamp.rgb;
    half3 diffuse = lerp(shadowAlbedo, litAlbedo, saturate(shadowFactor * diffuseRamp.a));
    diffuse *= light.color * normalDotLight;

    float3 specNormalOS = TransformWorldToObjectDir(hairInputData.specNormalWS, true);
    float3 viewDirOS = TransformWorldToObjectDir(hairInputData.inputData.viewDirectionWS, true);
    float anisotropyEdgeFade = pow(
        saturate(dot(normalize(specNormalOS.xz), normalize(viewDirOS.xz))),
        _AnisotropyEdgeFade);

    float3 shiftedT1 = SafeNormalize(
        strandAxisWS + hairInputData.specNormalWS *
            ((2.0 * _AnisotropyValue - 1.0) + hairSurfaceData.strokeOffset));
    float3 shiftedT2 = SafeNormalize(
        strandAxisWS + hairInputData.specNormalWS *
            ((2.0 * _AnisotropyValue2 - 1.0) + hairSurfaceData.strokeOffset));

    float lobe1 = CharacterHairLobe(shiftedT1, halfVector, 200.0);
    float lobe2 = CharacterHairLobe(
        shiftedT2,
        halfVector,
        200.0 * max(1.0 - _AnisotropyRange2, 0.01));

    half4 specRamp = SAMPLE_TEXTURE2D_LOD(
        _SpecRampMap,
        sampler_SpecRampMap,
        float2(
            saturate(lobe1 * hairSurfaceData.metallicGloss.g),
            step(0.0, dot(shiftedT1, halfVector)) *
                anisotropyEdgeFade * anisotropyEdgeFade),
        0.0);

    // specRamp = SAMPLE_TEXTURE2D_LOD(
    // _SpecRampMap,
    // sampler_SpecRampMap,
    // float2(
    //     saturate(lobe1 * hairSurfaceData.metallicGloss.g),1.0),
    // 0.0);

    half3 primarySpecularResponse = lobe1 * specRamp.rgb * anisotropyEdgeFade;
    half primaryMask = max(max(primarySpecularResponse.r, primarySpecularResponse.g), primarySpecularResponse.b);
    half3 secondarySpecular = lobe2 * anisotropyEdgeFade * _AnisotropyColor2.rgb * hairSurfaceData.metallicGloss.a;
    secondarySpecular = lerp(secondarySpecular, half3(0.0, 0.0, 0.0), primaryMask.xxx);
    half3 specular = primarySpecularResponse * _AnisotropyIntensity * 5.0h * _SpecularScale * hairSurfaceData.metallicGloss.g;
    specular += secondarySpecular;
    specular *= shadowFactor * light.color;
    // half3 specular = lobe1 * specRamp.rgb * _AnisotropyIntensity * 5.0h * _SpecularScale;
    // specular += lobe2 * _AnisotropyColor2.rgb * hairSurfaceData.metallicGloss.a;
    // specular *= anisotropyEdgeFade * hairSurfaceData.metallicGloss.g * shadowFactor * light.color;

    if (applySpecularLine)
    {
        float3 lineTangent = SafeNormalize(
            strandAxisWS + hairInputData.specNormalWS * (2.0 * _LineValue - 1.0));
        float lineDotHalf = dot(lineTangent, halfVector);
        float lineSine = max(
            sqrt(saturate(1.0 - lineDotHalf * lineDotHalf)),
            1.0e-4);
        float lineExponent = (float)((int)(200.0 * max(1.0 - _LineRange, 0.0)));
        float lineLobe = saturate(pow(lineSine, lineExponent));

        float proceduralLineMask = ceil(saturate(frac(input.uv.x * _LineAmount) - 0.5));
        float lineMask = lerp(proceduralLineMask, hairSurfaceData.lineMapMask, _UseLineMap);
        float lineFactor = lerp(1.0 - _LineIntensity, 1.0, lineMask);

        // half3 primarySpecularResponse = saturate(
        //     lobe1 * specRamp.rgb * anisotropyEdgeFade * hairSurfaceData.metallicGloss.g);

        float highlightProtection = max(
            max(primarySpecularResponse.r, primarySpecularResponse.g),
            primarySpecularResponse.b);

        lineFactor = lerp(lineFactor, 1.0, highlightProtection);
        lineFactor = lerp(1.0, lineFactor, lineLobe);
        lineFactor = lerp(1.0, lineFactor, hairSurfaceData.metallicGloss.g);

        diffuse *= lineFactor;
        half diffuseLuminance = dot(diffuse, half3(0.2126729h, 0.7151522h, 0.0721750h));
        diffuse = lerp(
            diffuseLuminance.xxx,
            diffuse,
            lerp(_LineSaturation, 1.0h, lineFactor));
    }

    //return specular;
    return diffuse + specular;
}

half4 CharacterHairForwardFragment(CharacterHairVaryings input, half facing : VFACE) : SV_Target0
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    CharacterHairSurfaceData hairData;
    InitializeCharacterHairSurfaceData(input.uv, hairData);

    CharacterHairInputData characterInputData;
    CharacterHairInitializeInputData(input, hairData, facing, characterInputData);
    CharacterHairInitializeGIData(input, characterInputData.inputData);

    InputData inputData = characterInputData.inputData;
    Light mainLight = GetMainLight(inputData.shadowCoord);
    half3 color = CharacterHairLighting(hairData, characterInputData, input, mainLight, true);

#ifdef _ADDITIONAL_LIGHTS
    uint pixelLightCount = GetAdditionalLightsCount();

#if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
        color += CharacterHairLighting(hairData, characterInputData, input, light, false);
    }
#endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
        color += CharacterHairLighting(hairData, characterInputData, input, light, false);
    LIGHT_LOOP_END
#endif

    color += hairData.surface.albedo * characterInputData.inputData.bakedGI * hairData.surface.occlusion;
    color += hairData.surface.emission;

    return half4(color, 1.0h);
}

#endif
