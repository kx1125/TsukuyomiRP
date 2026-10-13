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
float _StudioHairShadowCutoff;
float4 _StudioDirectionalShadowDiagnosticBias;
float _StudioCapturedDirectionalShadowReady;
float4 _StudioDirectionalCasterBias[2];
float4x4 _StudioSpotWorldToShadow;
float3 _StudioSpotShadowPosition;
float _StudioSpotShadowProjectionReady;

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

    float4 positionCS = TransformWorldToHClip(IsStudioHair() ? positionWS + lightDirectionWS * _ShadowBias.x : ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    // Built-in point casters have no vertex bias; their receiver applies it.
    if (_SourceMode > .5 && _ShadowBias.z > 1.5)
        return TransformWorldToHClip(positionWS);
    if (_SourceMode > .5 && _ShadowBias.z < .5)
    {
        // Studio's spot caster uses clip-space bias and zero normal bias.
        // Confirmed in the original GPU frame (event 6068, UnityShadows[5]).
        // Both clip z and w scale with the imported world's unit conversion.
        positionCS = TransformWorldToHClip(positionWS);
        if(_StudioHairShadowCutoff>0 && IsStudioHair() && _StudioSpotShadowProjectionReady>.5
            && dot(_LightPosition-_StudioSpotShadowPosition,_LightPosition-_StudioSpotShadowPosition)<1e-10)
        {
            // Preserve the receiver-matrix composition at the .5 alpha boundary.
            // Direct VP interpolation can cross a 1/256-pixel raster boundary
            // after the imported world's scale conversion. The shared shadow
            // transform reproduces the source UV/alpha at audited hair pixels.
            float4 shadow=mul(_StudioSpotWorldToShadow,float4(positionWS,1));
            positionCS=float4(2*shadow.x-shadow.w,shadow.w-2*shadow.y,shadow.z,shadow.w);
            float units=max(_StudioUnitsPerWorldUnit,.0001);
            positionCS.z+=clamp(-_StudioPunctualShadowBias/(units*units*positionCS.w),-1,0);
            return positionCS;
        }
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
#if !_CASTING_PUNCTUAL_LIGHT_SHADOW
    if(_SourceMode>.5 && _StudioDirectionalShadowDiagnosticBias.z>.5)
    {
        // Diagnostic uses source-unit bias recovered from the Studio GPU frame.
        // Defaults to zero; it does not alter ordinary scene rendering.
        float units=max(_StudioUnitsPerWorldUnit,.0001);
        float3 sourcePosition=positionWS;
        if(!IsStudioHair() || _StudioHairShadowCutoff>0)
        {
            float nl=dot(normalWS,lightDirectionWS);
            float sine=sqrt(saturate(1-nl*nl));
            sourcePosition-=normalWS*sine*_StudioDirectionalShadowDiagnosticBias.y/units;
        }
        positionCS=TransformWorldToHClip(sourcePosition);
        float depthScale=abs(dot(UNITY_MATRIX_VP[2].xyz,lightDirectionWS));
        float bias=_StudioDirectionalShadowDiagnosticBias.x/units*depthScale/max(positionCS.w,.000001);
        #if UNITY_REVERSED_Z
            positionCS.z-=bias;
        #else
            positionCS.z+=bias;
        #endif
    }
#endif
#if !defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    if(_StudioCapturedDirectionalShadowReady>.5)
    {
        uint cascade=abs(UNITY_MATRIX_VP[0].x-_StudioDirectionalCasterBias[0].z)
            <abs(UNITY_MATRIX_VP[0].x-_StudioDirectionalCasterBias[1].z)?0:1;
        float4 capturedBias=_StudioDirectionalCasterBias[cascade];
        float3 sourcePosition=positionWS;
        if(!IsStudioHair() || _StudioHairShadowCutoff>0)
            sourcePosition-=normalWS*sqrt(saturate(1-pow(dot(normalWS,lightDirectionWS),2)))*capturedBias.y;
        positionCS=TransformWorldToHClip(sourcePosition);
        positionCS.z+=clamp(capturedBias.x/max(positionCS.w,.000001),-1,0);
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
    output.studioScreenPosition = ComputeScreenPos(IsStudioHair() ? TransformObjectToHClip(input.positionOS.xyz) : output.positionCS);
    return output;
}

half4 ShadowPassFragment(Varyings input, FRONT_FACE_TYPE studioShadowFront : FRONT_FACE_SEMANTIC) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);

    // The Studio SSS LightingPass replacement has no TreeTransparentCutout
    // caster. AIT Clothes True (mode 6) is absent from the source auxiliary spot
    // and point maps (no 9312/2292-index draws). Keep ordinary shadows intact.
    if(_StudioHairShadowCutoff>0 && IsStudioAITCloth())clip(-1);

    #if defined(_ALPHATEST_ON)
        half alpha=SampleAlbedoAlpha(input.uv,TEXTURE2D_ARGS(_BaseMap,sampler_BaseMap)).a;
        // Studio replaces clothing and hair with LightingPassCutout while
        // rendering SSS shadows. That program clips raw MainTex alpha at .5,
        // without the ordinary material's opacity or ordered coverage pattern.
        if(_StudioHairShadowCutoff>0 && ((_SourceMode>2.5 && _SourceMode<3.5) || _SourceMode>4.5))
        {
            // LightingPassCutout culls back faces even when the ordinary
            // two-sided clothing or hair material has Cull Off.
            clip(IS_FRONT_VFACE(studioShadowFront,1,-1));
            // The source replacement samples float alpha. The surface helper
            // returns half4, which can round values near the .5 cutout boundary.
            float sourceAlpha=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a;
            clip(sourceAlpha-_StudioHairShadowCutoff); return 0;
        }
        if(_SourceMode>2.5 && _SourceMode<3.5)
        {
            // Shader Forge hair_07/08 uses a 2x2 coverage pattern in its shadow pass.
            if(_StudioHairShadowCutoff>0) { clip(alpha-_StudioHairShadowCutoff); return 0; }
            float2 pixel=floor(input.studioScreenPosition.xy/input.studioScreenPosition.w*_ScreenParams.xy);
            float2 parity=fmod(pixel,2);
            float pattern=lerp(lerp(.2,.6,parity.y),lerp(.8,.4,parity.y),parity.x);
            clip(alpha+pattern-1);
        }
        else if(IsStudioAITCloth()) StudioAITCoverage(input.uv,input.studioScreenPosition.xy/input.studioScreenPosition.w*_ScreenParams.xy);
        else if(_SourceMode>4.5) StudioClothCoverage(input.uv,input.studioScreenPosition.xy/input.studioScreenPosition.w*_ScreenParams.xy);
        else if(_StudioLashes>.5) StudioLashAlpha(alpha,input.studioScreenPosition.xy/input.studioScreenPosition.w*_ScreenParams.xy);
        else Alpha(alpha,_BaseColor,_Cutoff);
    #endif

    #if defined(LOD_FADE_CROSSFADE)
        LODFadeCrossFade(input.positionCS);
    #endif

    return 0;
}

#endif
