#!/usr/bin/env python3
"""Normalize a chroma-removed Qinglan sprite sheet into equal square cells."""

from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

from PIL import Image


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--columns", type=int, default=6)
    parser.add_argument("--rows", type=int, default=4)
    parser.add_argument("--cell-size", type=int, default=256)
    parser.add_argument("--padding", type=int, default=12)
    parser.add_argument(
        "--component-layout",
        action="store_true",
        help="Assign alpha-connected components to their nearest grid center before packing.",
    )
    parser.add_argument(
        "--row-order",
        default="0,2,1,3",
        help="Comma-separated source row indices for each destination row.",
    )
    return parser.parse_args()


def alpha_bounds(image: Image.Image) -> tuple[int, int, int, int]:
    alpha = image.getchannel("A")
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError("A source cell contains no non-transparent pixels.")
    return bounds


def connected_component_frames(
    source: Image.Image,
    columns: int,
    rows: int,
    alpha_threshold: int = 2,
) -> dict[tuple[int, int], Image.Image]:
    width, height = source.size
    alpha = source.getchannel("A").tobytes()
    active = bytearray(1 if value > alpha_threshold else 0 for value in alpha)
    visited = bytearray(width * height)
    groups: dict[tuple[int, int], list[list[int]]] = {
        (row, column): [] for row in range(rows) for column in range(columns)
    }

    for start in range(width * height):
        if not active[start] or visited[start]:
            continue
        queue: deque[int] = deque([start])
        visited[start] = 1
        pixels: list[int] = []
        minimum_x = width
        maximum_x = 0
        minimum_y = height
        maximum_y = 0
        while queue:
            index = queue.popleft()
            pixels.append(index)
            y, x = divmod(index, width)
            minimum_x = min(minimum_x, x)
            maximum_x = max(maximum_x, x)
            minimum_y = min(minimum_y, y)
            maximum_y = max(maximum_y, y)
            for neighbor_y in range(max(0, y - 1), min(height, y + 2)):
                row_offset = neighbor_y * width
                for neighbor_x in range(max(0, x - 1), min(width, x + 2)):
                    neighbor = row_offset + neighbor_x
                    if active[neighbor] and not visited[neighbor]:
                        visited[neighbor] = 1
                        queue.append(neighbor)

        center_x = (minimum_x + maximum_x) * 0.5
        center_y = (minimum_y + maximum_y) * 0.5
        column = min(columns - 1, max(0, int(center_x * columns / width)))
        row = min(rows - 1, max(0, int(center_y * rows / height)))
        groups[(row, column)].append(pixels)

    frames: dict[tuple[int, int], Image.Image] = {}
    for key, components in groups.items():
        if not components:
            raise ValueError(f"No alpha components were assigned to source cell {key}.")
        indices = [index for component in components for index in component]
        xs = [index % width for index in indices]
        ys = [index // width for index in indices]
        left, right = min(xs), max(xs) + 1
        top, bottom = min(ys), max(ys) + 1
        mask = Image.new("L", (right - left, bottom - top), 0)
        mask_pixels = mask.load()
        for index in indices:
            y, x = divmod(index, width)
            mask_pixels[x - left, y - top] = 255
        frame = Image.new("RGBA", mask.size, (0, 0, 0, 0))
        frame.paste(source.crop((left, top, right, bottom)), (0, 0), mask)
        frames[key] = frame
    return frames


def main() -> None:
    args = parse_args()
    if args.columns <= 0 or args.rows <= 0 or args.cell_size <= 0:
        raise ValueError("Grid and cell dimensions must be positive.")
    row_order = [int(value) for value in args.row_order.split(",")]
    if len(row_order) != args.rows or sorted(row_order) != list(range(args.rows)):
        raise ValueError("row-order must be a permutation containing each source row once.")

    source = Image.open(args.input).convert("RGBA")
    source_width, source_height = source.size
    frames: list[Image.Image] = []
    component_frames = (
        connected_component_frames(source, args.columns, args.rows)
        if args.component_layout
        else None
    )
    for destination_row in range(args.rows):
        source_row = row_order[destination_row]
        top = round(source_row * source_height / args.rows)
        bottom = round((source_row + 1) * source_height / args.rows)
        for column in range(args.columns):
            if component_frames is not None:
                frames.append(component_frames[(source_row, column)])
                continue
            left = round(column * source_width / args.columns)
            right = round((column + 1) * source_width / args.columns)
            frame = source.crop((left, top, right, bottom))
            frame_bounds = alpha_bounds(frame)
            frames.append(frame.crop(frame_bounds))

    available = args.cell_size - args.padding * 2
    scale = min(
        1.0,
        available / max(frame.width for frame in frames),
        available / max(frame.height for frame in frames),
    )
    output = Image.new(
        "RGBA",
        (args.columns * args.cell_size, args.rows * args.cell_size),
        (0, 0, 0, 0),
    )
    for index, frame in enumerate(frames):
        width = max(1, round(frame.width * scale))
        height = max(1, round(frame.height * scale))
        normalized = frame.resize((width, height), Image.Resampling.LANCZOS)
        column = index % args.columns
        row = index // args.columns
        x = column * args.cell_size + (args.cell_size - width) // 2
        y = row * args.cell_size + args.cell_size - args.padding - height
        output.alpha_composite(normalized, (x, y))

    args.output.parent.mkdir(parents=True, exist_ok=True)
    output.save(args.output, optimize=True)
    print(
        f"Wrote {args.output} size={output.width}x{output.height} "
        f"cells={args.columns}x{args.rows} cell={args.cell_size} scale={scale:.6f}"
    )


if __name__ == "__main__":
    main()
