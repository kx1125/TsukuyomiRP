"""Fetch only the pinned public LUT sources and notices needed by build_luts.py."""
import argparse
from pathlib import Path
import urllib.request
from build_luts import UNITY, SELFSHADOW

parser = argparse.ArgumentParser()
parser.add_argument("destination", type=Path)
args = parser.parse_args()
files = {
    "hdrp/LtcData.BRDF_Disney.cs": f"https://raw.githubusercontent.com/Unity-Technologies/Graphics/{UNITY}/Packages/com.unity.render-pipelines.high-definition/Runtime/Material/LTCAreaLight/Generated/LtcData.BRDF_Disney.cs",
    "hdrp/LICENSE.md": f"https://raw.githubusercontent.com/Unity-Technologies/Graphics/{UNITY}/LICENSE.md",
    "selfshadow/ltc_1.dds": f"https://raw.githubusercontent.com/selfshadow/ltc_code/{SELFSHADOW}/fit/results/ltc_1.dds",
    "selfshadow/ltc_2.dds": f"https://raw.githubusercontent.com/selfshadow/ltc_code/{SELFSHADOW}/fit/results/ltc_2.dds",
    "selfshadow/LICENSE": f"https://raw.githubusercontent.com/selfshadow/ltc_code/{SELFSHADOW}/LICENSE",
}
for name, url in files.items():
    path = args.destination / name
    path.parent.mkdir(parents=True, exist_ok=True)
    with urllib.request.urlopen(url, timeout=60) as response:
        path.write_bytes(response.read())
    print(name)
