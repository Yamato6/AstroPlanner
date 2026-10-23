using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AstroPlanner.UI.Services;

namespace AstroPlanner.UI.ViewModels;

/// <summary>
/// Bitácora de observaciones: entradas registradas, objetos ya observados y
/// progreso del maratón Messier.
/// </summary>
public sealed partial class LogbookViewModel : ObservableObject
{
    public const int MessierTotal = 110;

    private readonly IObservationLogService _service;
    private readonly IAppLogger _logger;
    private HashSet<string> _observedIds = [];

    public LogbookViewModel(IObservationLogService service, IAppLogger logger)
    {
        _service = service;
        _logger = logger;
        foreach (var entry in _service.Load().OrderByDescending(e => e.ObservedAtLocal))
        {
            Entries.Add(entry);
        }

        RefreshSummary();
    }

    /// <summary>Se dispara al añadir o borrar entradas.</summary>
    public event Action? Changed;

    /// <summary>Entradas, de la más reciente a la más antigua.</summary>
    public ObservableCollection<ObservationLogEntry> Entries { get; } = [];

    public IReadOnlySet<string> ObservedIds => _observedIds;

    [ObservableProperty] private int observedMessierCount;

    [ObservableProperty] private string? statusMessage;

    public string MessierProgressText => $"{ObservedMessierCount} / {MessierTotal}";

    public double MessierProgressPercent => 100.0 * ObservedMessierCount / MessierTotal;

    [GeneratedRegex(@"^M\d+$")]
    private static partial Regex MessierId();

    public bool HasBeenObserved(string objectId) => _observedIds.Contains(objectId);

    /// <summary>Borrador de una entrada nueva para <paramref name="objectId"/>, con el equipo de la última.</summary>
    public LogEntryDraftViewModel CreateDraft(string objectId, string objectName, DateTime observedAtLocal, string location) =>
        new(objectId, objectName, observedAtLocal, location, Entries.FirstOrDefault()?.Equipment ?? "");

    /// <summary>Guarda el borrador como entrada; false si no es válido o no se pudo guardar.</summary>
    public bool Add(LogEntryDraftViewModel draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        draft.Validate();
        if (draft.HasErrors || draft.ObservedAtLocal is not { } observedAt)
        {
            return false;
        }

        var entry = new ObservationLogEntry(
            Guid.NewGuid(),
            draft.ObjectId,
            draft.ObjectName,
            observedAt,
            draft.Location.Trim(),
            draft.Equipment.Trim(),
            draft.Seeing,
            draft.Transparency,
            draft.Rating,
            draft.Notes.Trim());

        // Se mantiene el orden de más reciente a más antigua.
        var index = 0;
        while (index < Entries.Count && Entries[index].ObservedAtLocal > entry.ObservedAtLocal)
        {
            index++;
        }

        Entries.Insert(index, entry);
        if (!Save())
        {
            Entries.Remove(entry);
            return false;
        }

        StatusMessage = $"Observación de {entry.ObjectId} registrada.";
        RefreshSummary();
        return true;
    }

    [RelayCommand]
    private void Delete(ObservationLogEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        var index = Entries.IndexOf(entry);
        if (index < 0)
        {
            return;
        }

        Entries.RemoveAt(index);
        if (!Save())
        {
            Entries.Insert(index, entry);
            return;
        }

        StatusMessage = $"Observación de {entry.ObjectId} eliminada.";
        RefreshSummary();
    }

    public string BuildCsv(CultureInfo culture)
    {
        var csv = new CsvBuilder(culture).AddRow(["Fecha", "Hora", "ID", "Nombre", "Lugar", "Equipo",
            "Seeing (1-5)", "Transparencia (1-5)", "Valoración (1-5)", "Notas"]);
        foreach (var entry in Entries.OrderBy(e => e.ObservedAtLocal))
        {
            csv.AddRow([
                entry.ObservedAtLocal.ToString("yyyy-MM-dd", culture),
                entry.ObservedAtLocal.ToString("HH:mm", culture),
                entry.ObjectId,
                entry.ObjectName,
                entry.Location,
                entry.Equipment,
                entry.Seeing.ToString(culture),
                entry.Transparency.ToString(culture),
                entry.Rating.ToString(culture),
                entry.Notes
            ]);
        }

        return csv.ToString();
    }

    /// <summary>Exporta la bitácora a CSV (UTF-8 con BOM para que Excel respete los acentos).</summary>
    public bool Export(string filePath)
    {
        try
        {
            File.WriteAllText(filePath, BuildCsv(CultureInfo.CurrentCulture), new UTF8Encoding(true));
            StatusMessage = $"Bitácora exportada a {Path.GetFileName(filePath)}.";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error("No se pudo exportar la bitácora.", ex);
            StatusMessage = $"No se pudo exportar la bitácora: {ex.Message}";
            return false;
        }
    }

    private bool Save()
    {
        try
        {
            _service.Save(Entries);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error("No se pudo guardar la bitácora.", ex);
            StatusMessage = $"No se pudo guardar la bitácora: {ex.Message}";
            return false;
        }
    }

    private void RefreshSummary()
    {
        _observedIds = Entries.Select(e => e.ObjectId).ToHashSet(StringComparer.Ordinal);
        ObservedMessierCount = _observedIds.Count(id => MessierId().IsMatch(id));
        OnPropertyChanged(nameof(ObservedIds));
        OnPropertyChanged(nameof(MessierProgressText));
        OnPropertyChanged(nameof(MessierProgressPercent));
        Changed?.Invoke();
    }
}

/// <summary>Datos de la ventana "Registrar observación".</summary>
public sealed partial class LogEntryDraftViewModel : ObservableValidator
{
    public LogEntryDraftViewModel(
        string objectId, string objectName, DateTime observedAtLocal, string location, string equipment)
    {
        ObjectId = objectId;
        ObjectName = objectName;
        observedDate = observedAtLocal.Date;
        observedTime = observedAtLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
        this.location = location;
        this.equipment = equipment;
    }

    public string ObjectId { get; }
    public string ObjectName { get; }
    public string Title => $"{ObjectId} — {ObjectName}";

    /// <summary>Escala 1-5 para seeing, transparencia y valoración.</summary>
    public IReadOnlyList<int> Scale { get; } = [1, 2, 3, 4, 5];

    [ObservableProperty] private DateTime observedDate;

    [ObservableProperty, NotifyDataErrorInfo]
    [RegularExpression(@"^([01]?\d|2[0-3]):[0-5]\d$", ErrorMessage = "Escribe la hora como HH:mm (por ejemplo 22:30).")]
    private string observedTime;

    [ObservableProperty] private string location;
    [ObservableProperty] private string equipment;
    [ObservableProperty] private int seeing = 3;
    [ObservableProperty] private int transparency = 3;
    [ObservableProperty] private int rating = 3;
    [ObservableProperty] private string notes = "";

    /// <summary>Fecha y hora local de la observación, o null si la hora no es válida.</summary>
    public DateTime? ObservedAtLocal =>
        TimeSpan.TryParseExact(ObservedTime, [@"h\:mm", @"hh\:mm"], CultureInfo.InvariantCulture, out var time)
            && time < TimeSpan.FromDays(1)
            ? ObservedDate.Date + time
            : null;

    public void Validate() => ValidateAllProperties();
}
