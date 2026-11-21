using System.Diagnostics;
using AnalisisOpiniones.Extraction.Abstractions;

namespace AnalisisOpiniones.Extraction.Services;

public sealed class ExtractionWorker : BackgroundService
{
    private readonly IEnumerable<IExtractor> _extractores;
    private readonly ILogger<ExtractionWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public ExtractionWorker(
        IEnumerable<IExtractor> extractores,
        ILogger<ExtractionWorker> logger,
        IHostApplicationLifetime lifetime)
    {
        _extractores = extractores;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("=== Inicio del proceso de extracción ETL ===");

        var reloj = Stopwatch.StartNew();

        var tareas = _extractores
            .Select(extractor => EjecutarExtractorAsync(extractor, stoppingToken))
            .ToArray();

        var resultados = await Task.WhenAll(tareas);

        reloj.Stop();

        foreach (var resultado in resultados)
        {
            if (resultado.Exitoso)
            {
                _logger.LogInformation(
                    "[{Fuente}] OK | {Registros} registros | {Tiempo} ms | {Ruta}",
                    resultado.Fuente,
                    resultado.Registros,
                    resultado.TiempoMs,
                    resultado.RutaStaging);
            }
            else
            {
                _logger.LogError(
                    "[{Fuente}] ERROR | {Tiempo} ms | {Error}",
                    resultado.Fuente,
                    resultado.TiempoMs,
                    resultado.Error);
            }
        }

        var exitosos = resultados.Count(r => r.Exitoso);
        var totalRegistros = resultados.Sum(r => r.Registros);

        _logger.LogInformation(
            "=== Extracción finalizada | Fuentes OK: {Ok}/{Total} | Registros: {Registros} | Tiempo total: {Tiempo} ms ===",
            exitosos,
            resultados.Length,
            totalRegistros,
            reloj.ElapsedMilliseconds);

        Environment.ExitCode = exitosos == resultados.Length ? 0 : 1;
        _lifetime.StopApplication();
    }

    private async Task<ExtractionResult> EjecutarExtractorAsync(
        IExtractor extractor,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Iniciando extracción: {Fuente}", extractor.Nombre);

        try
        {
            return await extractor.ExtractAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo no controlado en {Fuente}", extractor.Nombre);
            return ExtractionResult.Fallo(extractor.Nombre, ex.Message, 0);
        }
    }
}
