using System.Text.Encodings.Web;
using System.Text.Json;
using AnalisisOpiniones.Extraction.Abstractions;

namespace AnalisisOpiniones.Extraction.Services;

public sealed class StagingWriter : IStagingWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _carpeta;
    private readonly ILogger<StagingWriter> _logger;

    public StagingWriter(string carpeta, ILogger<StagingWriter> logger)
    {
        _carpeta = carpeta;
        _logger = logger;
    }

    public async Task<string> GuardarAsync(
        string fuente,
        IEnumerable<IDictionary<string, object?>> registros,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_carpeta);

        var lista = registros as IReadOnlyCollection<IDictionary<string, object?>>
                    ?? registros.ToList();

        var marcaTiempo = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var nombre = $"{Sanitizar(fuente)}_{marcaTiempo}_{Guid.NewGuid():N}.json";
        var ruta = Path.Combine(_carpeta, nombre);
        var rutaTemporal = ruta + ".tmp";

        await using (var flujo = File.Create(rutaTemporal))
        {
            await JsonSerializer.SerializeAsync(flujo, lista, JsonOptions, cancellationToken);
            await flujo.FlushAsync(cancellationToken);
        }

        File.Move(rutaTemporal, ruta, overwrite: true);

        var rutaCompleta = Path.GetFullPath(ruta);
        _logger.LogInformation(
            "Staging guardado: {Ruta} ({Registros} registros)",
            rutaCompleta,
            lista.Count);

        return rutaCompleta;
    }

    private static string Sanitizar(string texto)
    {
        var permitidos = texto.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray();
        return permitidos.Length == 0 ? "fuente" : new string(permitidos);
    }
}
