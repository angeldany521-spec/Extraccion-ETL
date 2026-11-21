namespace AnalisisOpiniones.Extraction.Abstractions;

public interface IExtractor
{
    string Nombre { get; }

    Task<ExtractionResult> ExtractAsync(CancellationToken cancellationToken = default);
}

public sealed class ExtractionResult
{
    public string Fuente { get; init; } = string.Empty;
    public bool Exitoso { get; init; }
    public int Registros { get; init; }
    public string? RutaStaging { get; init; }
    public long TiempoMs { get; init; }
    public string? Error { get; init; }

    public static ExtractionResult Ok(string fuente, int registros, string? rutaStaging, long tiempoMs) => new()
    {
        Fuente = fuente,
        Exitoso = true,
        Registros = registros,
        RutaStaging = rutaStaging,
        TiempoMs = tiempoMs
    };

    public static ExtractionResult Fallo(string fuente, string error, long tiempoMs) => new()
    {
        Fuente = fuente,
        Exitoso = false,
        Error = error,
        TiempoMs = tiempoMs
    };

    public static ExtractionResult Deshabilitado(string fuente) => new()
    {
        Fuente = fuente,
        Exitoso = true,
        Registros = 0,
        TiempoMs = 0
    };
}
