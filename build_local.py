import os
import re
import subprocess
import sys
import build
from core.version import VERSION, APP_NAME


SEMVER_PATTERN = re.compile(
    r"^v?(?P<major>\d+)\.(?P<minor>\d+)\.(?P<patch>\d+)(?:-(?P<label>[a-zA-Z]+)\.(?P<number>\d+))?$"
)
PRERELEASE_ORDER = {
    "alpha": 0,
    "beta": 1,
    "rc": 2,
}

def get_latest_git_tag():
    # Obtiene el tag SemVer más alto disponible localmente en todo el repositorio.
    try:
        tag_output = subprocess.check_output(
            ["git", "tag", "--list"],
            stderr=subprocess.DEVNULL,
        ).decode("utf-8")
        tags = [tag.strip() for tag in tag_output.splitlines() if tag.strip()]
        semver_tags = [tag for tag in tags if SEMVER_PATTERN.match(tag)]
        if not semver_tags:
            return None
        return max(semver_tags, key=parse_version)
    except Exception:
        return None

def parse_version(v):
    # Convierte una versión SemVer en una tupla comparable, incluyendo prereleases.
    match = SEMVER_PATTERN.match(v.strip())
    if not match:
        return (0, 0, 0, -1, -1)

    major = int(match.group("major"))
    minor = int(match.group("minor"))
    patch = int(match.group("patch"))
    label = match.group("label")
    number = match.group("number")

    if not label:
        return (major, minor, patch, len(PRERELEASE_ORDER), sys.maxsize)

    prerelease_rank = PRERELEASE_ORDER.get(label.lower(), -1)
    prerelease_number = int(number) if number is not None else 0
    return (major, minor, patch, prerelease_rank, prerelease_number)

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
