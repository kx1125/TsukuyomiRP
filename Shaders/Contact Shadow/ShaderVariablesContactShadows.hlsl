// Disable warning
// https://discussions.unity.com/t/globally-suppress-pow-f-e-negative-f-warning/807217/16
#pragma warning(disable: 3556)

#define _ContactShadowOpacity 1.0f

RWTexture2D<float> _ContactShadowTextureUAV;

CBUFFER_START(ContactShadowParameters)
float4 _ContactShadowParamsParameters;
float4 _ContactShadowParamsParameters2;
float4 _ContactShadowParamsParameters3;
CBUFFER_END

#define _ContactShadowLength                _ContactShadowParamsParameters.x
#define _ContactShadowDistanceScaleFactor   _ContactShadowParamsParameters.y
#define _ContactShadowFadeEnd               _ContactShadowParamsParameters.z
#define _ContactShadowFadeOneOverRange      _ContactShadowParamsParameters.w
#define _ContactShadowMinDistance           _ContactShadowParamsParameters2.y
#define _ContactShadowFadeInEnd             _ContactShadowParamsParameters2.z
#define _ContactShadowBias                  _ContactShadowParamsParameters2.w
#define _SampleCount                        (int)_ContactShadowParamsParameters3.x
#define _ContactShadowThickness             _ContactShadowParamsParameters3.y
#define _FrameCountMod8                     (int)_ContactShadowParamsParameters3.z
