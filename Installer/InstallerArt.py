from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent
with Image.open(root / 'QSLStudio.ico') as source:
    mark = source.convert('RGBA')

hero = Image.new('RGB', (164, 314))
draw = ImageDraw.Draw(hero)
for y in range(314):
    t = y / 313
    draw.line((0, y, 164, y), fill=tuple(round(a + (b-a)*t) for a,b in zip((229, 239, 251), (179, 202, 237))))
welcome_icon = mark.resize((110, 110), Image.Resampling.LANCZOS)
hero.paste(welcome_icon, (27, 102), welcome_icon)
hero.save(root / 'welcome.bmp')

header = Image.new('RGB', (150, 57), 'white')
header_icon = mark.resize((39, 39), Image.Resampling.LANCZOS)
header.paste(header_icon, (101, 8), header_icon)
header.save(root / 'header.bmp')
