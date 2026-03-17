import os
import subprocess
import shutil
import sys
import zipfile

def build():
    print("Iniciando proceso de construcción de QA-Screenshot...")
    
    # 1. Limpieza previa
    for folder in ['build', 'dist']:
        if os.path.exists(folder):
            print(f"Limpiando carpeta {folder}...")
            shutil.rmtree(folder)
            
    # 2. Localizar customtkinter para incluir sus assets
    try:
        import customtkinter
        ctk_path = os.path.dirname(customtkinter.__file__)
        print(f"CustomTkinter detectado en: {ctk_path}")
    except ImportError:
        print("Error: CustomTkinter no está instalado. Instálalo con 'pip install customtkinter'")
        return

    # 3. Comando PyInstaller
    # --noconsole: No abre terminal al ejecutar
    # --onedir: Crea una carpeta portable (recomendado)
    # --add-data: Incluye carpetas del proyecto y temas de CTK
    
    separator = ";" if os.name == 'nt' else ":"
    
    cmd = [
        "pyinstaller",
        "--noconsole",
        "--name=QA-Screenshot",
        f"--add-data=core{separator}core",
        f"--add-data=module_capture{separator}module_capture",
        f"--add-data=module_editor{separator}module_editor",
        f"--add-data={ctk_path}{separator}customtkinter",
        "--clean",
        "main.py"
    ]
    
    print("Ejecutando PyInstaller (esto puede tardar un poco deacuerdo a tu PC)...")
    try:
        subprocess.run(cmd, check=True)
        print("¡Construcción exitosa del ejecutable!")
    except subprocess.CalledProcessError as e:
        print(f"Error durante la construcción: {e}")
        return

    # 4. Crear archivo ZIP para distribución
    dist_folder = os.path.join("dist", "QA-Screenshot")
    zip_name = "QA-Screenshot-Portable-Win64.zip"
    
    print(f"Creando paquete comprimido: {zip_name}...")
    
    # Asegurar que el zip no exista ya
    if os.path.exists(zip_name):
        os.remove(zip_name)
        
    with zipfile.ZipFile(zip_name, 'w', zipfile.ZIP_DEFLATED) as zipf:
        for root, dirs, files in os.walk(dist_folder):
            for file in files:
                abs_path = os.path.join(root, file)
                rel_path = os.path.relpath(abs_path, os.path.dirname(dist_folder))
                zipf.write(abs_path, rel_path)

    print(f"\n==========================================")
    print(f"PROCESO FINALIZADO CON ÉXITO")
    print(f"Ejecutable: {os.path.abspath(os.path.join(dist_folder, 'QA-Screenshot.exe'))}")
    print(f"Paquete ZIP: {os.path.abspath(zip_name)}")
    print(f"==========================================\n")

if __name__ == "__main__":
    # Asegurar que pyinstaller esté instalado
    try:
        import PyInstaller
    except ImportError:
        print("Instalando PyInstaller...")
        subprocess.run([sys.executable, "-m", "pip", "install", "pyinstaller"], check=True)
        
    build()
