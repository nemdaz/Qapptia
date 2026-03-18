import os
import subprocess
import shutil
import sys
import zipfile
from core.version import VERSION

def is_version_valid(current_v):
    history_file = "build_history.txt"
    if not os.path.exists(history_file):
        return True
    
    with open(history_file, "r") as f:
        versions = [line.strip() for line in f.readlines() if line.strip()]
    
    if not versions:
        return True
    
    # Simple comparación SemVer lógica (Major.Minor.Patch)
    # Para mayor robustez, se podría usar packaging.version, 
    # pero mantendremos dependencias mínimas.
    last_v = versions[-1]
    
    if current_v == last_v:
        print(f"Error: La versión {current_v} ya ha sido construida anteriormente.")
        return False
        
    # Aquí podríamos añadir lógica de comparación jerárquica más compleja
    # Por ahora, bloqueo por duplicidad exacta es el primer escudo.
    return True

def save_build_history(v):
    with open("build_history.txt", "a") as f:
        f.write(v + "\n")

def build():
    print(f"Iniciando construcción de {VERSION}...")
    
    if not is_version_valid(VERSION):
        print("Abortando build para proteger la integridad de versiones.")
        return

    # Verificar si el ZIP ya existe
    zip_name = f"QA-Screenshot-v{VERSION}-Win64.zip"
    if os.path.exists(zip_name):
        print(f"Error: El archivo {zip_name} ya existe. Sube la versión en core/version.py")
        return
    
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
    # zip_name ya se definió al inicio para validación
    
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

    save_build_history(VERSION)

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
