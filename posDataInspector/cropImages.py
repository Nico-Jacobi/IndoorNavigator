from PIL import Image
import os
from PIL import ImageChops


def is_almost_white(pixel, tol=10):
    """returns if given pixel color is white (white means it can be cropped in this context)"""
    return all(channel >= 255 - tol for channel in pixel[:3])  # nur RGB checken

def trim_border(im: Image.Image, tol=10) -> Image.Image:
    """will trim as much whitespaces a possible form around the given image"""
    pixels = im.load()
    width, height = im.size

    left = 0
    right = width - 1
    top = 0
    bottom = height - 1

    # von links trimmen
    while left < right:
        if any(not is_almost_white(pixels[left, y], tol) for y in range(height)):
            break
        left += 1

    # von rechts trimmen
    while right > left:
        if any(not is_almost_white(pixels[right, y], tol) for y in range(height)):
            break
        right -= 1

    # von oben trimmen
    while top < bottom:
        if any(not is_almost_white(pixels[x, top], tol) for x in range(width)):
            break
        top += 1

    # von unten trimmen
    while bottom > top:
        if any(not is_almost_white(pixels[x, bottom], tol) for x in range(width)):
            break
        bottom -= 1

    # ausschneiden
    return im.crop((left, top, right + 1, bottom + 1))


def batch_trim(input_folder: str, output_folder: str, tol=10):
    """will trim all files in given input folder and save them in output folder"""
    if not os.path.exists(output_folder):
        os.makedirs(output_folder)

    for fname in os.listdir(input_folder):
        if fname.lower().endswith(".png"):
            path_in = os.path.join(input_folder, fname)
            path_out = os.path.join(output_folder, fname)

            img = Image.open(path_in).convert("RGBA")
            trimmed = trim_border(img, tol=tol)
            trimmed.save(path_out)
            print(f"Trimmed {fname}")



if __name__ == "__main__":
    output_dir = "resources/Graphics/interesting/cropped"
    input_dir = "resources/Graphics/interesting"

    batch_trim(input_dir, output_dir)

# This script will crop images in the specified input directory and save them to the output directory.