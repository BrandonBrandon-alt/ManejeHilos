// =====================================================================
// Llamados al sistema desde C# - Taller 3
//
// C# no hace las syscalls directamente: la clase System.Diagnostics.Process
// y las clases de System.IO / System.Environment las traducen a las del
// sistema operativo (fork/execve/waitpid/kill en Linux, CreateProcess /
// WaitForSingleObject / TerminateProcess en Windows).
// Cuando .NET no ofrece una API, se puede llamar a la librería nativa con
// P/Invoke ([DllImport]).
// =====================================================================

using System.Diagnostics;
using System.Runtime.InteropServices;

Console.WriteLine("=== LLAMADOS AL SISTEMA DESDE C# ===\n");

// ---------------------------------------------------------------------
// 1. Detectar el sistema operativo para adecuar el comando
// ---------------------------------------------------------------------
bool esWindows = OperatingSystem.IsWindows();
Console.WriteLine($"Sistema operativo: {RuntimeInformation.OSDescription}");
Console.WriteLine($"PID de este programa: {Environment.ProcessId}\n");

// ---------------------------------------------------------------------
// 2. Configurar el proceso hijo (comando, argumentos, entorno y directorio)
//    ArgumentList separa los argumentos y evita inyección de comandos.
// ---------------------------------------------------------------------
var info = new ProcessStartInfo
{
    FileName = esWindows ? "cmd.exe" : "bash",
    RedirectStandardOutput = true,   // capturamos stdout del hijo
    RedirectStandardError = true,    // capturamos stderr del hijo
    UseShellExecute = false,         // necesario para redirigir los streams
    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
};
if (esWindows)
{
    info.ArgumentList.Add("/c");
    info.ArgumentList.Add("echo Saludos desde el SO & echo %MI_VARIABLE% & ver");
}
else
{
    info.ArgumentList.Add("-c");
    info.ArgumentList.Add("echo \"Saludos desde el SO\"; echo \"$MI_VARIABLE\"; echo \"Directorio: $(pwd)\"; uname -a");
}
info.Environment["MI_VARIABLE"] = "Valor enviado desde C#";   // variable de entorno solo para el hijo

// ---------------------------------------------------------------------
// 3. Crear el proceso (fork + execve en Linux / CreateProcess en Windows)
// ---------------------------------------------------------------------
using (var proceso = Process.Start(info)!)
{
    Console.WriteLine($"Proceso hijo creado con PID {proceso.Id}");

    // Leer stdout y stderr de forma asíncrona evita el bloqueo por
    // llenado del búfer (el mismo problema que en Java).
    Task<string> salida = proceso.StandardOutput.ReadToEndAsync();
    Task<string> errores = proceso.StandardError.ReadToEndAsync();

    // 4. Esperar al hijo con tiempo límite (waitpid en Linux)
    if (!proceso.WaitForExit(5000))
    {
        proceso.Kill(entireProcessTree: true);  // kill(SIGKILL) / TerminateProcess
        Console.WriteLine("El proceso tardó demasiado y fue terminado.");
    }

    Console.WriteLine("---- Salida del proceso hijo ----");
    Console.Write(await salida);
    string err = await errores;
    if (err.Length > 0) Console.WriteLine($"[stderr] {err}");
    Console.WriteLine($"Código de salida: {proceso.ExitCode}\n");
}

// ---------------------------------------------------------------------
// 5. Terminar un proceso desde el programa (señal kill)
// ---------------------------------------------------------------------
var dormilon = Process.Start(new ProcessStartInfo
{
    FileName = esWindows ? "timeout" : "sleep",
    Arguments = esWindows ? "/t 30" : "30",
    UseShellExecute = false,
    RedirectStandardOutput = true
})!;
Console.WriteLine($"Proceso 'sleep' iniciado con PID {dormilon.Id}; ¿terminó? {dormilon.HasExited}");
dormilon.Kill();
dormilon.WaitForExit();
Console.WriteLine($"Tras Kill(): ¿terminó? {dormilon.HasExited}, código: {dormilon.ExitCode}\n");

// ---------------------------------------------------------------------
// 6. Inspeccionar el proceso actual (similar a ProcessHandle en Java)
// ---------------------------------------------------------------------
var actual = Process.GetCurrentProcess();
Console.WriteLine("---- Información del proceso actual ----");
Console.WriteLine($"Nombre: {actual.ProcessName} | PID: {actual.Id}");
Console.WriteLine($"Hilos del S.O.: {actual.Threads.Count}");
Console.WriteLine($"Memoria física usada: {actual.WorkingSet64 / 1024} KB");
Console.WriteLine($"Tiempo total de CPU: {actual.TotalProcessorTime.TotalMilliseconds} ms\n");

// ---------------------------------------------------------------------
// 7. P/Invoke: llamar directo a la libc de Linux (getpid, getppid, getuid)
// ---------------------------------------------------------------------
if (OperatingSystem.IsLinux())
{
    Console.WriteLine("---- P/Invoke a libc ----");
    Console.WriteLine($"getpid()  = {Nativo.getpid()}");
    Console.WriteLine($"getppid() = {Nativo.getppid()}");
    Console.WriteLine($"getuid()  = {Nativo.getuid()}");
}

static class Nativo
{
    [DllImport("libc")] public static extern int getpid();
    [DllImport("libc")] public static extern int getppid();
    [DllImport("libc")] public static extern uint getuid();
}
