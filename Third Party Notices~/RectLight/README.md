# Rectangle LTC resources

The GGX matrix and amplitude payloads are derived from `selfshadow/ltc_code`, commit
`31e5e96b54f98f33098f8503003119ba2231a1c6`. The full redistribution notice is in
[selfshadow-LICENSE.txt](selfshadow-LICENSE.txt). Include this notice with binary distributions.

**Real-Time Polygonal-Light Shading with Linearly Transformed Cosines.**
Eric Heitz, Jonathan Dupuy, Stephen Hill and David Neubelt.
ACM Transactions on Graphics (Proceedings of ACM SIGGRAPH 2016) 35(4), 2016.
[Project and paper](https://eheitzresearch.wordpress.com/415-2/).

The Disney matrix data and Disney model/FGD conventions are derived from
Unity Graphics/HDRP 17.0.4, commit `feb4de2d9a93a4ae10d287d3a6d7003d08ea3e53`.
They are covered by the [Unity Companion License notice](Unity-Companion-License.md),
for Unity-dependent projects. The FGD is regenerated with deterministic quadrature;
it is not a byte-for-byte copy of HDRP's Monte Carlo render target.

The Low and Medium evaluators implement the equations documented in the feature guide.
No Unreal Engine source files or assets are redistributed with this package.
