# TsukuyomiRP LookDev Sample

This subway look-development scene demonstrates TsukuyomiRP lighting, shadows, ambient occlusion, bloom, tonemapping, volumetric lighting, and planar reflection support.

## Setup

1. Import the **LookDev** sample from the TsukuyomiRP package details in Package Manager.
2. In the setup prompt, select **Apply and Open**.

The setup assigns `Settings/SampleScene_Asset.asset` to Graphics Settings and the active Quality level, validates the APV Baking Set against the imported Scene GUID, and then opens `LookDev.unity`.

If the prompt was dismissed, run **Tools > Tsukuyomi RP > Samples > Setup LookDev Sample**.

`SampleScene_Asset.asset` references the included `SampleScene_Renderer.asset`, which contains the Tsukuyomi renderer feature and its pipeline profile. Restore your project's previous render pipeline asset after evaluating the sample if needed.
