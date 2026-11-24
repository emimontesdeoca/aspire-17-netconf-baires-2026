# Aspire 17 llegó: qué es, para qué sirve y qué trae de nuevo

Material de la sesión para [.NET Conf 2026 - .NET Baires](https://sessionize.com/net-conf-2026-net-baires/).

La charla empieza con los fundamentos de Aspire y termina con las novedades de la versión 17.0.
La demo es intencionalmente pequeña: nace desde una carpeta vacía y evoluciona en cuatro pasos.

```text
aspire-17-netconf-baires-2026/
├── demo/
│   └── GUIA.md              # guion manual, paso a paso
├── slides/
│   ├── deck.md              # deck MARP, en español, 25' + Q&A
│   └── assets/
└── src/
    ├── AspireDemo.sln       # estado final y ejecutable
    ├── AspireDemo.AppHost/  # topología y Docker Compose
    ├── AspireDemo.ServiceDefaults/
    ├── Api/
    └── SubApi/
```

## Qué muestra la demo

```text
HTTP 8080              HTTP 8081
   api  ─────────────▶  sub-api  ─────────────▶ PostgreSQL
    │
    └──────────────────────────────────────────▶ Redis
```

1. `api` llama a `sub-api` usando service discovery de Aspire.
2. `sub-api` consulta `PostgreSQL`.
3. `api` guarda esa respuesta en `Redis` durante un minuto.
4. Un entorno Docker Compose publica la misma topología.

La primera llamada atraviesa PostgreSQL y guarda el texto en Redis. La segunda devuelve el mismo
texto directamente desde Redis; la diferencia se ve comparando las dos trazas en el Dashboard.

## Requisitos

- .NET 10 SDK
- Aspire CLI 17.0
- Docker o Podman en ejecución para PostgreSQL, Redis y Docker Compose
- Bash para ejecutar los comandos del guion
- `curl` para las pruebas manuales

Comprueba el entorno:

```bash
dotnet --version
aspire --version
aspire doctor
docker version
```

El AppHost apunta a .NET 10. Aspire 17 exige .NET 10 o superior para el AppHost, pero las aplicaciones
orquestadas pueden usar otros frameworks compatibles.

## Ejecutar el estado final

```bash
cd src
dotnet build AspireDemo.sln
aspire run
```

Con Aspire ejecutándose, abre otra terminal:

```bash
curl -s http://localhost:8081/database-info
curl -s http://localhost:8080/database-info
curl -s http://localhost:8080/database-info
```

También puedes usar [Api.http](src/Api/Api.http) y [SubApi.http](src/SubApi/SubApi.http).

En el Dashboard:

1. Abre **Recursos** y encuentra `api`, `sub-api`, `postgres`, `aspiredemo` y `cache`.
2. Cambia a la vista de grafo para ver las referencias.
3. Ejecuta dos peticiones a `api` y compara las trazas.
4. Abre PostgreSQL y Redis para revisar consola, salud y telemetría.

## Crear la demo desde cero, en vivo

Abre [demo/GUIA.md](demo/GUIA.md) al lado de la terminal. Es un guion manual con cuatro pasos:

1. Crear la solución y conectar dos APIs.
2. Añadir PostgreSQL a `sub-api`.
3. Añadir Redis a `api`.
4. Publicar Docker Compose.

El documento incluye cada comando, el contenido completo de `Program.cs` y `AppHost.cs`, las
peticiones de prueba, lo que se debe señalar en el Dashboard y una frase corta para cada transición.

## Publicar con Docker Compose

La solución final ya contiene `AddDockerComposeEnvironment("compose")` y
`Aspire.Hosting.Docker`. Generar artefactos:

```bash
cd src
aspire publish -o aspire-output
```

Desplegarlos:

```bash
aspire deploy
```

Retirarlos:

```bash
aspire destroy
```

Los artefactos `src/aspire-output/` quedan fuera de Git.

## Paquetes principales

| Proyecto | Paquetes de Aspire 17 |
|---|---|
| `AspireDemo.AppHost` | `Aspire.Hosting.AppHost`, `Aspire.Hosting.Dotnet`, PostgreSQL, Redis y Docker |
| `Api` | `Aspire.StackExchange.Redis` |
| `SubApi` | `Aspire.Npgsql` |
| Service Defaults | service discovery, resiliencia, health checks y OpenTelemetry |

El AppHost usa `<UseAppHost>false</UseAppHost>`. Esta opción hace que .NET inicie la DLL administrada
en lugar de un ejecutable nativo generado y evita bloqueos en equipos Windows con políticas de
ejecución más estrictas. No cambia el modelo de recursos de Aspire.

## Novedades de Aspire 17 cubiertas en las slides

- Dashboard centrado en recursos, All resources, Terminals y exportación Mermaid.
- Autocompletado de shell, tray companion, secretos por entorno y sources para equipos.
- Go AppHosts en preview.
- Attach debugger y herramientas para coding agents desde VS Code.
- Publicación a Kubernetes con paquetes estables.
- Integraciones de Entra ID, API Management y Azure AI.
- Parámetros opcionales, browser origins y más APIs estables.
- Cambios de migración: .NET 10+, `AddDotnetProject` y retirada progresiva de
  `Aspire.AppHost.Sdk`.

Fuente principal:
[What's new in Aspire 17.0](https://aspire.dev/whats-new/aspire-17/).

## Slides MARP

```bash
cd slides
npx @marp-team/marp-cli@4.5.1 deck.md -o deck.html --allow-local-files --html
npx @marp-team/marp-cli@4.5.1 deck.md --pdf -o deck.pdf --allow-local-files
npx @marp-team/marp-cli@4.5.1 -s .
```

Las notas incluyen los tiempos de una charla de 25 minutos y 5 minutos de preguntas. Durante HTML,
pulsa `P` para abrir la vista de presentador.

## Problemas habituales

| Síntoma | Solución |
|---|---|
| `aspire: command not found` | Instala Aspire CLI 17 y abre una terminal nueva |
| PostgreSQL o Redis no arrancan | Ejecuta `docker version` y `aspire doctor` |
| El puerto 8080/8081 está ocupado | Libera el puerto o cambia `WithHttpEndpoint(port: ...)` |
| La segunda traza sigue atravesando PostgreSQL | Haz las llamadas con menos de un minuto entre ellas |
| No aparece la traza de la segunda llamada | Refresca Traces y filtra por `api`; las health checks no generan trazas |
| `aspire publish` falla al iniciar el AppHost en Windows | Mantén `<UseAppHost>false</UseAppHost>` en el AppHost |
| No aparecen contenedores en el Dashboard | Comprueba el runtime detectado con `aspire doctor` |

## Referencias

- [What's new in Aspire 17.0](https://aspire.dev/whats-new/aspire-17/)
- [Get started with PostgreSQL integrations](https://aspire.dev/integrations/databases/postgres/postgres-get-started/)
- [Get started with Redis integrations](https://aspire.dev/integrations/caching/redis/redis-get-started/)
- [Deploy to Docker Compose](https://aspire.dev/deployment/docker-compose/)
- [Call for Speakers - .NET Conf 2026 - .NET Baires](https://sessionize.com/net-conf-2026-net-baires/)

La demo no crea tablas ni requiere credenciales externas. Aspire inicia los recursos locales y
genera la configuración de conexión. PostgreSQL y Redis son efímeros para que cada ensayo empiece
desde cero.
