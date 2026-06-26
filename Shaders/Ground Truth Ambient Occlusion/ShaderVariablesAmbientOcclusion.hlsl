#ifndef TSUKUYOMI_AMBIENT_OCCLUSION_VARIABLES_INCLUDED
#define TSUKUYOMI_AMBIENT_OCCLUSION_VARIABLES_INCLUDED

half4 _CameraViewTopLeftCorner[2];
half4x4 _CameraViewProjections[2];
float4 _ProjectionParams2;
float4 _CameraViewXExtent[2];
float4 _CameraViewYExtent[2];
float4 _CameraViewZExtent[2];

CBUFFER_START(ShaderVariablesAmbientOcclusion)
    float4 _AOBufferSize;
    float4 _AOParams0;
    float4 _AOParams1;
    float4 _AOParams2;
    float4 _AOParams3;
    float4 _AOParams4;
    float4 _FirstTwoDepthMipOffsets;
    float4 _AODepthToViewParams;
CBUFFER_END

#endif
