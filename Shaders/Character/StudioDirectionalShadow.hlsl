// Captured Built-in screen-shadow selection and filtering for the reference
// camera. All other cameras retain the normal URP shadow receiver.
float _StudioCapturedDirectionalShadowReady;
float4x4 _StudioDirectionalWorldToShadow[4];
float4 _StudioDirectionalSplitNear,_StudioDirectionalSplitFar;
float _StudioDirectionalShadowStrength;
float _StudioDirectionalScreenShadowReady;
TEXTURE2D_X(_StudioDirectionalScreenShadowTexture);
float StudioDirectionalShadowRaw(float fallback,float3 positionWS)
{
    if(_StudioCapturedDirectionalShadowReady>.5)
    {
        float depth=-TransformWorldToView(positionWS).z;
        float4 selected=step(_StudioDirectionalSplitNear,depth.xxxx)
            *(1-step(_StudioDirectionalSplitFar,depth.xxxx));
        if(selected.x+selected.y<=0)return 1;
        uint cascade=selected.y>.5?1:0;
        float3 coordinate=mul(_StudioDirectionalWorldToShadow[cascade],float4(positionWS,1)).xyz;
        real weights[16];real2 positions[16];
        SampleShadow_ComputeSamples_Tent_7x7(float4(1.0/4096,1.0/4096,4096,4096),coordinate.xy,weights,positions);
        float shadow=0;
        [unroll]for(int i=0;i<16;i++)
            shadow+=weights[i]*SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture,sampler_LinearClampCompare,float3(positions[i],coordinate.z));
        return lerp(1,shadow,_StudioDirectionalShadowStrength);
    }
    return fallback;
}
float StudioDirectionalShadow(float fallback,float3 positionWS)
{
#if !defined(_RECEIVE_SHADOWS_OFF)
    if(_StudioCapturedDirectionalShadowReady>.5)
    {
        if(_StudioDirectionalScreenShadowReady>.5)
        {
            float4 screen=ComputeScreenPos(TransformWorldToHClip(positionWS));
            return SAMPLE_TEXTURE2D_X_LOD(_StudioDirectionalScreenShadowTexture,sampler_LinearClamp,screen.xy/screen.w,0).r;
        }
        return StudioDirectionalShadowRaw(fallback,positionWS);
    }
#endif
    return fallback;
}
