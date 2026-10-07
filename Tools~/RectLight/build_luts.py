"""Build fixed-version rect-light LUT payloads (Python 3 + numpy).

Use --reference <directory> containing hdrp/ and selfshadow/ downloads.
No fitting or downloads are performed at runtime. See the feature documentation.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path
import numpy as np

UNITY = "feb4de2d9a93a4ae10d287d3a6d7003d08ea3e53"
SELFSHADOW = "31e5e96b54f98f33098f8503003119ba2231a1c6"

def build(reference, output):
    output.mkdir(parents=True, exist_ok=True)
    payloads = {}
    for i, name in [(1, "GGXMatrix"), (2, "GGXAmplitude")]:
        source = (reference / "selfshadow" / f"ltc_{i}.dds").read_bytes()
        assert source[:4] == b"DDS " and len(source) == 128 + 64 * 64 * 8
        payloads[name] = source[128:]
    source = (reference / "hdrp/LtcData.BRDF_Disney.cs").read_text(encoding="utf-8")
    source = re.sub(r"//[^\n]*", "", source)
    data = source.split("new ushort[64 * 64 * 4]", 1)[1].split("{", 1)[1].split("}", 1)[0]
    matrix = np.array([int(x) for x in re.findall(r"\d+", data)], dtype="<u2")
    assert matrix.size == 64 * 64 * 4
    payloads["DisneyMatrix"] = matrix.tobytes()

    # HDRP DisneyDiffuseNoPI: fd90 = .5 + perceptualRoughness * (1 + L.V).
    # Integrate with cosine-weighted measure. Azimuthal moments are analytic;
    # 64-point Gauss-Legendre quadrature integrates the remaining polynomial.
    nodes, weights = np.polynomial.legendre.leggauss(64)
    mu = (nodes + 1) / 2
    weights = weights * mu
    nov = (np.arange(64) / 63.0) ** 2
    rough = np.arange(64) / 63.0
    v = nov[None, :, None]
    r = rough[:, None, None]
    a = r - .5 + r * v * mu
    a2 = a * a + .5 * r * r * (1 - v * v) * (1 - mu * mu)
    fv, fl = (1 - v) ** 5, (1 - mu) ** 5
    energy = np.sum((1 + a * (fv + fl) + a2 * fv * fl) * weights, axis=-1) / 1.03571
    payloads["DisneyFGD"] = (energy - .5).astype("<f2").tobytes()
    manifest = {"resolution": 64, "unityGraphicsCommit": UNITY, "selfshadowCommit": SELFSHADOW,
                "matrixUV": "(perceptualRoughness, sqrt(1-NoV)), half-texel remap",
                "disneyFGDUV": "(sqrt(NoV), perceptualRoughness), half-texel remap; decode R + 0.5",
                "disneyFGDIntegration": "64-point Gauss-Legendre with analytic azimuthal moments",
                "files": {}}
    for name, data in payloads.items():
        filename = name + ".bytes"
        (output / filename).write_bytes(data)
        manifest["files"][filename] = {"bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(json.dumps(manifest, indent=2))

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--reference", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parents[2] / "Runtime/Features/Rect Light/LUT")
    args = parser.parse_args()
    build(args.reference, args.output)
