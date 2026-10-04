# Taller 3 – Respuestas de C#

## Punto 3 – C# (características generales sobre el manejo de hilos)

- **Hilos del sistema operativo:** cada `System.Threading.Thread` de .NET se asigna 1 a 1 a un hilo nativo del S.O. (un hilo del kernel en Linux, un hilo de Windows), así que los hilos se reparten entre los núcleos y hay paralelismo real. A diferencia de Python con el GIL, no hay un bloqueo global que impida ejecutar código C# en varios núcleos a la vez.
- **Creación manual con `Thread`:** se le pasa un delegado o lambda, se inicia con `Start()` y se espera con `Join()`. Se le puede poner nombre (`Name`), prioridad (`Priority`) e indicar si es de primer o segundo plano (`IsBackground`).
- **ThreadPool y `Task`:** lo normal en C# es no crear hilos a mano, sino usar `Task.Run`, que reutiliza los hilos de un *pool* administrado por el runtime y así ahorra el costo de crearlos.
- **`async` / `await`:** permite escribir código asíncrono (E/S, esperas) que no bloquea el hilo mientras espera; cuando la operación termina, el método continúa en un hilo del pool.
- **Paralelismo de datos:** `Parallel.For`, `Parallel.ForEach` y PLINQ (`AsParallel()`) reparten un ciclo entre varios núcleos de forma automática.
- **Sincronización (exclusión mutua):** la palabra clave `lock` (internamente usa la clase `Monitor`) protege una sección crítica; además existen `Mutex`, `Semaphore`/`SemaphoreSlim`, `ReaderWriterLockSlim`, `Monitor.Wait/Pulse` (que funciona como variable de condición) e `Interlocked` para operaciones atómicas sin bloqueo.
- **Colecciones seguras para hilos:** `ConcurrentQueue`, `ConcurrentDictionary`, `ConcurrentBag` y `BlockingCollection` (sirve para el patrón productor-consumidor).
- **Cancelación cooperativa:** con `CancellationTokenSource` / `CancellationToken` se le pide a una tarea que se detenga sin matar el hilo a la fuerza.

**Evidencia:** programa `CSharp/HilosCSharp/Program.cs`. Clasifica los números del 1 al 50 en pares, impares y compuestos con 3 hilos que comparten un contador protegido con `lock` (el mismo ejercicio que en Java y Python). También muestra la condición de carrera frente a `Interlocked`, un semáforo con 2 permisos, `Task`, `Parallel.For` y la cancelación.
Para la evidencia, tome capturas del código y de la salida de `dotnet run --project CSharp/HilosCSharp`.

Ejemplo de salida obtenida:
```
--- 1. Thread + lock (exclusión mutua) ---
  [Hilo-Impares] (Id 5) terminó: 25 números
  [Hilo-Pares] (Id 4) terminó: 25 números
  [Hilo-Compuestos] (Id 6) terminó: 34 números
--- 2. Condición de carrera vs Interlocked ---
  Esperado: 400000 | Sin sincronizar: 202257 | Con Interlocked: 400000
--- 3. SemaphoreSlim (máximo 2 hilos a la vez) ---
  Tarea 1 ENTRA  (permisos libres: 1)
  Tarea 2 ENTRA  (permisos libres: 0)
  Tarea 1 SALE
  Tarea 3 ENTRA  (permisos libres: 0)
```

## Punto 4 – C# (características sobre los llamados al sistema)

- **Abstracción del sistema operativo:** la clase `System.Diagnostics.Process` encapsula la creación de procesos. En Linux, el runtime de .NET usa `vfork` + `execve` (verificado con `strace`); en Windows usa `CreateProcess`. El programador no llama a esas funciones directamente.
- **Configuración con `ProcessStartInfo`:** se define el ejecutable (`FileName`), los argumentos (`ArgumentList`, que separa cada argumento y evita la inyección de comandos), el directorio de trabajo (`WorkingDirectory`) y variables de entorno propias del hijo (`Environment`).
- **Redirección de streams:** con `RedirectStandardOutput/Error/Input` (y `UseShellExecute = false`) se capturan stdin, stdout y stderr del hijo. Hay que leerlos (por ejemplo, con `ReadToEndAsync`), porque si el búfer se llena el hijo se bloquea (el mismo problema que en Java).
- **Ciclo de vida y código de salida:** `WaitForExit()` / `WaitForExit(ms)` esperan al hijo (`wait4` en Linux), `ExitCode` devuelve su código de salida, `HasExited` indica si terminó y `Kill()` lo termina (`kill(pid, SIGKILL)` en Linux; `Kill(true)` también termina su árbol de procesos).
- **Inspección de procesos:** `Process.GetCurrentProcess()` y `Process.GetProcesses()` dan el PID, el número de hilos del S.O., la memoria usada (`WorkingSet64`) y el tiempo de CPU (`TotalProcessorTime`), de forma parecida a `ProcessHandle` en Java.
- **Otras APIs que hacen syscalls:** `System.IO` (`File`, `FileStream`, `Directory`, que usan `open`/`read`/`write`) y `System.Environment` (variables, PID, usuario).
- **P/Invoke (`[DllImport]`):** cuando .NET no ofrece una API, se puede llamar directamente a funciones de la librería nativa (`libc` en Linux, `kernel32.dll` en Windows), por ejemplo `getpid()`, `getppid()` y `getuid()`.

**Evidencia:** programa `CSharp/LlamadosSistemaCSharp/Program.cs`. Para la evidencia, tome capturas del código y de la salida de `dotnet run --project CSharp/LlamadosSistemaCSharp`.

Ejemplo de salida obtenida (Linux):
```
Proceso hijo creado con PID 1250
---- Salida del proceso hijo ----
Saludos desde el SO
Valor enviado desde C#
Directorio: /root
Linux vm 6.18.44-fc-v70 ... x86_64 GNU/Linux
Código de salida: 0
Proceso 'sleep' iniciado con PID 1253; ¿terminó? False
Tras Kill(): ¿terminó? True, código: 137
---- P/Invoke a libc ----
getpid()  = 1240
```

Syscalls observadas con `strace -f`:
```
vfork()                                   = 1276
execve("/usr/bin/bash", ["bash", "-c", ...])
wait4(1276, [{WIFEXITED(s) && WEXITSTATUS(s) == 0}], WNOHANG, NULL) = 1276
execve("/usr/bin/sleep", ["sleep", "30"], ...)
kill(1279, SIGKILL)                       = 0
```

## Cómo ejecutar
Requiere el .NET SDK 8 (o una versión más reciente).
```
dotnet run --project CSharp/HilosCSharp
dotnet run --project CSharp/LlamadosSistemaCSharp
```
