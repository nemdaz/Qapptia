"""
Tema oscuro forzado: aplica estilo Fusion + paleta oscura a nivel QApplication,
ignorando el tema del sistema operativo. Receta canonica Qt para dark mode.
"""
from __future__ import annotations

from PySide6.QtGui import QColor, QPalette
from PySide6.QtWidgets import QApplication


def apply_dark_theme(app: QApplication) -> None:
    app.setStyle("Fusion")
    pal = QPalette()
    pal.setColor(QPalette.ColorRole.Window, QColor("#2b2b2b"))
    pal.setColor(QPalette.ColorRole.WindowText, QColor("#ffffff"))
    pal.setColor(QPalette.ColorRole.Base, QColor("#1a1a1a"))
    pal.setColor(QPalette.ColorRole.AlternateBase, QColor("#2b2b2b"))
    pal.setColor(QPalette.ColorRole.Text, QColor("#ffffff"))
    pal.setColor(QPalette.ColorRole.Button, QColor("#2b2b2b"))
    pal.setColor(QPalette.ColorRole.ButtonText, QColor("#ffffff"))
    pal.setColor(QPalette.ColorRole.ToolTipBase, QColor("#2b2b2b"))
    pal.setColor(QPalette.ColorRole.ToolTipText, QColor("#ffffff"))
    pal.setColor(QPalette.ColorRole.PlaceholderText, QColor("#999999"))
    pal.setColor(QPalette.ColorRole.Highlight, QColor("#1a73e8"))
    pal.setColor(QPalette.ColorRole.HighlightedText, QColor("#ffffff"))
    disabled = QColor("#999999")
    pal.setColor(QPalette.ColorGroup.Disabled, QPalette.ColorRole.WindowText, disabled)
    pal.setColor(QPalette.ColorGroup.Disabled, QPalette.ColorRole.Text, disabled)
    pal.setColor(QPalette.ColorGroup.Disabled, QPalette.ColorRole.ButtonText, disabled)
    app.setPalette(pal)
