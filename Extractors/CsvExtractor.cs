using System.Diagnostics;
using AnalisisOpiniones.Extraction.Abstractions;
using AnalisisOpiniones.Extraction.Options;
using CsvHelper;
using Microsoft.Extensions.Options;

namespace AnalisisOpiniones.Extraction.Extractors;

public sealed class CsvExtractor : IExtractor
{
    private readonly CsvOptions _opciones;
    private readonly IStagingWriter _staging;
    private readonly ILogger<CsvExtractor> _logger;

    public string Nombre => "CSV_Encuestas";

    public CsvExtractor(
        IOptions<ExtractionOptions> opciones,
        IStagingWriter staging,
        ILogger<CsvExtractor> logger)
    {
        _opciones = opciones.Value.Csv;
        _staging = staging;
        _logger = logger;
    }

    public async Task<ExtractionResult> ExtractAsync(CancellationToken cancellationToken = default)
    {
        var reloj = Stopwatch.StartNew();

        try
        {
            if (!_opciones.Habilitado)
            {
                _logger.LogInformation("Extractor {Fuente} deshabilitado.", Nombre);
                return ExtractionResult.Deshabilitado(Nombre);
            }

            var ruta = ResolverRutaCSV(_opciones.RutaArchivo);
            if (!File.Exists(ruta))
                throw new FileNotFoundException($"No se encontró el archivo CSV: {ruta}");

            var separador = string.IsNullOrWhiteSpace(_opciones.Delimitador)
                ? ','
                : _opciones.Delimitador[0];

            var opcionesLectura = new CsvReaderOptions
            {
                Separator = separador,
                SkipEmptyLines = true
            };

            using var csv = CsvReader.FromFile(ruta, opcionesLectura);

            var registros = new List<IDictionary<string, object?>>();

            await foreach (var fila in csv.GetRecordsAsync<CsvDynamicRecord>(cancellationToken))
            {
                var diccionario = fila.ToDictionary(
                    par => par.Key,
                    par => (object?)par.Value,
                    StringComparer.OrdinalIgnoreCase);

                registros.Add(diccionario);
            }

            var rutaStaging = await _staging.GuardarAsync(Nombre, registros, cancellationToken);
            reloj.Stop();

            _logger.LogInformation(
                "CSV extraído: {Registros} registros en {Tiempo} ms. Staging: {Ruta}",
                registros.Count,
                reloj.ElapsedMilliseconds,
                rutaStaging);

            return ExtractionResult.Ok(Nombre, registros.Count, rutaStaging, reloj.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            reloj.Stop();
            _logger.LogError(ex, "Error al extraer el CSV de encuestas internas.");
            return ExtractionResult.Fallo(Nombre, ex.Message, reloj.ElapsedMilliseconds);
        }
    }

    private static string ResolverRutaCSV(string ruta)
    {
        var valor = string.IsNullOrWhiteSpace(ruta)
            ? Path.Combine("Datasets_Originales", "surveys_part1.csv")
            : ruta;

        return Path.IsPathRooted(valor)
            ? Path.GetFullPath(valor)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, valor));
    }
}
