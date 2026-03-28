import os
import subprocess
import shutil
import sys
import zipfile
import argparse
from core.version import VERSION, APP_NAME

def run_build(active_version):
    # Motor de construcción centralizado.
    print(f"Iniciando construcción de {active_version}...")
    
    # 1. Validación de Consistencia
    clean_active = active_version.lstrip('v')
    clean_config = VERSION.lstrip('v')
    
    if clean_active != clean_config:
        print(f"\nERROR DE CONSISTENCIA:")
        print(f"Versión solicitada: {active_version}")
        print(f"Versión en código (config): {VERSION}")
        print("ERROR: El tag de Git NO coincide con la versión en core/version.py")
        sys.exit(1)

    # Verificar si el ZIP ya existe
    release_dir = "releases"
    os.makedirs(release_dir, exist_ok=True)
    zip_name = os.path.join(release_dir, f"{APP_NAME}-v{clean_active}-Win64.zip")
    
    if os.path.exists(zip_name):
        print(f"Error: El archivo {zip_name} ya existe. Sube la versión en core/version.py")
        return
    
    # 2. Limpieza previa
    for folder in ['build', 'dist']:
        if os.path.exists(folder):
            print(f"Limpiando carpeta {folder}...")
            shutil.rmtree(folder)
            
    # 3. Localizar customtkinter
    try:
        import customtkinter
        ctk_path = os.path.dirname(customtkinter.__file__)
    except ImportError:
        print("Error: CustomTkinter no está instalado.")
        return

    # 4. Comando PyInstaller
    separator = ";" if os.name == 'nt' else ":"
    cmd = [
        "pyinstaller", "--noconsole", f"--name={APP_NAME}",
        f"--add-data=core{separator}core",
        f"--add-data=module_capture{separator}module_capture",
        f"--add-data=module_editor{separator}module_editor",
        f"--add-data={ctk_path}{separator}customtkinter",
        "--clean", "main.py"
    ]
    
    print("Ejecutando PyInstaller (esto puede tardar un poco)...")
    try:
        subprocess.run(cmd, check=True)
        print("¡Construcción exitosa del ejecutable!")
    except subprocess.CalledProcessError as e:
        print(f"Error durante la construcción: {e}")
        return

    # 5. Crear archivo ZIP
    dist_folder = os.path.join("dist", APP_NAME)
    print(f"Creando paquete comprimido: {zip_name}...")
    
    with zipfile.ZipFile(zip_name, 'w', zipfile.ZIP_DEFLATED) as zipf:
        for root, dirs, files in os.walk(dist_folder):
            for file in files:
                abs_path = os.path.join(root, file)
                rel_path = os.path.relpath(abs_path, os.path.dirname(dist_folder))
                zipf.write(abs_path, rel_path)

    # 6. Autolimpieza de archivos temporales de PyInstaller
    spec_file = f"{APP_NAME}.spec"
    if os.path.exists(spec_file):
        os.remove(spec_file)
        print(f"Limpieza final: Archivo {spec_file} eliminado.")

    print(f"\n==========================================")
    print(f"PROCESO FINALIZADO CON ÉXITO")
    print(f"Versión: {clean_active}")
    print(f"ZIP: {os.path.abspath(zip_name)}")
    print(f"==========================================\n")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=f"Motor de Construcción {APP_NAME}")
    parser.add_argument("--version", required=True, help="Versión oficial a construir (ej: v1.0.0)")
    args = parser.parse_args()
    
    # Asegurar PyInstaller
    try:
        import PyInstaller
    except ImportError:
        print("Instalando PyInstaller...")
        subprocess.run([sys.executable, "-m", "pip", "install", "pyinstaller"], check=True)
        
    run_build(args.version)
