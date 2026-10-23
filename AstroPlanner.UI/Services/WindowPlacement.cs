using System.IO;
using System.Text.Json;
using System.Windows;

namespace AstroPlanner.UI.Services;

/// <summary>Tamaño, posición y estado de la ventana principal entre sesiones.</summary>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool IsMaximized)
{
    /// <summary>Lo mínimo de la barra de título que debe quedar a la vista para poder arrastrar la ventana.</summary>
    private const double MinVisible = 120;

    /// <summary>
    /// Ajusta la ventana al escritorio actual (<paramref name="virtualScreen"/>:
    /// todos los monitores). Si quedó fuera, p. ej. porque se desconectó un
    /// monitor, se centra en el área de trabajo del principal y se ajusta a ella.
    /// </summary>
    public WindowPlacement FitTo(Rect virtualScreen, Rect workArea, double minWidth, double minHeight)
    {
        // Límite por todo el escritorio, no por el monitor principal: una ventana
        // grande en un segundo monitor más grande conserva su tamaño.
        var width = Math.Clamp(Width, minWidth, Math.Max(minWidth, virtualScreen.Width));
        var height = Math.Clamp(Height, minHeight, Math.Max(minHeight, virtualScreen.Height));

        // La barra de título (franja superior) debe verse en parte dentro del escritorio.
        var titleBar = new Rect(Left, Top, width, 32);
        titleBar.Intersect(virtualScreen);
        var reachable = !titleBar.IsEmpty && titleBar.Width >= Math.Min(MinVisible, width) && Top >= virtualScreen.Top;
        if (reachable)
        {
            return this with { Width = width, Height = height };
        }

        width = Math.Clamp(width, minWidth, Math.Max(minWidth, workArea.Width));
        height = Math.Clamp(height, minHeight, Math.Max(minHeight, workArea.Height));
        return this with
        {
            Width = width,
            Height = height,
            Left = workArea.Left + (workArea.Width - width) / 2,
            Top = workArea.Top + (workArea.Height - height) / 2
        };
    }
}

/// <summary>Guarda la posición de la ventana en %LocalAppData%\AstroPlanner\window.json.</summary>
public sealed class WindowPlacementStore(string? filePath = null)
{
    private readonly string _filePath = filePath ?? AppPaths.DataFile("window.json");

    public WindowPlacement? Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return null;
            var placement = JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(_filePath));
            return placement is { Width: > 0, Height: > 0 } && double.IsFinite(placement.Left) && double.IsFinite(placement.Top)
                ? placement
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;   // sin posición guardada: la ventana se centra
        }
    }

    public void Save(WindowPlacement placement)
    {
        try
        {
            AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(placement));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No poder recordar la posición no debe impedir cerrar la app.
        }
    }
}
