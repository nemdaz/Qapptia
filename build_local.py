import os
import subprocess
import sys
import build
from core.version import VERSION, APP_NAME

def get_latest_git_tag():
    # Obtiene el último tag de Git de forma local.
    try:
        tag = subprocess.check_output(["git", "describe", "--tags", "--abbrev=0"], 
                                         stderr=subprocess.DEVNULL).decode('utf-8').strip()
        return tag
    except:
        return None

def parse_version(v):
    # Convierte una versión en una tupla comparable.
    v_clean = v.lstrip('v').split('-')[0]
    try:
        return tuple(map(int, v_clean.split('.')))
    except:
        return (0, 0, 0)

def is_version_higher(current, latest_tag):
    # Verifica si la versión actual es superior al último tag.
    if not latest_tag:
        return True
        
    curr_tuple = parse_version(current)
    tag_tuple = parse_version(latest_tag)
    return curr_tuple > tag_tuple

def build_local():
    print(f"--- {APP_NAME}: Constructor Local (SemVer Control) ---")
    
    latest_tag = get_latest_git_tag()
    
    if latest_tag:
        print(f"Última versión en Git: {latest_tag}")
        if not is_version_higher(VERSION, latest_tag):
            print(f"\nERROR DE REGRESIÓN:")
            print(f"Versión local actual: {VERSION}")
            print(f"Versión en Git: {latest_tag}")
            print("\nERROR: Debes subir la versión en core/version.py para poder construir.")
            sys.exit(1)
    else:
        print("No se detectaron versiones previas en Git.")

    print(f"Validación exitosa. Iniciando proceso...")
    try:
        build.run_build(VERSION)
    except Exception as e:
        print(f"Error durante el proceso: {e}")

if __name__ == "__main__":
    build_local()
