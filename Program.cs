using System.Net.Http.Headers;
using AnalisisOpiniones.Extraction.Abstractions;
using AnalisisOpiniones.Extraction.Extractors;
using AnalisisOpiniones.Extraction.Options;
using AnalisisOpiniones.Extraction.Services;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<ExtractionOptions>()
    .Bind(builder.Configuration.GetSection("Extraction"))
    .ValidateOnStart();

builder.Services.AddHttpClient("ApiExtractor", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddSingleton<IStagingWriter>(sp =>
{
    var opciones = sp.GetRequiredService<IOptions<ExtractionOptions>>().Value.Staging;
    var carpeta = ResolverRuta(opciones.Carpeta, AppContext.BaseDirectory);
    var logger = sp.GetRequiredService<ILogger<StagingWriter>>();
    return new StagingWriter(carpeta, logger);
});

builder.Services.AddSingleton<IExtractor, CsvExtractor>();
builder.Services.AddSingleton<IExtractor, DatabaseExtractor>();
builder.Services.AddSingleton<IExtractor, ApiExtractor>();

builder.Services.AddHostedService<ExtractionWorker>();

var host = builder.Build();
await host.RunAsync();

static string ResolverRuta(string ruta, string directorioBase)
{
    var valor = string.IsNullOrWhiteSpace(ruta) ? "staging" : ruta;
    return Path.IsPathRooted(valor)
        ? Path.GetFullPath(valor)
        : Path.GetFullPath(Path.Combine(directorioBase, valor));
}
