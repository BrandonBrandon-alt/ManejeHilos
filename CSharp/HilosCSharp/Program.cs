// =====================================================================
// Ejercicio de Hilos Concurrentes - C#
// Taller 3 - Infraestructura Computacional
//
// Clasifica los números de 1 a N en tres grupos (pares, impares y
// compuestos). Cada grupo lo calcula un hilo distinto y todos comparten
// un mismo contador global protegido con exclusión mutua.
//
// Se muestran las herramientas principales de C# para manejar hilos:
//   1. System.Threading.Thread         -> hilos del S.O. creados a mano
//   2. lock (Monitor)                   -> exclusión mutua / sección crítica
//   3. Interlocked                      -> operaciones atómicas sin lock
//   4. SemaphoreSlim                    -> semáforo que limita el acceso
//   5. Task / async-await (ThreadPool)  -> concurrencia de alto nivel
//   6. Parallel.For                     -> paralelismo de datos en varios núcleos
//   7. CancellationToken                -> cancelación cooperativa
// =====================================================================

using System.Collections.Concurrent;
using System.Diagnostics;

const int N = 50;

Console.WriteLine("=== EJERCICIO DE HILOS CONCURRENTES - C# ===");
Console.WriteLine($"Núcleos lógicos disponibles: {Environment.ProcessorCount}");
Console.WriteLine($"Hilo principal -> Id administrado: {Environment.CurrentManagedThreadId}\n");

// ---------------------------------------------------------------------
// 1. HILOS CLÁSICOS (Thread) + lock (Monitor) para la sección crítica
// ---------------------------------------------------------------------
Console.WriteLine("--- 1. Thread + lock (exclusión mutua) ---");

var resultados = new Dictionary<string, List<int>>
{
    ["pares"] = new(),
    ["impares"] = new(),
    ["compuestos"] = new()
};
object candado = new();     // objeto usado como monitor
int totalClasificados = 0;  // recurso compartido por los 3 hilos

void Clasificar(string grupo, Func<int, bool> criterio)
{
    for (int i = 1; i <= N; i++)
    {
        if (!criterio(i)) continue;

        // SECCIÓN CRÍTICA: solo un hilo a la vez puede entrar aquí
        lock (candado)
        {
            resultados[grupo].Add(i);
            totalClasificados++;
        }
        Thread.Sleep(5); // simula trabajo para que se note la intercalación
    }
    Console.WriteLine($"  [{Thread.CurrentThread.Name}] (Id {Environment.CurrentManagedThreadId}) terminó: {resultados[grupo].Count} números");
}

var hilos = new[]
{
    new Thread(() => Clasificar("pares", n => n % 2 == 0))      { Name = "Hilo-Pares" },
    new Thread(() => Clasificar("impares", n => n % 2 != 0))    { Name = "Hilo-Impares" },
    new Thread(() => Clasificar("compuestos", EsCompuesto))     { Name = "Hilo-Compuestos", Priority = ThreadPriority.BelowNormal }
};

var reloj = Stopwatch.StartNew();
foreach (var h in hilos) h.Start();   // los tres hilos corren de forma concurrente
foreach (var h in hilos) h.Join();    // el hilo principal espera a que terminen
reloj.Stop();

Console.WriteLine($"  Pares      : {string.Join(", ", resultados["pares"])}");
Console.WriteLine($"  Impares    : {string.Join(", ", resultados["impares"])}");
Console.WriteLine($"  Compuestos : {string.Join(", ", resultados["compuestos"])}");
Console.WriteLine($"  Total clasificados (contador compartido): {totalClasificados} en {reloj.ElapsedMilliseconds} ms\n");

// ---------------------------------------------------------------------
// 2. Condición de carrera vs. Interlocked (operación atómica)
// ---------------------------------------------------------------------
Console.WriteLine("--- 2. Condición de carrera vs Interlocked ---");
int inseguro = 0, seguro = 0;
var incrementadores = Enumerable.Range(0, 4).Select(_ => new Thread(() =>
{
    for (int i = 0; i < 100_000; i++)
    {
        inseguro++;                         // NO atómico: lee, suma y escribe
        Interlocked.Increment(ref seguro);  // atómico: lo garantiza la CPU
    }
})).ToList();
incrementadores.ForEach(t => t.Start());
incrementadores.ForEach(t => t.Join());
Console.WriteLine($"  Esperado: 400000 | Sin sincronizar: {inseguro} | Con Interlocked: {seguro}\n");

// ---------------------------------------------------------------------
// 3. Semáforo: máximo 2 hilos dentro del recurso al mismo tiempo
// ---------------------------------------------------------------------
Console.WriteLine("--- 3. SemaphoreSlim (máximo 2 hilos a la vez) ---");
var semaforo = new SemaphoreSlim(2);
var tareasSemaforo = Enumerable.Range(1, 5).Select(async id =>
{
    await semaforo.WaitAsync();             // P(): pide permiso
    try
    {
        Console.WriteLine($"  Tarea {id} ENTRA  (permisos libres: {semaforo.CurrentCount})");
        await Task.Delay(200);
        Console.WriteLine($"  Tarea {id} SALE");
    }
    finally
    {
        semaforo.Release();                 // V(): libera el permiso
    }
});
await Task.WhenAll(tareasSemaforo);
Console.WriteLine();

// ---------------------------------------------------------------------
// 4. Task + async/await: el ThreadPool reutiliza hilos
// ---------------------------------------------------------------------
Console.WriteLine("--- 4. Task / async-await (ThreadPool) ---");
Task<int> contarPares = Task.Run(() => Enumerable.Range(1, N).Count(n => n % 2 == 0));
Task<int> contarImpares = Task.Run(() => Enumerable.Range(1, N).Count(n => n % 2 != 0));
Task<int> contarCompuestos = Task.Run(() => Enumerable.Range(1, N).Count(EsCompuesto));
int[] conteos = await Task.WhenAll(contarPares, contarImpares, contarCompuestos);
Console.WriteLine($"  Pares={conteos[0]}  Impares={conteos[1]}  Compuestos={conteos[2]}");
Console.WriteLine($"  ¿El hilo actual es del ThreadPool? {Thread.CurrentThread.IsThreadPoolThread}\n");

// ---------------------------------------------------------------------
// 5. Parallel.For: paralelismo real repartido entre núcleos
// ---------------------------------------------------------------------
Console.WriteLine("--- 5. Parallel.For + ConcurrentBag ---");
var hilosUsados = new ConcurrentBag<int>();
long sumaCompuestos = 0;
Parallel.For(1, 200_001, i =>
{
    hilosUsados.Add(Environment.CurrentManagedThreadId);
    if (EsCompuesto(i)) Interlocked.Add(ref sumaCompuestos, i);
});
Console.WriteLine($"  Suma de compuestos hasta 200000: {sumaCompuestos}");
Console.WriteLine($"  Hilos distintos que participaron: {hilosUsados.Distinct().Count()}\n");

// ---------------------------------------------------------------------
// 6. Cancelación cooperativa con CancellationToken
// ---------------------------------------------------------------------
Console.WriteLine("--- 6. CancellationToken ---");
using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
var trabajoLargo = Task.Run(async () =>
{
    int vueltas = 0;
    while (!cts.Token.IsCancellationRequested)
    {
        vueltas++;
        await Task.Delay(50);
    }
    return vueltas;
});
Console.WriteLine($"  La tarea se canceló sola tras {await trabajoLargo} iteraciones");

Console.WriteLine("\n=== FIN ===");

// Un número compuesto es un entero mayor que 1 con al menos un divisor
// distinto de 1 y de sí mismo.
static bool EsCompuesto(int n)
{
    if (n <= 3) return false;
    for (int i = 2; i * i <= n; i++)
        if (n % i == 0) return true;
    return false;
}
