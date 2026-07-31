import atexit
import json
import os
import secrets
import socket
import tempfile
import threading
import time


IPC_HOST = "127.0.0.1"
IPC_TIMEOUT_SECONDS = 0.25
IPC_CONNECT_RETRIES = 5
IPC_RETRY_DELAY_SECONDS = 0.1
IPC_STATE_DIR = os.path.join(tempfile.gettempdir(), "qascreenshot_ipc")

CHANNEL_APP = "app"
CHANNEL_EDITOR = "editor"
CHANNEL_CONFIG = "config"

PROTOCOL_PREFIX = "QASCREENSHOT_IPC_V2"
ACK_SIGNAL = "ACK"
WAKE_UP_SIGNAL = "WAKE_UP"
QUIT_SIGNAL = "QUIT"
REFRESH_TRAY_ICON_SIGNAL = "REFRESH_TRAY_ICON"


def _state_file_path(channel):
    os.makedirs(IPC_STATE_DIR, exist_ok=True)
    return os.path.join(IPC_STATE_DIR, f"{channel}.json")


def _build_message(token, signal):
    return f"{PROTOCOL_PREFIX}:{token}:{signal}".encode("ascii")


def _build_ack(token, signal):
    return f"{PROTOCOL_PREFIX}:{token}:{ACK_SIGNAL}:{signal}".encode("ascii")


def _load_channel_state(channel):
    state_path = _state_file_path(channel)
    try:
        with open(state_path, "r", encoding="utf-8") as state_file:
            return json.load(state_file)
    except FileNotFoundError:
        return None
    except (OSError, ValueError, json.JSONDecodeError):
        _delete_channel_state(channel)
        return None


def _write_channel_state(channel, port, token):
    state_path = _state_file_path(channel)
    tmp_path = f"{state_path}.tmp"
    payload = {
        "pid": os.getpid(),
        "port": int(port),
        "token": token,
    }
    with open(tmp_path, "w", encoding="utf-8") as state_file:
        json.dump(payload, state_file)
    os.replace(tmp_path, state_path)


def _delete_channel_state(channel, token=None):
    state_path = _state_file_path(channel)
    if token is not None:
        state = _load_channel_state(channel)
        if not state or state.get("token") != token:
            return

    try:
        os.unlink(state_path)
    except FileNotFoundError:
        pass
    except OSError:
        pass


def _send_signal(channel, signal):
    for attempt in range(IPC_CONNECT_RETRIES):
        state = _load_channel_state(channel)
        if not state:
            return False

        port = state.get("port")
        token = state.get("token")
        if not isinstance(port, int) or not token:
            _delete_channel_state(channel)
            return False

        try:
            with socket.create_connection((IPC_HOST, port), timeout=IPC_TIMEOUT_SECONDS) as conn:
                conn.settimeout(IPC_TIMEOUT_SECONDS)
                conn.sendall(_build_message(token, signal))
                response = conn.recv(128)
                if response == _build_ack(token, signal):
                    return True
        except (ConnectionRefusedError, socket.timeout, OSError):
            if attempt < IPC_CONNECT_RETRIES - 1:
                time.sleep(IPC_RETRY_DELAY_SECONDS)
                continue

        _delete_channel_state(channel, token=token)
        return False

    return False


def _bind_server_socket():
    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server_socket.bind((IPC_HOST, 0))
    server_socket.listen(1)
    return server_socket


def _maybe_ack(conn, token, signal, received_data, callback):
    if received_data != _build_message(token, signal):
        return False
    if callback:
        callback()
    conn.sendall(_build_ack(token, signal))
    return True


def request_wake_up(channel):
    """Solicita activar una instancia existente del canal indicado."""
    return _send_signal(channel, WAKE_UP_SIGNAL)


def request_quit(channel):
    """Solicita a la instancia existente del canal indicado que termine."""
    return _send_signal(channel, QUIT_SIGNAL)


def request_refresh_tray_icon(channel):
    """Solicita a la instancia existente del canal indicado refrescar el menu del tray icon."""
    return _send_signal(channel, REFRESH_TRAY_ICON_SIGNAL)


def start_server(channel, on_wake_up_callback, on_quit_callback=None, on_refresh_tray_icon_callback=None):
    """Inicia un servidor IPC para el canal indicado usando un puerto local dinámico."""
    server_socket = _bind_server_socket()
    bound_port = server_socket.getsockname()[1]
    token = secrets.token_hex(16)
    _write_channel_state(channel, bound_port, token)

    def _cleanup():
        _delete_channel_state(channel, token=token)
        try:
            server_socket.close()
        except OSError:
            pass

    atexit.register(_cleanup)

    def server_thread():
        with server_socket:
            while True:
                try:
                    conn, _addr = server_socket.accept()
                except OSError:
                    break

                with conn:
                    try:
                        data = conn.recv(1024)
                    except OSError:
                        continue

                    signal_handlers = (
                        (WAKE_UP_SIGNAL, on_wake_up_callback),
                        (REFRESH_TRAY_ICON_SIGNAL, on_refresh_tray_icon_callback),
                        (QUIT_SIGNAL, on_quit_callback),
                    )
                    for signal, callback in signal_handlers:
                        if _maybe_ack(conn, token, signal, data, callback):
                            break

    thread = threading.Thread(target=server_thread, daemon=True)
    thread.start()
    return bound_port