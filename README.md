# AnalisisOpiniones.Extraction

Worker Service encargado de la **extracción (E)** del proceso ETL del proyecto `AnalisisOpiniones`.

## Destino y compatibilidad

- Target actual: `net8.0`.
- El equipo puede tener SDK 9/10; con esos SDK se puede compilar `net8.0` sin problema.
- Si se necesita ejecutar con otro SDK, cambiar `<TargetFramework>` en el `.csproj` a `net9.0` o `net10.0`. El código no usa API exclusivas de .NET 8, salvo el destino declarado.

## Fuentes configuradas

| Fuente | Clase | Tecnología | Capa de configuración |
| --- | --- | --- | --- |
| CSV de encuestas internas | `CsvExtractor` | `CsvHelper` 34.1.0 | `Extraction:Csv` |
| Reseñas web | `DatabaseExtractor` | `Microsoft.Data.SqlClient` | `Extraction:Database` |
| Comentarios sociales | `ApiExtractor` | `IHttpClientFactory` + `HttpClient` | `Extraction:Api` |

Tabla destino de la base de datos relacional: `dbo.Resenas_Web` de `SistemaAnalisisOpiniones`.

## Ejecución

Desde la carpeta de la solución:

```powershell
dotnet run --project "AnalisisOpiniones.Extraction\AnalisisOpiniones.Extraction.csproj"
```

En `Development`, `appsettings.Development.json` deja la API en modo `Mock` para que el proyecto se pueda ejecutar sin un servicio externo. En `Production`, la configuración base usa `Modo = "Http"` y la URL `http://localhost:5000/api/comentarios-sociales`.

Para apuntar a la API real:

```json
"Api": {
  "Modo": "Http",
  "Habilitado": true,
  "Url": "https://servidor-real/api/comentarios-sociales",
  "Token": ""
}
```

## Staging

Los datos se guardan como JSON en la carpeta indicada por `Extraction:Staging:Carpeta` (por defecto, `staging` dentro de `bin/Debug/net8.0`). Cada archivo lleva timestamp y GUID para evitar sobrescrituras. La escritura se hace primero a `.tmp` y luego se renombra a `.json`.

## Seguridad de credenciales

No dejar contraseñas ni tokens dentro del repositorio. Para el entorno de desarrollo local se puede usar User Secrets:

```powershell
dotnet user-secrets set "Extraction:Database:ConnectionString" "Server=...;User Id=...;Password=...;"
dotnet user-secrets set "Extraction:Api:Token" "TU_TOKEN"
```

En producción se deben usar variables de entorno o un proveedor de secretos:

```powershell
$env:Extraction__Database__ConnectionString = "Server=...;..."
$env:Extraction__Api__Token = "TU_TOKEN"
```

La configuración sensible se enlaza mediante `IOptions<ExtractionOptions>` y se valida al iniciar con `ValidateOnStart()`.

## Estructura

```text
Abstractions/   IExtractor, IStagingWriter, ExtractionResult
Options/        ExtractionOptions y opciones por fuente
Extractors/     CsvExtractor, DatabaseExtractor, ApiExtractor
Services/       ExtractionWorker, StagingWriter
Program.cs      composición de dependencias y configuración
```

La separación anterior permite cambiar una fuente sin afectar el orquestador, agregar una nueva implementando `IExtractor` y mantener la configuración fuera del código.
