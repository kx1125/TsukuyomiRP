#ifndef TSUKUYOMI_CHARACTER_FACE_META_INCLUDED
#define TSUKUYOMI_CHARACTER_FACE_META_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/CharacterFaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

half4 TsukuyomiFaceMetaFragment(Varyings input) : SV_Target
{
    TsukuyomiFaceSurface surface = TsukuyomiFaceSampleSurface(input.uv);
    MetaInput meta = (MetaInput)0;
    meta.Albedo = surface.albedo * (0.96 * (1.0 - _Metallic));
    meta.Emission = surface.emission;
    // View-dependent SkinRim/HighlightMap are not baked into lightmaps.
    return UniversalFragmentMeta(input, meta);
}

#endif
