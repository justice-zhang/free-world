#!/usr/bin/env python3
"""Generate the four deterministic first-party Riding Wind overlay sprites."""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
from typing import Iterable, Sequence

from PIL import Image, ImageDraw, ImageFilter


Point = tuple[float, float]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--spec", required=True, type=Path)
    parser.add_argument("--source-dir", required=True, type=Path)
    parser.add_argument("--final-dir", required=True, type=Path)
    return parser.parse_args()


def hex_color(value: str, alpha: int = 255) -> tuple[int, int, int, int]:
    value = value.lstrip("#")
    if len(value) != 6:
        raise ValueError(f"Expected six-digit RGB color, received {value!r}.")
    return tuple(int(value[index:index + 2], 16) for index in (0, 2, 4)) + (alpha,)


def ellipse_point(center: Point, radii: Point, angle: float) -> Point:
    radians = math.radians(angle)
    return (
        center[0] + math.cos(radians) * radii[0],
        center[1] + math.sin(radians) * radii[1],
    )


def rotate_points(center: Point, points: Sequence[Point], angle: float) -> list[Point]:
    radians = math.radians(angle)
    cosine = math.cos(radians)
    sine = math.sin(radians)
    result: list[Point] = []
    for x, y in points:
        result.append((
            center[0] + x * cosine - y * sine,
            center[1] + x * sine + y * cosine,
        ))
    return result


class OverlayCanvas:
    def __init__(self, size: int, palette: dict[str, str]) -> None:
        self.size = size
        self.image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        self.glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        self.core = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        self.glow_draw = ImageDraw.Draw(self.glow)
        self.core_draw = ImageDraw.Draw(self.core)
        self.primary = hex_color(palette["primary"])
        self.highlight = hex_color(palette["highlight"])
        self.deep = hex_color(palette["deep"])
        self.accent = hex_color(palette["accent"])
        self.gold = hex_color(palette["gold"])

    def arc(
        self,
        center: Point,
        radii: Point,
        start: float,
        end: float,
        width: int,
        color: tuple[int, int, int, int] | None = None,
    ) -> None:
        color = color or self.primary
        box = (
            center[0] - radii[0], center[1] - radii[1],
            center[0] + radii[0], center[1] + radii[1],
        )
        self.glow_draw.arc(box, start, end, fill=color[:3] + (76,), width=width + 24)
        self.core_draw.arc(box, start, end, fill=self.deep, width=width + 8)
        self.core_draw.arc(box, start, end, fill=color, width=width)

    def polygon(
        self,
        points: Iterable[Point],
        fill: tuple[int, int, int, int],
        outline_scale: float = 1.0,
    ) -> None:
        points = list(points)
        center = (
            sum(point[0] for point in points) / len(points),
            sum(point[1] for point in points) / len(points),
        )
        expanded = [
            (
                center[0] + (point[0] - center[0]) * outline_scale,
                center[1] + (point[1] - center[1]) * outline_scale,
            )
            for point in points
        ]
        self.glow_draw.polygon(expanded, fill=fill[:3] + (82,))
        self.core_draw.polygon(expanded, fill=self.deep)
        self.core_draw.polygon(points, fill=fill)

    def diamond(self, center: Point, radius: float, fill: tuple[int, int, int, int]) -> None:
        points = rotate_points(center, [(0, -radius), (radius * .72, 0), (0, radius), (-radius * .72, 0)], 0)
        self.polygon(points, fill, 1.32)

    def leaf(self, center: Point, angle: float, scale: float = 1.0) -> None:
        points = rotate_points(
            center,
            [(-24 * scale, 0), (-7 * scale, -15 * scale), (24 * scale, 0), (-7 * scale, 15 * scale)],
            angle,
        )
        self.polygon(points, self.highlight, 1.24)
        vein = rotate_points(center, [(-15 * scale, 0), (16 * scale, 0)], angle)
        self.core_draw.line(vein, fill=self.primary, width=max(3, round(5 * scale)))

    def chevron(self, center: Point, angle: float, scale: float = 1.0) -> None:
        points = rotate_points(
            center,
            [(-23 * scale, -15 * scale), (8 * scale, 0), (-23 * scale, 15 * scale)],
            angle,
        )
        self.glow_draw.line(points, fill=self.accent[:3] + (90,), width=max(8, round(24 * scale)), joint="curve")
        self.core_draw.line(points, fill=self.deep, width=max(7, round(17 * scale)), joint="curve")
        self.core_draw.line(points, fill=self.accent, width=max(4, round(9 * scale)), joint="curve")

    def finish(self) -> Image.Image:
        glow = self.glow.filter(ImageFilter.GaussianBlur(14))
        self.image.alpha_composite(glow)
        self.image.alpha_composite(self.core)
        return self.image


def build_static_wind(size: int, palette: dict[str, str], phase: float) -> Image.Image:
    canvas = OverlayCanvas(size, palette)
    center = (size * .5, size * .545)
    for start in (202, 292, 22, 112):
        canvas.arc(center, (222, 178), start + phase, start + 44 + phase, 8, canvas.accent)
    for angle in (45, 135, 225, 315):
        canvas.diamond(ellipse_point(center, (246, 199), angle + phase), 10, canvas.highlight)
    return canvas.finish()


def build_breeze(size: int, palette: dict[str, str], phase: float) -> Image.Image:
    canvas = OverlayCanvas(size, palette)
    center = (size * .5, size * .545)
    canvas.arc(center, (270, 218), 184 + phase, 337 + phase, 11)
    canvas.arc(center, (270, 218), 13 + phase, 76 + phase, 11)
    for angle in (205, 254, 308, 48):
        point = ellipse_point(center, (286, 232), angle + phase)
        canvas.leaf(point, angle + 86 + phase, .9)
    return canvas.finish()


def build_swift_wind(size: int, palette: dict[str, str], phase: float) -> Image.Image:
    canvas = OverlayCanvas(size, palette)
    center = (size * .5, size * .545)
    canvas.arc(center, (304, 246), 178 + phase, 328 + phase, 13)
    canvas.arc(center, (304, 246), 350 + phase, 78 + phase + 360, 13)
    canvas.arc(center, (236, 188), 205 + phase, 352 + phase, 9, canvas.accent)
    canvas.arc(center, (236, 188), 20 + phase, 116 + phase, 9, canvas.accent)
    for angle in (194, 236, 282, 326, 18, 61):
        point = ellipse_point(center, (321, 261), angle + phase)
        canvas.chevron(point, angle + 90 + phase, .88)
    return canvas.finish()


def build_riding_wind(size: int, palette: dict[str, str], phase: float) -> Image.Image:
    canvas = OverlayCanvas(size, palette)
    center = (size * .5, size * .545)
    canvas.arc(center, (338, 272), 168 + phase, 326 + phase, 15)
    canvas.arc(center, (338, 272), 345 + phase, 86 + phase + 360, 15)
    canvas.arc(center, (274, 218), 191 + phase, 347 + phase, 12, canvas.accent)
    canvas.arc(center, (274, 218), 10 + phase, 121 + phase, 12, canvas.accent)
    canvas.arc(center, (210, 164), 218 + phase, 357 + phase, 8, canvas.gold)
    canvas.arc(center, (210, 164), 24 + phase, 132 + phase, 8, canvas.gold)

    for angle in (180, 222, 264, 306, 348, 30, 72, 114):
        point = ellipse_point(center, (358, 290), angle + phase)
        canvas.diamond(point, 12, canvas.highlight if angle % 84 else canvas.gold)

    left_wing = rotate_points((228, 586), [(-72, 8), (-18, -35), (54, -24), (8, 1), (62, 25), (-20, 35)], -8)
    right_wing = rotate_points((796, 586), [(72, 8), (18, -35), (-54, -24), (-8, 1), (-62, 25), (20, 35)], 8)
    canvas.polygon(left_wing, canvas.accent, 1.12)
    canvas.polygon(right_wing, canvas.accent, 1.12)
    canvas.diamond((512, 238), 20, canvas.gold)
    return canvas.finish()


def main() -> None:
    args = parse_args()
    spec = json.loads(args.spec.read_text(encoding="utf-8"))
    size = int(spec["sourceSize"])
    final_size = int(spec["runtimeSize"])
    phase = float(int(spec["seed"]) % 7)
    builders = [build_static_wind, build_breeze, build_swift_wind, build_riding_wind]
    args.source_dir.mkdir(parents=True, exist_ok=True)
    args.final_dir.mkdir(parents=True, exist_ok=True)

    for index, tier in enumerate(spec["tiers"]):
        image = builders[index](size, spec["palette"], phase)
        source_path = args.source_dir / tier["sourceFile"]
        final_path = args.final_dir / tier["finalFile"]
        image.save(source_path, optimize=True)
        runtime = image.resize((final_size, final_size), Image.Resampling.LANCZOS)
        runtime.save(final_path, optimize=True)
        alpha = runtime.getchannel("A")
        visible = sum(alpha.histogram()[16:])
        coverage = visible / (final_size * final_size)
        print(f"tier={index} source={source_path} final={final_path} coverage={coverage:.4f}")


if __name__ == "__main__":
    main()
