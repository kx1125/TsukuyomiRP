# TsukuyomiRP LookDev Sample

This subway look-development scene demonstrates TsukuyomiRP lighting, shadows, ambient occlusion, bloom, tonemapping, volumetric lighting, and planar reflection support.

## Setup

1. Import the **LookDev** sample from the TsukuyomiRP package details in Package Manager.
2. In **Project Settings > Graphics**, assign `Settings/PC_RPAsset.asset` as the Default Render Pipeline Asset.
3. In **Project Settings > Quality**, assign the same asset to the active quality level.
4. Open `LookDev.unity`.

`PC_RPAsset.asset` references the included `PC_Renderer.asset`, which contains the Tsukuyomi renderer feature and its pipeline profile. Restore your project's previous render pipeline asset after evaluating the sample if needed.
