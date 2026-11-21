namespace AnalisisOpiniones.Extraction.Options;

public sealed class ExtractionOptions
{
    public CsvOptions Csv { get; set; } = new();
    public DatabaseOptions Database { get; set; } = new();
    public ApiOptions Api { get; set; } = new();
    public StagingOptions Staging { get; set; } = new();
}

public sealed class CsvOptions
{
    public bool Habilitado { get; set; } = true;
    public string RutaArchivo { get; set; } = string.Empty;
    public string Delimitador { get; set; } = ",";
}

public sealed class DatabaseOptions
{
    public bool Habilitado { get; set; } = true;
    public string ConnectionString { get; set; } = string.Empty;
    public string Consulta { get; set; } = "SELECT * FROM Resenas_Web";
    public int CommandTimeoutSegundos { get; set; } = 120;
}

public sealed class ApiOptions
{
    // Http = llama a la URL configurada. Mock = genera comentarios de ejemplo
    // para probar el flujo cuando todavía no existe una API real.
    public string Modo { get; set; } = "Http";
    public bool Habilitado { get; set; } = true;
    public string Url { get; set; } = string.Empty;
    public string? Token { get; set; }
    public int TimeoutSegundos { get; set; } = 60;
}

public sealed class StagingOptions
{
    public string Carpeta { get; set; } = "staging";
}
