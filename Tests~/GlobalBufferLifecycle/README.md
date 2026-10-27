# Global buffer lifecycle regression

Copy this directory into a Unity project's `Assets/Tests/GlobalBufferLifecycle`, then run the `Tsukuyomi.Rendering.GlobalBufferTests` assembly in the EditMode Test Runner. The project must reference `tsukuyomi.render-pipelines.universal` and Unity Test Framework. `Tests~` keeps these optional tests out of normal package compilation.

The two cases exercise Rect Light and RT Shadow callbacks across all four Play/Edit states. They assert that the same fallback buffer stays valid, contributions reset to zero, and fallback data remains zero / -1. Cleanup restores the edit-mode defaults even when an assertion fails. Run while the Editor is stopped.

On Unity 6000.6.2f1 / Direct3D12, both cases failed on the original code at `ExitingPlayMode` (disposed SRV) and both passed after the fix. ProjectH also passed two actual Play/Stop cycles with domain and scene reload disabled, asset refresh, script recompilation, and OpenPBR / Studio Hair preview draws. Dawn/Lucy Game and Scene views were checked visually with no missing-SRV warnings.
