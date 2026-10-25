"""Draws NeuralSwitch's icon: a switch, on, in the app's accent colour, on a dark rounded tile.

    python tools/make-icon.py

Writes src/NeuralSwitch/Assets/NeuralSwitch.ico (16 to 256 px) and docs/icon.png. Needs Pillow.
"""

from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
SCALE = 4  # drawn large and scaled down, for smooth edges
SIZE = 256 * SCALE

TILE_TOP = (27, 33, 43)
TILE_BOTTOM = (14, 16, 20)
ACCENT = (43, 217, 165)
KNOB = (6, 40, 29)


def tile() -> Image.Image:
    gradient = Image.new("RGBA", (SIZE, SIZE))
    draw = ImageDraw.Draw(gradient)
    for y in range(SIZE):
        t = y / (SIZE - 1)
        colour = tuple(round(a + (b - a) * t) for a, b in zip(TILE_TOP, TILE_BOTTOM))
        draw.line([(0, y), (SIZE, y)], fill=colour + (255,))

    mask = Image.new("L", (SIZE, SIZE), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, SIZE - 1, SIZE - 1], radius=56 * SCALE, fill=255)
    gradient.putalpha(mask)
    return gradient


def icon() -> Image.Image:
    image = tile()
    draw = ImageDraw.Draw(image)

    # The switch: a pill in the accent colour with its knob to the right, as the app's switch looks when on.
    left, top, right, bottom = 36 * SCALE, 84 * SCALE, 220 * SCALE, 172 * SCALE
    draw.rounded_rectangle([left, top, right, bottom], radius=(bottom - top) // 2, fill=ACCENT + (255,))

    inset = 12 * SCALE
    knob = bottom - top - 2 * inset
    draw.ellipse([right - inset - knob, top + inset, right - inset, bottom - inset], fill=KNOB + (255,))

    return image.resize((256, 256), Image.LANCZOS)


def main() -> None:
    big = icon()
    ico = ROOT / "src" / "NeuralSwitch" / "Assets" / "NeuralSwitch.ico"
    ico.parent.mkdir(parents=True, exist_ok=True)
    big.save(ico, sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)])
    big.save(ROOT / "docs" / "icon.png")
    # In-app images: WPF draws an .ico from one of its small frames, which blurs at 72 px.
    big.save(ico.with_suffix(".png"))
    print(f"wrote {ico} and {ico.with_suffix('.png')}")


if __name__ == "__main__":
    main()
