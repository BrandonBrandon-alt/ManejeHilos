"""
Punto 5 - Ejemplo en Python de 3 llamadas al sistema en Linux

El módulo os de Python expone las syscalls de Linux casi sin capas
intermedias, así que cada función corresponde a una llamada al kernel:

  1. Manejo de archivos  -> open(), write(), read(), close()
  2. Identificación      -> getpid(), getppid()
  3. Control de procesos -> fork(), execve(), wait4()

Verificación:  strace -f -e trace=openat,write,read,close,getpid,getppid,clone,execve,wait4 python3 tres_llamadas_sistema.py
"""

import os

RUTA = "/tmp/taller3_python.txt"

# ---------------------------------------------------------------------
# LLAMADA 1: open / write / read / close  (sistema de archivos)
# ---------------------------------------------------------------------
print("1) Llamadas de archivos: open, write, read, close")
fd = os.open(RUTA, os.O_CREAT | os.O_WRONLY | os.O_TRUNC, 0o644)  # syscall openat
print(f"   open()  -> descriptor de archivo {fd}")
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
pid = os.fork()                                  # syscall clone (fork)
if pid == 0:
    # Hijo: se reemplaza a sí mismo por el comando 'ls -l'
    os.execv("/bin/ls", ["ls", "-l", RUTA])      # syscall execve
else:
    print(f"   fork() -> el padre creó al hijo con PID {pid}", flush=True)
    _, estado = os.waitpid(pid, 0)               # syscall wait4
    print(f"   wait() -> el hijo terminó con código {os.waitstatus_to_exitcode(estado)}")
    os.remove(RUTA)                              # syscall unlink (limpieza)
