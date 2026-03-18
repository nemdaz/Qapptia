import socket
import threading
import time

# Puerto dedicado para el Editor de QA Screenshot
IPC_PORT = 49999
WAKE_UP_SIGNAL = b"WAKE_UP"

def is_editor_running():
    """Verifica si ya hay una instancia del editor escuchando en el puerto IPC."""
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.settimeout(0.5)
            s.connect(('127.0.0.1', IPC_PORT))
            s.sendall(WAKE_UP_SIGNAL)
            return True
    except (ConnectionRefusedError, socket.timeout):
        return False

def start_ipc_server(on_wake_up_callback):
    """Inicia un hilo servidor que escucha señales de despertar."""
    def server_thread():
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
                s.bind(('127.0.0.1', IPC_PORT))
                s.listen(1)
                while True:
                    conn, addr = s.accept()
                    with conn:
                        data = conn.recv(1024)
                        if data == WAKE_UP_SIGNAL:
                            on_wake_up_callback()
        except OSError:
            # Probablemente el puerto ya está en uso, lo cual es correcto si ya hay una instancia
            pass

    thread = threading.Thread(target=server_thread, daemon=True)
    thread.start()
