#ifndef TSUKUYOMI_STUDIO_HAIR_INPUT_INCLUDED
#define TSUKUYOMI_STUDIO_HAIR_INPUT_INCLUDED

// The extension is unconditional across passes and lighting keywords, so the
// native material layout remains stable for SRP Batcher and DOTS variants.
#define TSUKUYOMI_OPENPBR_EXTRA_MATERIAL_PROPERTIES \
    float4 _HairSourceMap_ST; \
    float4 _HairBaseColor; \
    float4 _HairRootColor; \
    float4 _HairTipColor; \
    float4 _HairSpecularColor; \
    float _HairRootPower; \
    float _HairTipPower; \
    float _HairSmoothness; \
    float _HairMetallic; \
    float _HairWetness; \
    float _HairRimPower; \
    float _HairRingPower; \
    float _HairRingSuppression;
#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/OpenPBR/OpenPBRInput.hlsl"

TEXTURE2D(_HairSourceMap); SAMPLER(sampler_HairSourceMap);
TEXTURE2D(_HairGradientMap); SAMPLER(sampler_HairGradientMap);
TEXTURE2D(_HairOcclusionMap); SAMPLER(sampler_HairOcclusionMap);

#endif
