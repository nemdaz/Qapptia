import socket
import threading

# Protocolo IPC versionado para evitar falsos positivos con procesos ajenos.
IPC_HOST = "127.0.0.1"
IPC_PORT_START = 49999
IPC_PORT_COUNT = 8
IPC_TIMEOUT_SECONDS = 0.2

PROTOCOL_PREFIX = b"QASCREENSHOT_IPC_V1:"
WAKE_UP_SIGNAL = b"WAKE_UP"
QUIT_SIGNAL = b"QUIT"
ACK_PREFIX = b"ACK:"

_last_known_port = None
_port_lock = threading.Lock()


def _build_message(signal):
    return PROTOCOL_PREFIX + signal


def _build_ack(signal):
    return PROTOCOL_PREFIX + ACK_PREFIX + signal


def _candidate_ports():
    with _port_lock:
        last_known_port = _last_known_port

    ports = []
    if last_known_port is not None:
        ports.append(last_known_port)

    for offset in range(IPC_PORT_COUNT):
        port = IPC_PORT_START + offset
        if port not in ports:
            ports.append(port)
    return ports


def _store_last_port(port):
    global _last_known_port
    with _port_lock:
        _last_known_port = port


def _send_signal(signal):
    message = _build_message(signal)
    expected_ack = _build_ack(signal)

    for port in _candidate_ports():
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
                s.settimeout(IPC_TIMEOUT_SECONDS)
                s.connect((IPC_HOST, port))
                s.sendall(message)
                response = s.recv(128)
                if response == expected_ack:
                    _store_last_port(port)
                    return True
        except (ConnectionRefusedError, socket.timeout, OSError):
            continue

    return False


def _bind_first_available_port():
    for offset in range(IPC_PORT_COUNT):
        port = IPC_PORT_START + offset
        server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        try:
            server_socket.bind((IPC_HOST, port))
            server_socket.listen(1)
            return server_socket, port
        except OSError:
            server_socket.close()

    return None, None


def _maybe_ack(conn, signal, received_data, callback):
    message = _build_message(signal)
    if received_data != message:
        return False
    if callback:
        callback()
    conn.sendall(_build_ack(signal))
    return True


def is_editor_running():
    """Verifica si ya hay una instancia del editor escuchando en IPC."""
    return _send_signal(WAKE_UP_SIGNAL)


def request_editor_quit():
    """Solicita a la instancia del editor que cierre su ventana principal."""
    return _send_signal(QUIT_SIGNAL)


def start_ipc_server(on_wake_up_callback, on_quit_callback=None):
    """Inicia un hilo servidor que escucha senales de despertar y cierre."""
    server_socket, bound_port = _bind_first_available_port()
    if server_socket is None:
        return None

    _store_last_port(bound_port)

    def server_thread():
        with server_socket:
            while True:
                conn, _addr = server_socket.accept()
                with conn:
                    data = conn.recv(1024)
                    if _maybe_ack(conn, WAKE_UP_SIGNAL, data, on_wake_up_callback):
                        continue
                    _maybe_ack(conn, QUIT_SIGNAL, data, on_quit_callback)

    thread = threading.Thread(target=server_thread, daemon=True)
    thread.start()
    return bound_port