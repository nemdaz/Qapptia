# Plan de Trabajo Original y Tareas Completadas

A continuación se detalla el plan de implementación original y las tareas completadas para el desarrollo del capturador de pantalla.

---

## 📋 Lista de Tareas Completadas

- [x] Planificación de la aplicación
  - [x] Crear plan de implementación
  - [x] Revisar con el usuario
- [x] Implementación Base
  - [x] Crear y configurar el script principal (`main.py`)
  - [x] Implementar captura de pantalla (Pillow, winsound)
  - [x] Implementar atajo de teclado global (keyboard)
  - [x] Implementar icono en la bandeja del sistema (pystray)
- [x] Implementar Modos de Captura (NUEVO)
  - [x] Añadir dependencia `mouse`
  - [x] Implementar estado para el modo 'Flujo' (empezar/terminar)
  - [x] Implementar listener de eventos de click del mouse
  - [x] Modificar menú del Tray Icon para soportar Iniciar/Detener Flujo de Capturas
- [x] Verificación
  - [x] Instalar nuevas dependencias
  - [x] Validar captura Manual (desde el menú)
  - [x] Validar captura por Atajo (`Ctrl + Shift + K`)
  - [x] Validar Modo Flujo (clics del mouse generan capturas consecutivas)

---

## 🏗️ Plan de Implementación (Histórico)

La aplicación será un script de Python que se ejecutará en segundo plano, mostrará un icono en la bandeja del sistema de Windows, y capturará toda la pantalla cuando el usuario presione una combinación de teclas o interactúe con el ratón. Las capturas se guardarán automáticamente en la carpeta de Descargas del usuario.

### Archivos
#### `requirements.txt`
Contiene las dependencias:
- `pystray`
- `keyboard`
- `mouse` (Para interceptar eventos de click)
- `Pillow`

#### `main.py`
El script principal implementa tres "modos" de captura:
1.  **Modo Manual**: Clic en la opción "Capturar ahora" del menú System Tray.
2.  **Modo Atajo**: El listener global para `Ctrl + Shift + K` se mantiene y funciona independientemente.
3.  **Modo Flujo**:
    - Un toggle state `is_flow_active` boolean.
    - Opciones en el menú: "Iniciar Flujo" y "Detener Flujo".
    - `mouse.hook()` que intercepta la pulsación izquierda y manda a invocar `capture_screen()` automáticamente.

### Verificación
1. Instalar dependencias requeridas.
2. Ejecutar `python main.py`.
3. Validar que el icono aparece en la bandeja del sistema (al lado del reloj).
4. Presionar `Ctrl + Shift + S` (actualizado a `Ctrl + Shift + K`) estando en cualquier ventana y verificar que el archivo png apareció en "Descargas".
5. Usar el menú de la bandeja para salir de forma limpia.
