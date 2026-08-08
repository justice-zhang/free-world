#!/usr/bin/env python3
"""Build deterministic Qinglan portrait and silhouette runtime sprites."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageFilter


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--portrait-input", required=True, type=Path)
    parser.add_argument("--atlas-input", required=True, type=Path)
    parser.add_argument("--portrait-output", required=True, type=Path)
    parser.add_argument("--silhouette-output", required=True, type=Path)
    return parser.parse_args()


def normalize_portrait(source: Image.Image) -> Image.Image:
    source = source.convert("RGBA")
    bounds = source.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("Portrait source contains no visible pixels.")
    cropped = source.crop(bounds)
    available = 928
    scale = min(1.0, available / cropped.width, available / cropped.height)
    size = (max(1, round(cropped.width * scale)), max(1, round(cropped.height * scale)))
    cropped = cropped.resize(size, Image.Resampling.LANCZOS)
    output = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    x = (1024 - cropped.width) // 2
    y = 1024 - 48 - cropped.height
    output.alpha_composite(cropped, (x, y))
    return output


def create_silhouette(atlas: Image.Image) -> Image.Image:
    atlas = atlas.convert("RGBA")
    if atlas.size != (1536, 1024):
        raise ValueError("Expected the approved 1536x1024 ART-CHAR-001 atlas.")
    frame = atlas.crop((0, 0, 256, 256))
    alpha = frame.getchannel("A")
    outline = alpha.filter(ImageFilter.MaxFilter(9))
    output = Image.new("RGBA", (256, 256), (244, 239, 216, 0))
    output.putalpha(outline)
    core = Image.new("RGBA", (256, 256), (22, 61, 69, 255))
    output.alpha_composite(Image.composite(core, Image.new("RGBA", frame.size), alpha))
    return output


def main() -> None:
    args = parse_args()
    portrait = normalize_portrait(Image.open(args.portrait_input))
    silhouette = create_silhouette(Image.open(args.atlas_input))
    args.portrait_output.parent.mkdir(parents=True, exist_ok=True)
    args.silhouette_output.parent.mkdir(parents=True, exist_ok=True)
    portrait.save(args.portrait_output, optimize=True)
    silhouette.save(args.silhouette_output, optimize=True)
    print(
        f"Wrote portrait={args.portrait_output} {portrait.size} "
        f"silhouette={args.silhouette_output} {silhouette.size}"
    )


if __name__ == "__main__":
    main()
