using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using AnalisisOpiniones.Extraction.Abstractions;
using AnalisisOpiniones.Extraction.Options;
using Microsoft.Extensions.Options;

namespace AnalisisOpiniones.Extraction.Extractors;

public sealed class ApiExtractor : IExtractor
{
    private readonly ApiOptions _opciones;
    private readonly IStagingWriter _staging;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiExtractor> _logger;

    public string Nombre => "API_Comentarios";

    public ApiExtractor(
        IOptions<ExtractionOptions> opciones,
        IStagingWriter staging,
        IHttpClientFactory httpClientFactory,
        ILogger<ApiExtractor> logger)
    {
        _opciones = opciones.Value.Api;
        _staging = staging;
        _httpClientFactory = httpClientFactory;
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

            var modo = string.IsNullOrWhiteSpace(_opciones.Modo) ? "Http" : _opciones.Modo.Trim();
            var esMock = modo.Equals("Mock", StringComparison.OrdinalIgnoreCase);

            List<IDictionary<string, object?>> registros;

            if (esMock)
            {
                _logger.LogInformation("API en modo Mock: se generan comentarios de ejemplo.");
                registros = ObtenerComentariosMock();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_opciones.Url))
                    throw new InvalidOperationException("La URL de la API REST no está configurada.");

                var cliente = _httpClientFactory.CreateClient("ApiExtractor");
                cliente.Timeout = TimeSpan.FromSeconds(Math.Max(1, _opciones.TimeoutSegundos));

                if (!string.IsNullOrWhiteSpace(_opciones.Token))
                {
                    cliente.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", _opciones.Token);
                }

                _logger.LogInformation("Consultando API REST: {Url}", _opciones.Url);

                using var respuesta = await cliente.GetAsync(_opciones.Url, cancellationToken);
                respuesta.EnsureSuccessStatusCode();

                var json = await respuesta.Content.ReadAsStringAsync(cancellationToken);
                registros = Deserializar(json);
            }

            var rutaStaging = await _staging.GuardarAsync(Nombre, registros, cancellationToken);
            reloj.Stop();

            _logger.LogInformation(
                "API extraída ({Modo}): {Registros} registros en {Tiempo} ms. Staging: {Ruta}",
                modo,
                registros.Count,
                reloj.ElapsedMilliseconds,
                rutaStaging);

            return ExtractionResult.Ok(Nombre, registros.Count, rutaStaging, reloj.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            reloj.Stop();
            _logger.LogError(ex, "Error al extraer comentarios desde la API REST.");
            return ExtractionResult.Fallo(Nombre, ex.Message, reloj.ElapsedMilliseconds);
        }
    }

    private static List<IDictionary<string, object?>> ObtenerComentariosMock() =>
    [
        new Dictionary<string, object?>
        {
            ["idComentario"] = "SOC-0001",
            ["idCliente"] = "8537",
            ["idProducto"] = "366",
            ["fecha"] = "2026-10-02",
            ["comentario"] = "El envío llegó antes de lo esperado, pero el empaque venía golpeado.",
            ["fuente"] = "RedSocialA",
            ["likes"] = 12
        },
        new Dictionary<string, object?>
        {
            ["idComentario"] = "SOC-0002",
            ["idCliente"] = "2721",
            ["idProducto"] = "667",
            ["fecha"] = "2026-10-03",
            ["comentario"] = "Buena relación precio/calidad. La batería todavía dura poco.",
            ["fuente"] = "RedSocialB",
            ["likes"] = 7
        },
        new Dictionary<string, object?>
        {
            ["idComentario"] = "SOC-0003",
            ["idCliente"] = "4109",
            ["idProducto"] = "112",
            ["fecha"] = "2026-10-05",
            ["comentario"] = "La atención al cliente respondió rápido y resolvió mi caso.",
            ["fuente"] = "ForoOficial",
            ["likes"] = 21
        }
    ];

    private static List<IDictionary<string, object?>> Deserializar(string json)
    {
        using var documento = JsonDocument.Parse(json);
        var raiz = documento.RootElement;

        if (raiz.ValueKind == JsonValueKind.Array)
            return LeerArreglo(raiz);

        if (raiz.ValueKind == JsonValueKind.Object)
        {
            foreach (var propiedad in raiz.EnumerateObject())
            {
                if (propiedad.Value.ValueKind == JsonValueKind.Array)
                    return LeerArreglo(propiedad.Value);
            }

            return [ConvertirObjeto(raiz)];
        }

        return [];
    }

    private static List<IDictionary<string, object?>> LeerArreglo(JsonElement arreglo)
    {
        var registros = new List<IDictionary<string, object?>>();

        foreach (var elemento in arreglo.EnumerateArray())
        {
            if (elemento.ValueKind == JsonValueKind.Object)
                registros.Add(ConvertirObjeto(elemento));
        }

        return registros;
    }

    private static IDictionary<string, object?> ConvertirObjeto(JsonElement objeto)
    {
        var resultado = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var propiedad in objeto.EnumerateObject())
            resultado[propiedad.Name] = ConvertirValor(propiedad.Value);

        return resultado;
    }

    private static object? ConvertirValor(JsonElement valor) => valor.ValueKind switch
    {
        JsonValueKind.String => valor.GetString(),
        JsonValueKind.Number => valor.TryGetDecimal(out var numero)
            ? numero
            : valor.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Object => ConvertirObjeto(valor),
        JsonValueKind.Array => valor.EnumerateArray().Select(ConvertirValor).ToList(),
        _ => valor.ToString()
    };
}
