#ifndef TOON_ENVIRONMENT_COMMON_OUTLINE_PASS_INCLUDED
#define TOON_ENVIRONMENT_COMMON_OUTLINE_PASS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Toon/ToonEnvironmentCommonInput.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

// Per-camera fade end (x), reciprocal range (y), and whether a fade range is active (z).
float4 _TsukuyomiOutlineDistanceFadeParams;

struct ToonOutlineAttributes
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ToonOutlineVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half fogFactor : TEXCOORD1;
    float distanceToFadeEnd : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

ToonOutlineVaryings ToonOutlineVertex(ToonOutlineAttributes input)
{
    ToonOutlineVaryings output = (ToonOutlineVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 normalOS = input.normalOS;
    UNITY_BRANCH
    if (_OutlineNormalSource > 0.5)
    {
        // GenerateSmoothNormal packs a unit tangent-space normal in RGB.
        float3 normalTS = input.color.rgb * 2.0 - 1.0;
        bool hasBakedNormal = abs(dot(normalTS, normalTS) - 1.0) < 0.1 && input.tangentOS.w != 0.0;
        float3 bitangentOS = cross(input.normalOS, input.tangentOS.xyz) * input.tangentOS.w;
        normalOS = hasBakedNormal ? mul(normalTS, float3x3(input.tangentOS.xyz, bitangentOS, input.normalOS)) : 0.0;
    }

    float3 normalWS = TransformObjectToWorldNormal(normalOS);
    float4 normalCS = mul(GetWorldToHClipMatrix(), float4(normalWS, 0.0));
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    float4 positionCS = TransformWorldToHClip(positionWS);
    float cameraDistance = distance(positionWS, GetCameraPositionWS());
    float distanceToEnd = _TsukuyomiOutlineDistanceFadeParams.z > 0.0
        ? _TsukuyomiOutlineDistanceFadeParams.x - cameraDistance : 1.0;
    float inverseRange = _TsukuyomiOutlineDistanceFadeParams.y;
    float widthScale = inverseRange > 0.0 ? saturate(distanceToEnd * inverseRange) : (distanceToEnd > 0.0);
    float width = _OutlineWidth * widthScale;

    // The positive w^2 denominator of d(clip.xy / clip.w) cancels on normalization.
    float2 screenSize = GetScaledScreenParams().xy;
    float2 aspectScale = float2(screenSize.x / screenSize.y, 1.0);
    float2 direction = (normalCS.xy * positionCS.w - positionCS.xy * normalCS.w) * aspectScale;
    direction *= rsqrt(max(dot(direction, direction), 1e-12));

    // Two NDC units span the image height. Width is in pixels at 1080p.
    float2 offsetNDC = direction * (width * (2.0 / 1080.0)) / aspectScale;
    positionCS.xy += offsetNDC * positionCS.w;

    output.positionCS = positionCS;
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.fogFactor = ComputeFogFactor(positionCS.z);
    output.distanceToFadeEnd = distanceToEnd;
    return output;
}

half4 ToonOutlineFragment(ToonOutlineVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    if (input.distanceToFadeEnd <= 0.0)
        discard;
#if defined(_ALPHATEST_ON)
    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
#endif
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    return half4(MixFog(_OutlineColor.rgb, input.fogFactor), 1.0h);
}

#endif
