"""
Llamados al sistema desde Python - Taller 3 (Punto 4)

Python ofrece dos niveles para hablar con el sistema operativo:
  * Módulo os: funciones casi 1 a 1 con las syscalls de POSIX
    (os.getpid, os.fork, os.execvp, os.waitpid, os.kill, os.open...).
  * Módulo subprocess: abstracción de alto nivel para crear procesos
    (internamente usa fork/vfork + execve + waitpid en Linux y
    CreateProcess en Windows).
"""

import os
import platform
import signal
import subprocess
import sys

print("=== LLAMADOS AL SISTEMA DESDE PYTHON ===\n")

# ---------------------------------------------------------------------
# 1. Detectar el sistema operativo para adecuar el comando
# ---------------------------------------------------------------------
es_windows = os.name == "nt"
print(f"Sistema operativo: {platform.system()} {platform.release()}")
print(f"PID de este programa: {os.getpid()}\n")

# ---------------------------------------------------------------------
# 2. Configurar el proceso hijo: comando como lista (evita inyección),
#    variables de entorno propias y directorio de trabajo.
# ---------------------------------------------------------------------
if es_windows:
    comando = ["cmd.exe", "/c", "echo Saludos desde el SO & echo %MI_VARIABLE% & ver"]
else:
    comando = ["bash", "-c",
               'echo "Saludos desde el SO"; echo "$MI_VARIABLE"; echo "Directorio: $(pwd)"; uname -a']

entorno = os.environ.copy()
entorno["MI_VARIABLE"] = "Valor enviado desde Python"

# ---------------------------------------------------------------------
# 3. subprocess.run: crea el proceso, captura stdout/stderr y espera,
#    con tiempo límite (timeout).
# ---------------------------------------------------------------------
try:
    resultado = subprocess.run(
        comando,
        capture_output=True,      # redirige stdout y stderr a tuberías (pipes)
        text=True,                # decodifica los bytes a str
        env=entorno,
        cwd=os.path.expanduser("~"),
        timeout=5,
    )
    print("---- Salida del proceso hijo ----")
    print(resultado.stdout, end="")
    if resultado.stderr:
        print(f"[stderr] {resultado.stderr}")
    print(f"Código de salida: {resultado.returncode}\n")
except subprocess.TimeoutExpired:
    print("El proceso tardó demasiado y fue terminado.\n")

# ---------------------------------------------------------------------
# 4. subprocess.Popen: control manual del ciclo de vida (no bloqueante)
# ---------------------------------------------------------------------
dormilon = subprocess.Popen(["timeout", "/t", "30"] if es_windows else ["sleep", "30"],
                            stdout=subprocess.DEVNULL)
print(f"Proceso 'sleep' iniciado con PID {dormilon.pid}; ¿terminó? {dormilon.poll() is not None}")
dormilon.kill()                   # kill(pid, SIGKILL) / TerminateProcess
codigo = dormilon.wait()          # waitpid
print(f"Tras kill(): ¿terminó? {dormilon.poll() is not None}, código: {codigo}")
if not es_windows:
    print(f"(código negativo = terminado por la señal {signal.Signals(-codigo).name})\n")

# ---------------------------------------------------------------------
# 5. Módulo os: llamadas directas a las syscalls POSIX (solo Linux/Unix)
# ---------------------------------------------------------------------
if not es_windows:
    print("---- Llamadas directas con el módulo os ----")
    print(f"os.getpid()  = {os.getpid()}")
    print(f"os.getppid() = {os.getppid()}")
    print(f"os.getuid()  = {os.getuid()}")

    pid = os.fork()                       # duplica el proceso actual
    if pid == 0:
        # Proceso hijo: se reemplaza por el programa 'echo'
        os.execvp("echo", ["echo", f"  Soy el hijo (PID {os.getpid()}) creado con fork + execvp"])
    else:
        _, estado = os.waitpid(pid, 0)    # el padre espera al hijo
        print(f"  El padre recogió al hijo {pid}, código de salida: {os.waitstatus_to_exitcode(estado)}")

sys.exit(0)
