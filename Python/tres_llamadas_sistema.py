"""
Punto 5 - Ejemplo en Python de 3 llamadas al sistema

El módulo os de Python expone las syscalls del sistema operativo casi sin
capas intermedias, así que cada función corresponde a una llamada al kernel:

                         Linux                          Windows
  1. Manejo de archivos  open, write, read, close       CreateFile, WriteFile, ReadFile, CloseHandle
  2. Identificación      getpid, getppid                GetCurrentProcessId, ...
  3. Control de procesos fork, execve, wait4            CreateProcess, WaitForSingleObject

El taller pide Linux; en Windows el programa también corre, pero el
punto 3 usa CreateProcess porque Windows no tiene fork().

Verificación en Linux:
  strace -f -e trace=openat,write,read,close,getpid,getppid,clone,execve,wait4 python3 tres_llamadas_sistema.py
"""

import os
import subprocess
import sys
import tempfile

# Carpeta temporal del sistema: /tmp en Linux, %TEMP% en Windows
RUTA = os.path.join(tempfile.gettempdir(), "taller3_python.txt")
ES_WINDOWS = os.name == "nt"

# ---------------------------------------------------------------------
# LLAMADA 1: open / write / read / close  (sistema de archivos)
# ---------------------------------------------------------------------
print("1) Llamadas de archivos: open, write, read, close")
fd = os.open(RUTA, os.O_CREAT | os.O_WRONLY | os.O_TRUNC, 0o644)  # syscall openat
print(f"   open()  -> descriptor de archivo {fd} ({RUTA})")
escritos = os.write(fd, b"Hola desde una syscall write()\n")      # syscall write
print(f"   write() -> {escritos} bytes escritos")
os.close(fd)                                                       # syscall close

fd = os.open(RUTA, os.O_RDONLY)
contenido = os.read(fd, 100)                                       # syscall read
os.close(fd)
print(f"   read()  -> {contenido.decode().strip()!r}\n")

# ---------------------------------------------------------------------
# LLAMADA 2: getpid / getppid  (identificación del proceso)
# ---------------------------------------------------------------------
print("2) Llamadas de identificación: getpid, getppid")
print(f"   getpid()  -> PID de este programa: {os.getpid()}")
print(f"   getppid() -> PID del proceso padre: {os.getppid()}\n")

# ---------------------------------------------------------------------
# LLAMADA 3: fork / execve / wait  (creación y control de procesos)
# ---------------------------------------------------------------------
print("3) Llamadas de procesos: fork, execve, wait", flush=True)
if hasattr(os, "fork"):
    # Linux / macOS
    pid = os.fork()                                  # syscall clone (fork)
    if pid == 0:
        # Hijo: se reemplaza a sí mismo por el comando 'ls -l'
        os.execv("/bin/ls", ["ls", "-l", RUTA])      # syscall execve
    _, estado = os.waitpid(pid, 0)                   # syscall wait4
    print(f"   fork() -> el padre creó al hijo con PID {pid}")
    print(f"   wait() -> el hijo terminó con código {os.waitstatus_to_exitcode(estado)}")
else:
    # Windows no tiene fork(): se crea el proceso con CreateProcess
    print("   (Windows no tiene fork/execve; se usa CreateProcess)", flush=True)
    hijo = subprocess.Popen(["cmd.exe", "/c", "dir", RUTA])   # CreateProcess
    print(f"   CreateProcess() -> hijo creado con PID {hijo.pid}", flush=True)
    codigo = hijo.wait()                                       # WaitForSingleObject
    print(f"   wait() -> el hijo terminó con código {codigo}")

os.remove(RUTA)                                      # syscall unlink (limpieza)
sys.exit(0)
