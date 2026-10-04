import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.File;

/**
 * Punto 5 - Ejemplo en Java de 3 llamadas al sistema en Linux.
 *
 * Java no llama al kernel directamente: la JVM traduce cada clase a las
 * syscalls del sistema operativo (en Linux, a través de la libc).
 *
 *   1. Manejo de archivos  -> FileOutputStream / FileInputStream = open(), write(), read(), close()
 *   2. Identificación      -> ProcessHandle.current().pid()      = getpid()
 *   3. Control de procesos -> ProcessBuilder.start() / waitFor()  = fork/vfork + execve + waitpid
 *
 * Compilar y ejecutar:  java TresLlamadasSistema.java
 * Verificación:         strace -f -e trace=openat,write,read,getpid,vfork,execve,wait4 java TresLlamadasSistema.java
 */
public class TresLlamadasSistema {

    private static final String RUTA = "/tmp/taller3_java.txt";

    public static void main(String[] args) throws IOException, InterruptedException {

        // -----------------------------------------------------------------
        // LLAMADA 1: open / write / read / close (sistema de archivos)
        // -----------------------------------------------------------------
        System.out.println("1) Llamadas de archivos: open, write, read, close");
        try (FileOutputStream salida = new FileOutputStream(RUTA)) {        // syscall openat
            byte[] datos = "Hola desde una syscall write()\n".getBytes();
            salida.write(datos);                                             // syscall write
            System.out.println("   write() -> " + datos.length + " bytes escritos");
        }                                                                    // syscall close

        try (FileInputStream entrada = new FileInputStream(RUTA)) {          // syscall openat
            byte[] buffer = new byte[100];
            int leidos = entrada.read(buffer);                               // syscall read
            System.out.println("   read()  -> '" + new String(buffer, 0, leidos).trim() + "'\n");
        }

        // -----------------------------------------------------------------
        // LLAMADA 2: getpid (identificación del proceso)
        // -----------------------------------------------------------------
        System.out.println("2) Llamadas de identificación: getpid, getppid");
        ProcessHandle actual = ProcessHandle.current();
        System.out.println("   getpid()  -> PID de la JVM: " + actual.pid());   // syscall getpid
        System.out.println("   getppid() -> PID del padre: "
                + actual.parent().map(p -> String.valueOf(p.pid())).orElse("desconocido") + "\n");

        // -----------------------------------------------------------------
        // LLAMADA 3: fork / execve / wait (creación y control de procesos)
        // -----------------------------------------------------------------
        System.out.println("3) Llamadas de procesos: fork, execve, wait");
        ProcessBuilder pb = new ProcessBuilder("ls", "-l", RUTA);
        pb.inheritIO();                                  // el hijo escribe en nuestra misma consola
        Process hijo = pb.start();                       // syscalls vfork + execve
        System.out.println("   start()   -> hijo creado con PID " + hijo.pid());
        int codigo = hijo.waitFor();                     // syscall waitpid (wait4)
        System.out.println("   waitFor() -> el hijo terminó con código " + codigo);

        new File(RUTA).delete();                         // syscall unlink (limpieza)
    }
}
