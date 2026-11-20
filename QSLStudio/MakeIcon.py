from pathlib import Path
from PIL import Image, ImageChops, ImageColor, ImageDraw, ImageFilter

SCALE = 4
SIZE = (256 * SCALE, 256 * SCALE)


def px(values):
    return tuple(value * SCALE for value in values)


def coated_rectangle(bounds, radius, top, bottom, edge):
    mask = Image.new('L', SIZE)
    ImageDraw.Draw(mask).rounded_rectangle(px(bounds), radius * SCALE, fill=255)
    layer = Image.new('RGBA', SIZE)
    d = ImageDraw.Draw(layer)
    x0, y0, x1, y1 = px(bounds)
    top, bottom = ImageColor.getrgb(top), ImageColor.getrgb(bottom)
    for y in range(y0, y1 + 1):
        t = (y - y0) / (y1 - y0)
        color = tuple(round(a + (b - a) * t) for a, b in zip(top, bottom))
        d.line((x0, y, x1, y), fill=color)
    d.rounded_rectangle(px(bounds), radius * SCALE, outline=edge, width=2 * SCALE)
    layer.putalpha(mask)
    return layer


def shine(image, bounds, color, amount, blur):
    alpha = Image.new('L', SIZE)
    ImageDraw.Draw(alpha).ellipse(px(bounds), fill=amount)
    light = Image.new('RGBA', SIZE, color)
    light.putalpha(ImageChops.multiply(alpha.filter(ImageFilter.GaussianBlur(blur * SCALE)), image.getchannel('A')))
    image.alpha_composite(light)


# A glass tile and the front panel of an amateur-radio transceiver.
image = coated_rectangle((8, 8, 247, 247), 58, '#1485f6', '#081b8d', '#80ccff')
shine(image, (-33, -36, 178, 120), '#70f7ff', 166, 23)
shine(image, (99, 125, 284, 264), '#6239fa', 107, 28)
tile = ImageDraw.Draw(image)
tile.arc(px((13, 13, 243, 243)), 187, 300, fill='#8ceaff', width=2 * SCALE)
tile.arc(px((15, 15, 241, 241)), 18, 117, fill='#435ef4', width=2 * SCALE)

shadow = Image.new('RGBA', SIZE)
ImageDraw.Draw(shadow).rounded_rectangle(px((36, 103, 224, 208)), 27 * SCALE, fill='#051446c4')
image.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(8 * SCALE)))

details = ImageDraw.Draw(image)
details.rounded_rectangle(px((62, 50, 76, 115)), 7 * SCALE, fill='#ccefff')
details.rounded_rectangle(px((64, 54, 71, 107)), 3 * SCALE, fill='#fbffff')
for bounds, color in (((77, 51, 175, 144), '#bfe6ff'), ((92, 64, 160, 126), '#f4fdff')):
    details.arc(px(bounds), 229, 311, fill=color, width=6 * SCALE)

radio = coated_rectangle((35, 99, 221, 201), 25, '#ffffff', '#a8d5f7', '#f6fdff')
image.alpha_composite(radio)
screen = coated_rectangle((51, 119, 145, 172), 12, '#163762', '#061a3f', '#4c8ecc')
image.alpha_composite(screen)
details = ImageDraw.Draw(image)
for left, top, color in ((64, 147, '#2eaee8'), (80, 139, '#54d3f6'),
                         (96, 132, '#84effc'), (112, 141, '#56d5fa')):
    details.rounded_rectangle(px((left, top, left + 10, 160)), 2 * SCALE, fill=color)
details.line(px((63, 164, 133, 164)), fill='#234f77', width=2 * SCALE)

dial = coated_rectangle((155, 119, 206, 171), 26, '#3972a6', '#153764', '#e5f7ff')
image.alpha_composite(dial)
knob = coated_rectangle((161, 125, 200, 165), 20, '#f8ffff', '#88bbe9', '#d8f7ff')
image.alpha_composite(knob)
details = ImageDraw.Draw(image)
details.rounded_rectangle(px((179, 128, 182, 141)), 2 * SCALE, fill='#3375ad')
for left in (55, 80, 105):
    details.rounded_rectangle(px((left, 181, left + 17, 188)), 3 * SCALE, fill='#578ab5')
details.ellipse(px((160, 181, 167, 188)), fill='#30aebc')

image = image.resize((256, 256), Image.Resampling.LANCZOS)
sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
image.save(Path(__file__).with_name('QSLStudio.ico'), sizes=[(size, size) for size in sizes], bitmap_format='bmp')
