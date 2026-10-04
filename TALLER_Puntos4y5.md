# Taller 3 – Punto 4 (Python) y Punto 5 (Java y Python)

> Las respuestas de C# (puntos 3 y 4) están en `CSharp/TALLER_CSharp.md`.

## Punto 4 – Python (características sobre los llamados al sistema)

- **Dos niveles de acceso:** el módulo `os` tiene funciones que corresponden casi 1 a 1 con las syscalls de POSIX (`os.getpid`, `os.fork`, `os.execvp`, `os.waitpid`, `os.kill`, `os.open`, `os.read`, `os.write`). El módulo `subprocess` es una abstracción de alto nivel para crear procesos: en Linux usa internamente fork/vfork + `execve` + `waitpid` y en Windows usa `CreateProcess`.
- **Creación de procesos con `subprocess.run` / `subprocess.Popen`:** el comando se pasa como lista de argumentos, lo que evita la inyección de comandos (con `shell=True` se pierde esa protección). `run` ejecuta el proceso y espera a que termine; `Popen` lo deja corriendo en segundo plano y permite controlarlo manualmente.
- **Gestión de streams (E/S):** con `capture_output=True` o `stdout=PIPE` / `stderr=PIPE` se capturan las salidas del hijo por medio de tuberías (pipes). `communicate()` las lee completas y evita el bloqueo por llenado del búfer (el mismo problema que en Java). Con `text=True` los bytes se convierten a cadenas de texto.
- **Ciclo de vida y código de salida:** `returncode` devuelve el código de salida del hijo, y si es negativo indica que lo terminó una señal (por ejemplo, `-9` = `SIGKILL`). Además, `timeout=` lanza `TimeoutExpired` si el hijo tarda demasiado, `poll()` consulta si ya terminó sin bloquear, `wait()` lo espera, y `kill()` / `terminate()` envían `SIGKILL` / `SIGTERM`.
- **Contexto de ejecución:** `env=` define las variables de entorno del hijo y `cwd=` su directorio de trabajo.
- **Portabilidad:** varias funciones de `os` solo existen en Unix (por ejemplo `fork` y `getuid`), así que el programa debe revisar `os.name` o `platform.system()` antes de usarlas.

**Evidencia:** `Python/llamados_sistema.py`. Ejecútalo con `python3 Python/llamados_sistema.py` y toma capturas del código y de la salida.

Salida obtenida (Linux):
```
---- Salida del proceso hijo ----
Saludos desde el SO
Valor enviado desde Python
Directorio: /root
Código de salida: 0

Proceso 'sleep' iniciado con PID 443; ¿terminó? False
Tras kill(): ¿terminó? True, código: -9
(código negativo = terminado por la señal SIGKILL)

---- Llamadas directas con el módulo os ----
os.getpid()  = 440
os.getppid() = 439
  Soy el hijo (PID 444) creado con fork + execvp
  El padre recogió al hijo 444, código de salida: 0
```

---

## Punto 5 – Ejemplo en Java: 3 llamadas al sistema (Linux)

Archivo: `Java/TresLlamadasSistema.java`. Se ejecuta con `java Java/TresLlamadasSistema.java` (Java 11 o una versión más reciente).

| # | Llamada al sistema | Cómo se hace en Java | Qué hace |
|---|---|---|---|
| 1 | `openat`, `write`, `read`, `close` | `new FileOutputStream(ruta)`, `.write()`, `new FileInputStream(ruta)`, `.read()`, cerrar con `try-with-resources` | Pide al kernel que abra un archivo y devuelva un descriptor; con ese descriptor escribe y lee bytes, y al final lo libera. |
| 2 | `getpid` | `ProcessHandle.current().pid()` | El kernel devuelve el identificador (PID) del proceso de la JVM. |
| 3 | `clone3` (vfork), `execve`, `wait4` | `new ProcessBuilder("ls","-l",ruta).start()` y `waitFor()` | El kernel crea un proceso hijo, lo reemplaza por el programa `ls` y el padre espera a que termine para recibir su código de salida. |

Salida obtenida:
```
1) Llamadas de archivos: open, write, read, close
   write() -> 31 bytes escritos
   read()  -> 'Hola desde una syscall write()'

2) Llamadas de identificación: getpid, getppid
   getpid()  -> PID de la JVM: 447
   getppid() -> PID del padre: 439

3) Llamadas de procesos: fork, execve, wait
-rw-r--r-- 1 root root 31 Oct  4 23:51 /tmp/taller3_java.txt
   start()   -> hijo creado con PID 470
   waitFor() -> el hijo terminó con código 0
```

Prueba con `strace -f` de que la JVM sí hace esas syscalls (salida recortada):
```
openat(AT_FDCWD, "/tmp/taller3_java.txt", O_WRONLY|O_CREAT|O_TRUNC, 0666) = 4
write(4, "Hola desde una syscall write()\n", 31) = 31
openat(AT_FDCWD, "/tmp/taller3_java.txt", O_RDONLY) = 4
read(4, "Hola desde una syscall write()\n", 100) = 31
getpid()                          = 494
clone3({flags=CLONE_VM|CLONE_VFORK|CLONE_CLEAR_SIGHAND, exit_signal=SIGCHLD, ...}) = 575
execve(".../lib/jspawnhelper", ...) = 0
execve("/usr/bin/ls", ["ls", "-l", "/tmp/taller3_java.txt"], ...) = 0
wait4(516, [{WIFEXITED(s) && WEXITSTATUS(s) == 0}], 0, NULL) = 516
```
Nota: la línea de `clone3` viene de una segunda ejecución con `strace`, por eso su PID (575) no coincide con el de las demás (516). En Java 21 sobre Linux, `ProcessBuilder` usa `posix_spawn`, que se ve como `clone3` con la bandera `CLONE_VFORK`. Primero ejecuta un programa auxiliar de la JVM (`jspawnhelper`) y este hace el `execve` final del comando pedido.

---

## Punto 5 – Ejemplo en Python: 3 llamadas al sistema (Linux)

Archivo: `Python/tres_llamadas_sistema.py`. Se ejecuta con `python3 Python/tres_llamadas_sistema.py`.

| # | Llamada al sistema | Cómo se hace en Python | Qué hace |
|---|---|---|---|
| 1 | `openat`, `write`, `read`, `close` | `os.open()`, `os.write()`, `os.read()`, `os.close()` | Abre un archivo y obtiene un descriptor (número entero), escribe y lee bytes con él, y lo cierra. |
| 2 | `getpid`, `getppid` | `os.getpid()`, `os.getppid()` | El kernel devuelve el PID del programa y el de su proceso padre. |
| 3 | `clone` (fork), `execve`, `wait4` | `os.fork()`, `os.execv()`, `os.waitpid()` | `fork` duplica el proceso; el hijo se reemplaza por `ls` con `execve` y el padre espera a que termine con `wait4`. |

Salida obtenida:
```
1) Llamadas de archivos: open, write, read, close
   open()  -> descriptor de archivo 3
   write() -> 31 bytes escritos
   read()  -> 'Hola desde una syscall write()'

2) Llamadas de identificación: getpid, getppid
   getpid()  -> PID de este programa: 445
   getppid() -> PID del proceso padre: 439

3) Llamadas de procesos: fork, execve, wait
   fork() -> el padre creó al hijo con PID 446
-rw-r--r-- 1 root root 31 Oct  4 23:51 /tmp/taller3_python.txt
   wait() -> el hijo terminó con código 0
```

Prueba con `strace -f` (salida recortada):
```
openat(AT_FDCWD, "/tmp/taller3_python.txt", O_WRONLY|O_CREAT|O_TRUNC|O_CLOEXEC, 0644) = 3
write(3, "Hola desde una syscall write()\n", 31) = 31
openat(AT_FDCWD, "/tmp/taller3_python.txt", O_RDONLY|O_CLOEXEC) = 3
read(3, "Hola desde una syscall write()\n", 100) = 31
getpid()                          = 486
getppid()                         = 483
clone(child_stack=NULL, flags=CLONE_CHILD_CLEARTID|CLONE_CHILD_SETTID|SIGCHLD, ...) = 487
execve("/bin/ls", ["ls", "-l", "/tmp/taller3_python.txt"], ...) = 0
wait4(487, [{WIFEXITED(s) && WEXITSTATUS(s) == 0}], 0, NULL) = 487
```
Nota: en Linux, `os.fork()` aparece como `clone` porque la glibc implementa `fork()` con esa syscall.

### Cómo ver las syscalls tú misma (Linux o WSL)
```
strace -f -e trace=openat,write,read,getpid,getppid,clone,clone3,execve,wait4 python3 Python/tres_llamadas_sistema.py
strace -f -e trace=openat,write,read,getpid,clone3,execve,wait4 java Java/TresLlamadasSistema.java
```
