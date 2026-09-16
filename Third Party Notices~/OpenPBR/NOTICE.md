# OpenPBR third-party provenance

The EON diffuse formulas, LTC fuzz evaluation/coordinate conventions, coat
absorption and darkening formulas, and dielectric LUT indexing in
`ShaderLibrary/OpenPBR` are adapted from Adobe's OpenPBR BSDF implementation:

- https://github.com/adobe/openpbr-bsdf
- Pinned revision: `c91aad1d1ce1693e803f039d7c92c2965c4eb013`
- Copyright 2026 Adobe. Apache License, Version 2.0 (included alongside this notice).

`Runtime/Data/OpenPBR/Dielectric.bytes` contains a float16 conversion of
`impl/data/openpbr_opaque_dielectric_energy_complement_data.h` at that revision.
`Runtime/Data/OpenPBR/Fuzz.bytes` contains the LTC coefficient table from
`impl/data/openpbr_ltc_data.h`, converted to RGBA float16. That table originates
from **Tizian Zeltner, Brent Burley, and Matt Jen-Yuan Chiang**,
*Practical Multiple-Scattering Sheen Using Linearly Transformed Cosines* (2022),
https://github.com/tizian/ltc-sheen, also Apache-2.0. Attribution is retained from
Adobe's `openpbr_ltc_array.h` and `openpbr_fuzz_lobe.h`.

Changes in this integration: Unity HLSL syntax; explicit BRDF-only returns;
cached per-view state; opaque-only fixed-quality wrappers; texture resources;
bounded realtime coat and anisotropic IBL approximations. This is not the full
Adobe BSDF and does not claim complete OpenPBR conformance.

`DFG.bytes` is newly integrated by `Tools~/OpenPBR/build_luts.py` using Hammersley
samples of separable-Smith GGX visible normals. The script records sizes, hashes,
visibility convention and reference revision in the shipped manifest.

Model references:

- OpenPBR Surface 1.1.1: https://academysoftwarefoundation.github.io/OpenPBR/
- EON: https://arxiv.org/abs/2410.18026
- GGX microfacet multiple-scattering compensation follows the directional
  energy-complement construction described by Kulla/Conty (2017) and the
  OpenPBR implementation; the Balanced tier uses multiplicative compensation.
