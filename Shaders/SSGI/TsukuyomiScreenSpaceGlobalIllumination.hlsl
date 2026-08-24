#ifndef TSUKUYOMI_SCREEN_SPACE_GLOBAL_ILLUMINATION_INCLUDED
#define TSUKUYOMI_SCREEN_SPACE_GLOBAL_ILLUMINATION_INCLUDED

TEXTURE2D_X(_TsukuyomiScreenSpaceGlobalIlluminationTexture);

half3 SampleTsukuyomiScreenSpaceGlobalIllumination(float2 normalizedScreenSpaceUV)
{
    return SAMPLE_TEXTURE2D_X_LOD(
        _TsukuyomiScreenSpaceGlobalIlluminationTexture,
        sampler_LinearClamp,
        normalizedScreenSpaceUV,
        0.0).rgb;
}

#endif
