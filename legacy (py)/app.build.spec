# -*- mode: python ; coding: utf-8 -*-

import os
from pathlib import Path


# El nombre lo inyecta build.py para evitar imports del proyecto dentro del spec.
APP_NAME = os.environ.get('_APP_NAME', 'QACappta')
APP_ICON_ICO = os.environ.get('_APP_ICON_ICO')


a = Analysis(
    ['main.py'],
    pathex=[],
    binaries=[],
    datas=[
        ('core/assets', 'core/assets'),
        ('module_editor/styles', 'module_editor/styles'),
    ],
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
    optimize=0,
)

# Mantiene el build conservador: elimina solo runtimes Qt claramente no usados
# (QML/Quick/PDF/VirtualKeyboard), preservando Core/Gui/Widgets.
qt_pruned_binaries = {
    'qt6pdf.dll',
    'qt6qml.dll',
    'qt6qmlmeta.dll',
    'qt6qmlmodels.dll',
    'qt6qmlworkerscript.dll',
    'qt6quick.dll',
    'qt6virtualkeyboard.dll',
}

a.binaries = [
    entry for entry in a.binaries
    if Path(entry[0]).name.lower() not in qt_pruned_binaries
]

pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name=APP_NAME,
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon=APP_ICON_ICO,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name=APP_NAME,
)