import argparse
from pathlib import Path

from core import assets


ICO_SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)


def generate_windows_app_icon(output_path):
    """Genera un .ico multiresolucion para el ejecutable de Windows."""
    target_path = Path(output_path)
    target_path.parent.mkdir(parents=True, exist_ok=True)

    master_icon = assets.create_app_icon_image(assets.APP_ICON_MASTER_SIZE)
    master_icon.save(target_path, format="ICO", sizes=[(size, size) for size in ICO_SIZES])
    return target_path


def main():
    parser = argparse.ArgumentParser(description="Genera el icono .ico multiresolucion de la aplicacion")
    parser.add_argument(
        "--output",
        default="build/app-icon.ico",
        help="Ruta de salida del archivo .ico",
    )
    args = parser.parse_args()

    icon_path = generate_windows_app_icon(args.output)
    print(icon_path)


if __name__ == "__main__":
    main()
