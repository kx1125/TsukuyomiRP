# PH Pass compatibility regression tests

Copy `Editor/` into an Editor test folder in a Unity 6000.5 / URP 17.5 project
that references this package. Run `Tsukuyomi.Rendering.Tests.PHPassCompatibilityTests`
in EditMode. Copy the adjacent `GlobalBufferLifecycle/Editor` tests as well to
exercise fallback buffers with D3D12 graphics enabled.

The migration test imports a temporary legacy YAML Profile into `Assets/`, verifies
the resulting node settings and IDs, saves/reloads it, then removes the test asset.
The scheduling tests deliberately vary authoring order and omit either light feature.
