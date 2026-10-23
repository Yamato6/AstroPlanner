using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AstroPlanner.UI.Services;

/// <summary>Detalles de Windows que WPF no expone.</summary>
internal static class NativeWindows
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Barra de título oscura, a juego con el tema (Windows 10 20H1+ y Windows 11).
    /// Se aplica en SourceInitialized, antes de que la ventana se pinte por
    /// primera vez; en versiones anteriores de Windows no hace nada.
    /// </summary>
    public static void UseDarkTitleBar(Window window) =>
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            var enabled = 1;
            if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
            }
        };

    /// <summary>Muestra y pone delante una ventana (p. ej. al abrir la app por segunda vez).</summary>
    public static void BringToFront(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            // Restaura al estado anterior (maximizada si lo estaba), no siempre a "normal".
            SystemCommands.RestoreWindow(window);
        }

        window.Show();
        window.Activate();
        // Truco habitual: Windows no deja robar el foco, pero sí subir la ventana un instante.
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }
}

/// <summary>
/// Una sola instancia de AstroPlanner por sesión de usuario: dos procesos
/// escribirían a la vez el plan y la bitácora. La segunda instancia avisa a la
/// primera (que se pone delante) y se cierra.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\AstroPlanner.SingleInstance.6F1C2B7E";
    private const string EventName = @"Local\AstroPlanner.Activate.6F1C2B7E";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, bool isFirstInstance, EventWaitHandle activate)
    {
        _mutex = mutex;
        IsFirstInstance = isFirstInstance;
        _activate = activate;
    }

    /// <summary>
    /// null si no se pueden abrir los objetos con nombre (p. ej. los creó una
    /// instancia abierta como administrador): entonces la app arranca igual,
    /// sin la comprobación de instancia única, en lugar de cerrarse sin avisar.
    /// </summary>
    public static SingleInstance? TryCreate()
    {
        try
        {
            var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            try
            {
                return new SingleInstance(mutex, createdNew, new EventWaitHandle(false, EventResetMode.AutoReset, EventName));
            }
            catch
            {
                mutex.Dispose();
                throw;
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or WaitHandleCannotBeOpenedException or IOException)
        {
            return null;
        }
    }

    public bool IsFirstInstance { get; }

    /// <summary>Pide a la instancia que ya está abierta que se muestre.</summary>
    public void ActivateFirstInstance() => _activate.Set();

    /// <summary>Llama a <paramref name="onActivate"/> (en otro hilo) cada vez que se abre una segunda instancia.</summary>
    public void ListenForActivation(Action onActivate) =>
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activate, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _registration?.Unregister(null);
        if (IsFirstInstance)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _activate.Dispose();
    }
}
