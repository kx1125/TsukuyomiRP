#ifndef UNIVERSAL_SHADOW_CASTER_PASS_INCLUDED
#define UNIVERSAL_SHADOW_CASTER_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

// Shadow Casting Light geometric parameters. These variables are used when applying the shadow Normal Bias and are set by UnityEngine.Rendering.Universal.ShadowUtils.SetupShadowCasterConstantBuffer in com.unity.render-pipelines.universal/Runtime/ShadowUtils.cs
// For Directional lights, _LightDirection is used when applying shadow Normal Bias.
// For Spot lights and Point lights, _LightPosition is used to compute the actual light direction because it is different at each shadow caster geometry vertex.
float3 _LightDirection;
float3 _LightPosition;

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    #if defined(_ALPHATEST_ON)
        float2 uv       : TEXCOORD0;
    #endif
    float4 positionCS   : SV_POSITION;
    float4 studioScreenPosition : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

float4 GetShadowPositionHClip(Attributes input)
{
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

#if defined(STUDIO_SURFACE)
    float4 positionCS = TransformWorldToHClip(IsStudioHair() ? positionWS + lightDirectionWS * _ShadowBias.x : ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
#else
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
#endif
#if _CASTING_PUNCTUAL_LIGHT_SHADOW && !defined(STUDIO_EYE_SHADOW)
    // Built-in point casters have no vertex bias; their receiver applies it.
    if (_SourceMode > .5 && _ShadowBias.z > 1.5)
        return TransformWorldToHClip(positionWS);
    if (_SourceMode > .5 && _ShadowBias.z < .5)
    {
        // Studio's spot caster uses clip-space bias and zero normal bias.
        // Confirmed in the original GPU frame (event 6068, UnityShadows[5]).
        // Both clip z and w scale with the imported world's unit conversion.
        positionCS = TransformWorldToHClip(positionWS);
        float units = max(_StudioUnitsPerWorldUnit, .0001);
        float bias = _StudioPunctualShadowBias / (units * units);
        // Unity 6 can clamp the light near plane differently from Unity 2018.
        // Convert the source depth offset into the active projection's depth
        // scale, preserving the same world-space occlusion boundary.
        #if UNITY_REVERSED_Z
            float sourceNear = max(_StudioSpotShadowNear / units, .000001);
            // Recover the projection coefficients from the shadow VP. Unity's
            // camera projection global may refer to a different render pass.
            float projectionB=abs(mul(UNITY_MATRIX_VP,float4(_LightPosition,1)).z);
            float3 clipWAxis=UNITY_MATRIX_VP[3].xyz;
            float projectionA=abs(dot(UNITY_MATRIX_VP[2].xyz,clipWAxis))/max(dot(clipWAxis,clipWAxis),.000001);
            bias *= projectionB/sourceNear-projectionA;
        #endif
        #if UNITY_REVERSED_Z
            positionCS.z += clamp(-bias / positionCS.w, -1.0, 0.0);
        #else
            positionCS.z += saturate(bias / positionCS.w);
        #endif
        return positionCS;
    }
#endif
    positionCS = ApplyShadowClamping(positionCS);
    return positionCS;
}

Varyings ShadowPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    #if defined(_ALPHATEST_ON)
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    #endif

    output.positionCS = GetShadowPositionHClip(input);
    // Shader Forge hair shadow vertices have no NORMAL input: depth bias only.
    // Their coverage coordinates are computed before the depth bias/clamping.
#if defined(STUDIO_SURFACE)
    output.studioScreenPosition = ComputeScreenPos(IsStudioHair() ? TransformObjectToHClip(input.positionOS.xyz) : output.positionCS);
#else
    output.studioScreenPosition = ComputeScreenPos(output.positionCS);
#endif
    return output;
}

half4 ShadowPassFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);

    #if defined(_ALPHATEST_ON)
        StudioDepthAlpha(input.uv,input.studioScreenPosition.xy/input.studioScreenPosition.w*_ScreenParams.xy);
    #endif

    #if defined(LOD_FADE_CROSSFADE)
        LODFadeCrossFade(input.positionCS);
    #endif

    return 0;
}

#endif
