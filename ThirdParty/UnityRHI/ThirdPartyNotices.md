# Third-party notices

This package redistributes or derives from the following components. Each
keeps its original license. SDK source trees are fetched at build time (CMake
FetchContent / NuGet), while the native runtime binaries are bundled under
`Plugins/x86_64`.

| Component | Location | License |
|---|---|---|
| NVRHI (API/design port) | native D3D12 backend | MIT — `ThirdParty/NVRHI-LICENSE.txt` |
| DirectX-Headers | fetched (`v1.717.0-preview`) | MIT |
| DirectX Shader Compiler | fetched (`v1.9.2602`); runtime binaries in `Plugins/x86_64` | LLVM + Microsoft |
| NVAPI | fetched (NVIDIA/nvapi); linked, not redistributed as a DLL | NVIDIA |
| NRD, NRI, DLSS/NGX runtimes | fetched; runtime binaries in `Plugins/x86_64` | NVIDIA RTX SDKs license |
| D3D12 Agility SDK | fetched; runtime binaries in `Plugins/x86_64/D3D12` | Microsoft Software License Terms |
