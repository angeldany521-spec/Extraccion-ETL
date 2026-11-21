namespace AnalisisOpiniones.Extraction.Abstractions;

public interface IStagingWriter
{
    Task<string> GuardarAsync(
        string fuente,
        IEnumerable<IDictionary<string, object?>> registros,
        CancellationToken cancellationToken = default);
}
