import os
import subprocess
import shutil
import sys
import zipfile
import argparse
from pathlib import Path

from build_icon import generate_windows_app_icon
from core.version import VERSION, APP_NAME

BUILD_SPEC_FILE = "app.build.spec"
BUILD_ICON_FILE = Path("build") / "app-icon.ico"


def _clean_build_artifacts(app_name):
    folders_to_remove = ["build", "dist"]
    for folder in folders_to_remove:
        if os.path.exists(folder):
            print(f"Limpiando carpeta {folder}...")
            shutil.rmtree(folder, ignore_errors=True)

    # Elimina caches de bytecode del repo para evitar residuos entre builds.
    for cache_dir in Path(".").rglob("__pycache__"):
        shutil.rmtree(cache_dir, ignore_errors=True)

    spec_file = f"{app_name}.spec"
    if os.path.exists(spec_file):
        print(f"Limpiando archivo {spec_file}...")
        os.remove(spec_file)

    # Cache global de PyInstaller (Windows y Unix-like).
    cache_candidates = []
    local_app_data = os.getenv("LOCALAPPDATA")
    if local_app_data:
        cache_candidates.append(Path(local_app_data) / "pyinstaller")
    cache_candidates.append(Path.home() / ".cache" / "pyinstaller")

    for cache_path in cache_candidates:
        if cache_path.exists():
            print(f"Limpiando cache PyInstaller: {cache_path}")
            shutil.rmtree(cache_path, ignore_errors=True)

def run_build(active_version):
    # Motor de construccion centralizado.
    print(f"Iniciando construccion de {active_version}...")

    clean_active = active_version.lstrip('v')
    clean_config = VERSION.lstrip('v')

    if clean_active != clean_config:
        print("\nERROR DE CONSISTENCIA:")
        print(f"Version solicitada: {active_version}")
        print(f"Version en codigo (config): {VERSION}")
        print("ERROR: El tag de Git NO coincide con la version en core/version.py")
        sys.exit(1)

    release_dir = "releases"
    os.makedirs(release_dir, exist_ok=True)
    zip_name = os.path.join(release_dir, f"{APP_NAME}-v{clean_active}-Win64.zip")

    if os.path.exists(zip_name):
        print(f"Error: El archivo {zip_name} ya existe. Sube la version en core/version.py")
        return

    _clean_build_artifacts(APP_NAME)

    build_icon_path = generate_windows_app_icon(BUILD_ICON_FILE)

    if not os.path.exists(BUILD_SPEC_FILE):
        print(f"Error: No se encontro {BUILD_SPEC_FILE}")
        return

    cmd = [
        "pyinstaller",
        "--clean",
        BUILD_SPEC_FILE,
    ]

    print("Ejecutando PyInstaller (esto puede tardar un poco)...")
    try:
        env = os.environ.copy()
        env["_APP_NAME"] = APP_NAME
        env["_APP_ICON_ICO"] = str(build_icon_path.resolve())
        subprocess.run(cmd, check=True, env=env)
        print("Construccion exitosa del ejecutable")
    except subprocess.CalledProcessError as exc:
        print(f"Error durante la construccion: {exc}")
        return

    dist_folder = os.path.join("dist", APP_NAME)
    print(f"Creando paquete comprimido: {zip_name}...")

    with zipfile.ZipFile(zip_name, "w", zipfile.ZIP_DEFLATED) as zipf:
        for root, dirs, files in os.walk(dist_folder):
            for file in files:
                abs_path = os.path.join(root, file)
                rel_path = os.path.relpath(abs_path, os.path.dirname(dist_folder))
                zipf.write(abs_path, rel_path)

    print("\n==========================================")
    print("PROCESO FINALIZADO CON EXITO")
    print(f"Version: {clean_active}")
    print(f"ZIP: {os.path.abspath(zip_name)}")
    zip_size_mb = os.path.getsize(zip_name) / (1024 * 1024)
    print(f"Peso ZIP: {zip_size_mb:.2f} MB")
    print("==========================================\n")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=f"Motor de Construccion {APP_NAME}")
    parser.add_argument("--version", required=True, help="Version oficial a construir (ej: v1.0.0)")
    args = parser.parse_args()

    try:
        import PyInstaller
    except ImportError:
        print("Instalando PyInstaller...")
        subprocess.run([sys.executable, "-m", "pip", "install", "pyinstaller"], check=True)

    run_build(args.version)
