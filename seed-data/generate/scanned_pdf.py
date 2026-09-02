"""Renders plain text onto page images and saves them as an image-only PDF (no text layer) —
simulating a scanned document so the ingestion pipeline's OCR path (T6) has something real to do.
Adds a slight rotation and grain per page so it doesn't look like a suspiciously perfect scan.
"""

import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

PAGE_SIZE = (1700, 2200)  # ~8.5x11in at 200dpi
MARGIN = 140


def _font(size: int) -> ImageFont.FreeTypeFont:
    for candidate in [
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
    ]:
        if Path(candidate).exists():
            return ImageFont.truetype(candidate, size)
    return ImageFont.load_default()


def _wrap(draw: ImageDraw.ImageDraw, text: str, font: ImageFont.FreeTypeFont, max_width: int) -> list[str]:
    words = text.split()
    lines: list[str] = []
    current = ""
    for word in words:
        trial = f"{current} {word}".strip()
        if draw.textlength(trial, font=font) <= max_width:
            current = trial
        else:
            if current:
                lines.append(current)
            current = word
    if current:
        lines.append(current)
    return lines


def _render_page(lines: list[str], seed: int) -> Image.Image:
    img = Image.new("L", PAGE_SIZE, color=255)
    draw = ImageDraw.Draw(img)
    body_font = _font(30)

    y = MARGIN
    for line in lines:
        draw.text((MARGIN, y), line, font=body_font, fill=10)
        y += 42

    # Light scan artifacts: grain + a small rotation, so this reads as "scanned", not "rendered".
    rng = random.Random(seed)
    noise = Image.effect_noise(PAGE_SIZE, 14).point(lambda p: 255 - p // 6)
    img = Image.blend(img.convert("L"), noise, alpha=0.05)
    angle = rng.uniform(-0.6, 0.6)
    img = img.rotate(angle, fillcolor=255, resample=Image.BICUBIC)
    img = img.filter(ImageFilter.GaussianBlur(radius=0.4))
    return img.convert("RGB")


def build_scanned_pdf(pages_text: list[list[str]], out_path: Path, seed: int = 0) -> None:
    """pages_text: one list of pre-wrapped lines per page."""
    images = [_render_page(lines, seed=seed + i) for i, lines in enumerate(pages_text)]
    out_path.parent.mkdir(parents=True, exist_ok=True)
    images[0].save(str(out_path), save_all=True, append_images=images[1:])


def paginate(draw: ImageDraw.ImageDraw, full_text: list[str], font_size: int = 30, lines_per_page: int = 44) -> list[list[str]]:
    font = _font(font_size)
    max_width = PAGE_SIZE[0] - 2 * MARGIN
    wrapped: list[str] = []
    for paragraph in full_text:
        if paragraph == "":
            wrapped.append("")
            continue
        wrapped.extend(_wrap(draw, paragraph, font, max_width))
        wrapped.append("")

    pages = []
    for i in range(0, len(wrapped), lines_per_page):
        pages.append(wrapped[i:i + lines_per_page])
    return pages or [[]]
