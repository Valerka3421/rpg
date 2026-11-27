# -*- coding: utf-8 -*-
"""Генератор атласа глифов для Unity-рендера сущностей.

Сущности (игрок, монстры, предметы, NPC) в игре рисуются одним символом
(Entity.Ch) с подкраской (Entity.Color). В MonoGame глиф берётся из
шрифтового атласа RuntimeFont. В Unity рисовать шрифт по-пиксельно дорого,
поэтому один раз генерируем PNG-атлас: белые глифы на прозрачном фоне,
клетка 64×64 (совпадает с TileSize). Unity-рендер тинтует спрайт цветом
сущности — как MonoGame.

Выход: Unity/Assets/Resources/GlyphAtlas/atlas.png + glyph_manifest.json
(char -> колонка/строка). Запуск: py -3 tools/gen_glyph_atlas.py
"""
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONT = os.path.join(ROOT, "Content", "fonts", "DejaVuSans.ttf")
OUT = os.path.join(ROOT, "Unity", "Assets", "Resources", "GlyphAtlas")

CELL = 64
COLS = 16
FONT_SIZE = 44

# Печатный ASCII + символы, встречающиеся в глифах сущностей/тайлов.
CHARSET = (
    " !\"#$%&'()*+,-./0123456789:;<=>?"
    "@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_"
    "`abcdefghijklmnopqrstuvwxyz{|}~"
    "●▲▼◆■★☆♠♣♥♦♂♀§¶†‡"
)


def main() -> int:
    if not os.path.exists(FONT):
        print(f"font not found: {FONT}", file=sys.stderr)
        return 1
    os.makedirs(OUT, exist_ok=True)

    chars = sorted(set(CHARSET))
    rows = (len(chars) + COLS - 1) // COLS
    atlas = Image.new("RGBA", (COLS * CELL, rows * CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(atlas)
    font = ImageFont.truetype(FONT, FONT_SIZE)

    manifest = {}
    for i, ch in enumerate(chars):
        col, row = i % COLS, i // COLS
        x0, y0 = col * CELL, row * CELL
        # центр по ширине, низ по базовой линии — как рисует SpriteFont
        bbox = draw.textbbox((0, 0), ch, font=font)
        w, h = bbox[2] - bbox[0], bbox[3] - bbox[1]
        draw.text((x0 + (CELL - w) // 2 - bbox[0], y0 + (CELL - h) // 2 - bbox[1]),
                  ch, font=font, fill=(255, 255, 255, 255))
        manifest[ch] = {"col": col, "row": row}

    atlas.save(os.path.join(OUT, "atlas.png"))
    with open(os.path.join(OUT, "glyph_manifest.json"), "w", encoding="utf-8") as f:
        json.dump({"cell": CELL, "cols": COLS, "rows": rows, "glyphs": manifest},
                  f, ensure_ascii=False, indent=1)
    print(f"ok: {len(chars)} glyphs, {COLS}x{rows} cells -> {OUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
