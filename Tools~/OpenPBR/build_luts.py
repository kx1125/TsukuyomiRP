"""Reproduce the shipped LUT payloads. Python 3 + numpy, no Unity required.

Usage: python build_luts.py --reference <adobe/openpbr-bsdf checkout>
The reference is pinned; no downloads or runtime baking take place here.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import numpy as np

PIN = 'c91aad1d1ce1693e803f039d7c92c2965c4eb013'


def radical_inverse(n):
    n = np.asarray(n, dtype=np.uint32)
    n = (n << 16) | (n >> 16)
    n = ((n & 0x55555555) << 1) | ((n & 0xAAAAAAAA) >> 1)
    n = ((n & 0x33333333) << 2) | ((n & 0xCCCCCCCC) >> 2)
    n = ((n & 0x0F0F0F0F) << 4) | ((n & 0xF0F0F0F0) >> 4)
    n = ((n & 0x00FF00FF) << 8) | ((n & 0xFF00FF00) >> 8)
    return n.astype(np.float64) / 4294967296.0


def bake_dfg(size=64, samples=16384):
    # Separable Smith GGX, matching Adobe's microfacet distribution. VNDF
    # sampling cancels D*G1(V)/(4*NdotV); remaining weight is G1(L).
    u = (np.arange(samples) + 0.5) / samples
    phi = 2 * np.pi * radical_inverse(np.arange(samples))
    t1 = np.sqrt(u) * np.cos(phi)
    t2original = np.sqrt(u) * np.sin(phi)
    nv = np.maximum(np.linspace(0, 1, size), 1e-5)[:, None]
    vx = np.sqrt(1 - nv * nv)
    result = np.zeros((size, size, 4))
    for row, r in enumerate(np.linspace(0, 1, size)):
        alpha = max(r * r, 0.0016)
        length = np.sqrt((alpha * vx)**2 + nv**2)
        vhx, vhz = alpha * vx / length, nv / length
        s = 0.5 * (1 + vhz)
        t2 = (1 - s) * np.sqrt(1 - t1*t1) + s * t2original
        diskz = np.sqrt(np.maximum(0, 1 - t1*t1 - t2*t2))
        hx = alpha * (-vhz*t2 + vhx*diskz)
        hy = np.broadcast_to(alpha*t1, hx.shape)
        hz = np.maximum(0, vhx*t2 + vhz*diskz)
        norm = np.sqrt(hx*hx + hy*hy + hz*hz)
        hx, hy, hz = hx/norm, hy/norm, hz/norm
        vh = np.maximum(0, vx*hx + nv*hz)
        nl = 2*vh*hz - nv
        g1 = np.where(nl > 0, 2*np.maximum(nl, 0) /
                      np.maximum(nl + np.sqrt(alpha*alpha + (1-alpha*alpha)*nl*nl), 1e-8), 0)
        fc = (1-vh)**5
        result[row, :, 0] = np.mean((1-fc)*g1, axis=1)
        result[row, :, 1] = np.mean(fc*g1, axis=1)
    result[:, :, 3] = result[:, :, 0] + result[:, :, 1]
    avg = np.trapezoid(result[:, :, 3] * (2*np.linspace(0, 1, size)), dx=1/(size-1), axis=1)
    result[:, :, 2] = avg[:, None]
    return result


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--reference', type=Path, required=True)
    p.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[2] / 'Runtime/Data/OpenPBR')
    args = p.parse_args()
    actual = subprocess.check_output(['git', '-C', str(args.reference), 'rev-parse', 'HEAD'], text=True).strip()
    if actual != PIN:
        raise RuntimeError(f'Reference must be {PIN}, got {actual}')
    src = args.reference / 'impl/data'
    text = (src / 'openpbr_opaque_dielectric_energy_complement_data.h').read_text()
    text = re.sub(r'//[^\n]*|/\*.*?\*/', '', text, flags=re.S)
    dielectric = np.array([int(x) for x in re.findall(r'\d+', text)], dtype=float) / 65535
    assert dielectric.size == 32**3
    text = (src / 'openpbr_ltc_data.h').read_text()
    ltc = np.array([[float(x) for x in row.split(',')]
                    for row in re.findall(r'vec3\(([^)]+)\)', text)])
    assert ltc.shape == (1024, 3)
    fuzz = np.column_stack((ltc, np.zeros(1024)))
    tables = {'DFG': bake_dfg(), 'Dielectric': dielectric, 'Fuzz': fuzz}
    args.output.mkdir(parents=True, exist_ok=True)
    manifest = {'referenceCommit': PIN, 'format': 'little-endian float16',
                'dfgSamples': 16384, 'dfgVisibility': 'separable Smith GGX', 'tables': {}}
    for name, data in tables.items():
        payload = np.asarray(data, dtype='<f2').tobytes()
        (args.output / (name + '.bytes')).write_bytes(payload)
        manifest['tables'][name] = {'bytes': len(payload), 'sha256': hashlib.sha256(payload).hexdigest()}
    (args.output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    main()
