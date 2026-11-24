# Guion manual de la demo

Este documento es para tenerlo abierto al lado de la pantalla. No hay scripts: cada comando y cada
cambio se hace a mano para que el público vea el proceso.

## Antes de empezar

Abre:

- una terminal Bash a la izquierda;
- otra terminal para `curl`;
- el editor en el proyecto;
- Docker Desktop o el runtime de contenedores.

Comprueba:

```bash
dotnet --version
aspire --version
aspire doctor
docker version
```

Antes del evento, deja instalada la plantilla correcta:

```bash
dotnet new install Aspire.ProjectTemplates@17.0.0 --force
```

Tiempos:

- 0–3 min: crear los proyectos y conectar las APIs.
- 3–7 min: añadir PostgreSQL.
- 7–11 min: añadir Redis.
- Opcional: publicar Docker Compose.

---

## 1. Crear la solución y las dos APIs

Desde una carpeta de demos vacía:

```bash
dotnet new aspire -n AspireDemo -o AspireDemoLive -f net10.0
cd AspireDemoLive

dotnet new webapi -n Api -o Api -f net10.0 --no-openapi
dotnet new webapi -n SubApi -o SubApi -f net10.0 --no-openapi

dotnet sln AspireDemo.sln add Api/Api.csproj SubApi/SubApi.csproj

dotnet add Api/Api.csproj reference \
  AspireDemo.ServiceDefaults/AspireDemo.ServiceDefaults.csproj

dotnet add SubApi/SubApi.csproj reference \
  AspireDemo.ServiceDefaults/AspireDemo.ServiceDefaults.csproj
```

> Lo que cuento: el AppHost modela la aplicación; ServiceDefaults aporta discovery, resiliencia,
> salud y OpenTelemetry a ambas APIs.

### Windows: ajuste opcional

Si el equipo muestra `Access is denied` al iniciar el AppHost, añade esta propiedad debajo de
`<OutputType>Exe</OutputType>` en `AspireDemo.AppHost/AspireDemo.AppHost.csproj`:

```xml
<UseAppHost>false</UseAppHost>
```

---

## 2. Conectar las APIs sin base de datos

Sustituye `AspireDemo.AppHost/AppHost.cs`:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var subApi = builder.AddDotnetProject("sub-api", "../SubApi/SubApi.csproj")
    .WithHttpEndpoint(port: 8081, targetPort: 18081)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.AddDotnetProject("api", "../Api/Api.csproj")
    .WithReference(subApi)
    .WaitFor(subApi)
    .WithHttpEndpoint(port: 8080, targetPort: 18080)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
```

Sustituye `SubApi/Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapGet("/database-info", () => "Hello from SubApi");

app.Run();
```

Sustituye `Api/Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddHttpClient("sub-api", client =>
    client.BaseAddress = new Uri("https+http://sub-api"));

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapGet("/database-info", async (
    IHttpClientFactory clients,
    CancellationToken cancellationToken) =>
    await clients.CreateClient("sub-api")
        .GetStringAsync("/database-info", cancellationToken));

app.Run();
```

Construye y arranca:

```bash
dotnet build AspireDemo.sln
aspire run
```

En la segunda terminal:

```bash
curl -s http://localhost:8081/database-info
curl -s http://localhost:8080/database-info
```

En el Dashboard:

1. Muestra `api` y `sub-api`.
2. Abre el grafo: `api → sub-api`.
3. Abre la traza de la petición a `api`.

> Frase clave: `WithReference(subApi)` conecta los proyectos. En el código no aparece un
> `localhost` de otra API, sino el nombre del recurso.

Detén Aspire:

```text
Ctrl+C
```

---

## 3. Añadir PostgreSQL

Instala los dos lados de la integración:

```bash
dotnet add AspireDemo.AppHost/AspireDemo.AppHost.csproj package \
  Aspire.Hosting.PostgreSQL --version 17.0.0

dotnet add SubApi/SubApi.csproj package \
  Aspire.Npgsql --version 17.0.0
```

Sustituye `AspireDemo.AppHost/AppHost.cs`:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddPostgres("postgres")
    .AddDatabase("aspiredemo");

var subApi = builder.AddDotnetProject("sub-api", "../SubApi/SubApi.csproj")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpEndpoint(port: 8081, targetPort: 18081)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.AddDotnetProject("api", "../Api/Api.csproj")
    .WithReference(subApi)
    .WaitFor(subApi)
    .WithHttpEndpoint(port: 8080, targetPort: 18080)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
```

Sustituye `SubApi/Program.cs`:

```csharp
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("aspiredemo");

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapGet("/database-info", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    await using var command = dataSource.CreateCommand(
        """
        SELECT
            current_database()
            || ' | PostgreSQL '
            || current_setting('server_version')
            || ' | '
            || clock_timestamp()::text
        """);

    return (string?)await command.ExecuteScalarAsync(cancellationToken)
        ?? throw new InvalidOperationException("PostgreSQL no devolvió información.");
});

app.Run();
```

Construye y arranca otra vez:

```bash
dotnet build AspireDemo.sln
aspire run
```

Prueba:

```bash
curl -s http://localhost:8081/database-info
curl -s http://localhost:8080/database-info
```

En el Dashboard:

1. Muestra `postgres` y `aspiredemo`.
2. Abre la traza: `api → sub-api → PostgreSQL`.
3. Explica que solo `sub-api` conoce la base.

> Frase clave: `api` no cambió. Aspire encapsuló la conexión a PostgreSQL detrás de `sub-api`.

Detén Aspire con `Ctrl+C`.

---

## 4. Añadir Redis a la primera API

Instala la integración de hosting y el cliente:

```bash
dotnet add AspireDemo.AppHost/AspireDemo.AppHost.csproj package \
  Aspire.Hosting.Redis --version 17.0.0

dotnet add Api/Api.csproj package \
  Aspire.StackExchange.Redis --version 17.0.0
```

Sustituye `AspireDemo.AppHost/AppHost.cs`:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddPostgres("postgres")
    .AddDatabase("aspiredemo");

var cache = builder.AddRedis("cache");

var subApi = builder.AddDotnetProject("sub-api", "../SubApi/SubApi.csproj")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpEndpoint(port: 8081, targetPort: 18081)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.AddDotnetProject("api", "../Api/Api.csproj")
    .WithReference(subApi)
    .WithReference(cache)
    .WaitFor(subApi)
    .WaitFor(cache)
    .WithHttpEndpoint(port: 8080, targetPort: 18080)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
```

Sustituye `Api/Program.cs`:

```csharp
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddRedisClient("cache");

builder.Services.AddHttpClient("sub-api", client =>
    client.BaseAddress = new Uri("https+http://sub-api"));

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapGet("/database-info", async (
    IHttpClientFactory clients,
    IConnectionMultiplexer redis,
    CancellationToken cancellationToken) =>
{
    const string key = "demo:database-info";
    var cache = redis.GetDatabase();
    var cached = await cache.StringGetAsync(key);

    if (cached.HasValue)
    {
        return cached.ToString();
    }

    var value = await clients.CreateClient("sub-api")
        .GetStringAsync("/database-info", cancellationToken);

    await cache.StringSetAsync(key, value, TimeSpan.FromMinutes(1));

    return value;
});

app.Run();
```

Construye y arranca:

```bash
dotnet build AspireDemo.sln
aspire run
```

Ejecuta estas dos líneas con poca separación:

```bash
curl -s http://localhost:8080/database-info
curl -s http://localhost:8080/database-info
```

En el Dashboard:

1. Compara las dos trazas.
2. En la primera aparece `sub-api` y PostgreSQL.
3. En la segunda, `api` termina en Redis.

> Frase clave: añadimos caché sin cambiar `sub-api` ni PostgreSQL. Solo ampliamos el modelo del
> AppHost y el cliente que necesita usar Redis.

Detén Aspire con `Ctrl+C`.

---

## 5. Publicar Docker Compose

Opcional si queda tiempo:

```bash
dotnet add AspireDemo.AppHost/AspireDemo.AppHost.csproj package \
  Aspire.Hosting.Docker --version 17.0.0
```

Añade una línea después de crear el builder en `AppHost.cs`:

```csharp
builder.AddDockerComposeEnvironment("compose");
```

Genera los artefactos:

```bash
aspire publish -o aspire-output
```

Abre:

```text
aspire-output/docker-compose.yaml
```

> Lo que cuento: la topología no se reescribe para desplegar. Aspire usa el mismo AppHost para
> generar Docker Compose.

Para desplegar de verdad:

```bash
aspire deploy
```

Para retirar:

```bash
aspire destroy
```

---

## Si algo falla

- Si se quedan imágenes pendientes, usa la solución final de `src/`.
- Si el puerto está ocupado, muestra primero la solución final y explica el cambio.
- Si Redis devuelve una respuesta vieja, espera un minuto o reinicia el recurso `cache`.
- Si Windows bloquea el AppHost, añade `<UseAppHost>false</UseAppHost>` al `.csproj`.

## Cierre

> Aspire no reemplaza ASP.NET Core, PostgreSQL ni Redis. Pone sus relaciones, configuración,
> arranque, salud, telemetría y publicación en un único modelo ejecutable.
