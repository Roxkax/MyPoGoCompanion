"""Trims the transparent border of every sprite so all Pokémon fill their box equally (owner's choice, 2026-10-27)."""
import pathlib
from PIL import Image

for p in sorted(pathlib.Path(__file__).with_name("sprites").glob("*.png")):
    im = Image.open(p).convert("RGBA")
    box = im.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()
    if not box:
        continue
    im = im.crop(box)
    side = max(im.size)  # square canvas, content centred
    sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    sq.paste(im, ((side - im.width) // 2, (side - im.height) // 2))
    sq.save(p)
