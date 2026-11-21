using System.Data;
using System.Diagnostics;
using AnalisisOpiniones.Extraction.Abstractions;
using AnalisisOpiniones.Extraction.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AnalisisOpiniones.Extraction.Extractors;

public sealed class DatabaseExtractor : IExtractor
{
    private readonly DatabaseOptions _opciones;
    private readonly IStagingWriter _staging;
    private readonly ILogger<DatabaseExtractor> _logger;

    public string Nombre => "BD_Resenas";

    public DatabaseExtractor(
        IOptions<ExtractionOptions> opciones,
        IStagingWriter staging,
        ILogger<DatabaseExtractor> logger)
    {
        _opciones = opciones.Value.Database;
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

            if (string.IsNullOrWhiteSpace(_opciones.ConnectionString))
                throw new InvalidOperationException("La cadena de conexión de SQL Server no está configurada.");

            if (string.IsNullOrWhiteSpace(_opciones.Consulta))
                throw new InvalidOperationException("La consulta de extracción no está configurada.");

            var registros = new List<IDictionary<string, object?>>();

            await using var conexion = new SqlConnection(_opciones.ConnectionString);
            await conexion.OpenAsync(cancellationToken);

            await using var comando = new SqlCommand(_opciones.Consulta, conexion)
            {
                CommandType = CommandType.Text,
                CommandTimeout = Math.Max(1, _opciones.CommandTimeoutSegundos)
            };

            await using var lector = await comando.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess,
                cancellationToken);

            while (await lector.ReadAsync(cancellationToken))
            {
                var fila = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                for (var i = 0; i < lector.FieldCount; i++)
                    fila[lector.GetName(i)] = lector.IsDBNull(i) ? null : lector.GetValue(i);

                registros.Add(fila);
            }

            var rutaStaging = await _staging.GuardarAsync(Nombre, registros, cancellationToken);
            reloj.Stop();

            _logger.LogInformation(
                "BD extraída: {Registros} registros en {Tiempo} ms. Staging: {Ruta}",
                registros.Count,
                reloj.ElapsedMilliseconds,
                rutaStaging);

            return ExtractionResult.Ok(Nombre, registros.Count, rutaStaging, reloj.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            reloj.Stop();
            _logger.LogError(ex, "Error al extraer reseñas desde SQL Server.");
            return ExtractionResult.Fallo(Nombre, ex.Message, reloj.ElapsedMilliseconds);
        }
    }
}
