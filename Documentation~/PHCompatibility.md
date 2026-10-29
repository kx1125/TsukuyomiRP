# PH compatibility with v0.3.0

The PH integration merges main through `babdcba297177f8559fd0874be1ebffdf05d49a7`.
The latest two changes are `d4715bb` (unused rendering code removal) and `babdcba`
(Profile-driven Pass assembly). Their prerequisite `be745b7` SceneScan framework
and `d01d44e` SSS update are also included; the merge retains PH history.

## Conflict resolutions

- `TsukuyomiFeature` uses the upstream `PassSession` lifecycle and graph editor.
  PH effects are `MsvoPass`, `RectLightPass` and `RayTracedShadowPass` nodes.
- MSVO prepares before GTAO regardless of node order. Active MSVO takes priority;
  its color composite runs before transparents and its lighting output runs after
  prepasses. The keyword restoration pass preserves the camera color attachment.
- Ray traced shadows request depth/motion before the GBuffer event range, preserving
  Forward depth-priming compatibility. Rectangle uploads follow the optional shadow
  producer. Either feature can be used independently. Camera histories and LTC
  resources are released through the session lifecycle.
- Node `Enabled` is the master gate. Volume overrides can disable an enabled node,
  but cannot enable an absent or disabled node.
- Studio skin/eye/hair shader adapters, PH SSS helpers and their original GUIDs are
  retained. Packed HDR stays the default. Resources include both the PH lights/MSVO
  and upstream SceneScan shader references.
- The apparent `Sample.meta` rename conflict was Git similarity detection. The
  retired sample metadata is removed; MSVO folder and SSS include retain their own
  PH GUIDs.

## Existing Profiles

Profiles without `_schemaVersion` load their old flat serialized fields and migrate
them once to matching Pass nodes. Parameters, enabled states and existing nodes are
preserved. The previous global post-processing gate is folded into Bloom and
Tonemapping node switches. Previously automatic FSR3 mask and neural rendering
helpers become explicit nodes. Save an upgraded Profile to persist schema version 1.

New Profiles remain empty. Existing version-1 Profiles keep their authored nodes;
add the three PH nodes in the Graph Editor when needed. Legacy flat fields remain
hidden solely for deserialization. Runtime configuration code should now edit node
parameters and call `NotifyChanged()`, or use `SetNodeEnabled()` for switches.

## Validation

Validated with Unity 6000.5.3f1 / URP 17.5.0:

- Isolated package Runtime/Editor compilation and existing upscaler compatibility checks.
- Full `RenderPipline` project script compilation.
- Nine D3D12 EditMode cases: legacy YAML migration/save/reload, empty new Profiles,
  duplicate prevention, independent light-node plans, MSVO/GTAO preparation order,
  disabled-node gating, ray-tracing runtime disposal, and both fallback-buffer
  Play/Edit transition cases.

These checks do not establish visual equivalence in a production scene or validate
every ray-tracing/shader variant. Existing backend restrictions (Forward/Forward+,
DX12 ray tracing, non-XR) still apply.
