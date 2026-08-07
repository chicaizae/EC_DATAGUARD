# EC DATAGUARD — Intelligent Data Security Platform (On-Premise)

Plataforma **On-Premise multitenant de DLP + clasificación de la información** con
capacidades de producto cloud: heartbeat de agentes, canal de mandatos, consola web
central y escalabilidad.

Este repositorio es el **esqueleto end-to-end (fase 1)** descrito en
`docs/01-arquitectura.md`.

## Stack

| Componente | Tecnología |
|---|---|
| Servidor / API / Consola web | .NET 8 (C#) sobre **Linux** (Ubuntu Server) |
| Base de datos central | PostgreSQL 16 (multi-tenant por `tenant_id`) |
| Agentes | Windows 7+ y Linux |
| Despliegue | Docker Compose (Linux), con ruta a Kubernetes |
| Consola | Blazor Server (`src/Server/EcDataguard.Web`) |

## Estructura

```
EC DATAGUARD/
├── docs/                        # Documentación (arquitectura, protocolo, Sophos, roadmap)
├── src/
│   ├── Contracts/               # DTOs de transporte agente-servidor
│   ├── Server/
│   │   ├── EcDataguard.Domain/          # Entidades y enumerados
│   │   ├── EcDataguard.Application/     # Casos de uso, motor de políticas/clasificación
│   │   ├── EcDataguard.Infrastructure/  # EF Core + PostgreSQL, JWT, seed
│   │   ├── EcDataguard.Api/             # API REST
│   │   └── EcDataguard.Web/             # Consola web (Blazor Server)
│   └── Agent/
│       └── EcDataguard.Agent/           # Agente Windows/Linux
├── tests/                        # Pruebas unitarias base
└── deploy/                       # Dockerfile + docker-compose
```

## Puesta en marcha (servidor Linux)

```bash
cd deploy
cp .env.example .env   # editar credenciales y PUBLIC_BASE_URL
docker compose up -d --build
```

Servicios (un solo punto de entrada, el proxy):

- Consola web y API: `http://<host>:8081` → Swagger en `http://<host>:8081/swagger`
- Agentes: `http://<host>:8081/api` (heartbeat en `/agent/...`)
- Los binarios del agente se sirven en `http://<host>:8081/agents/...` (requiere publicarlos antes, ver abajo).

Cuenta inicial (ver `.env`):

- Usuario: `admin@ecodataguard.local`
- Contraseña: `Admin*EcDataguard2026` (cambiar en producción)

## Desarrollo local sin Docker

Para levantar la API en Windows sin PostgreSQL local, use SQLite de desarrollo:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src\Server\EcDataguard.Api\EcDataguard.Api.csproj --urls http://localhost:8080
```

En otra terminal, levante la consola Blazor:

```powershell
dotnet run --project src\Server\EcDataguard.Web\EcDataguard.Web.csproj --urls http://localhost:8081
```

La API crea `data/ecdataguard-dev.db` automáticamente con la cuenta inicial. Este modo es solo para desarrollo; Docker Compose sigue usando PostgreSQL.

## Instalación de un agente

El agente almacena configuración en `%ProgramData%\EcDatagard\Agent` (Windows) o
`/etc/ecdataguard/agent` (Linux).

**Dos variantes de Windows** (elija el SO en Consola → Dispositivos):

| Variante | SO | Framework | Publicación |
|---|---|---|---|
| `EcDataguardAgent7.exe` | Windows 7/8 (x86) | .NET 6 (`net6.0-windows7.0`) | self-contained x86 |
| `EcDataguardAgent.exe` | Windows 10/11 (x64) | .NET 8 | self-contained x64 |
| `ecdataguard-agent` | Linux (x64) | .NET 8 | self-contained x64 |

Publicación de binarios (requiere .NET SDK 8):

```bash
# Linux
bash deploy/agent/build.sh

# Windows
powershell -ExecutionPolicy Bypass -File deploy\agent\build.ps1
```

Genera `deploy/agent/dist/{win7,win10,linux}/` + `manifest.json` (con SHA-256); el
compose monta esa carpeta en la API (`/agents`). Tras registrar un equipo con el SO
elegido, la consola muestra el comando instalador con su token.

El token se emite desde la consola en *Dispositivos* (Registrar equipo + instalador).

## Escalado

- `api` y `web` son **stateless** y se replican tras nginx:
  ```bash
  docker compose up -d --scale api=3 --scale web=3
  ```
- `proxy` (nginx): API con `least_conn`; consola Blazor con **sesiones fijas** (`ip_hash`).
- `redis`: **backplane de SignalR**, réplicas web comparten la sesión Blazor.
- PostgreSQL es el único con estado (volumen `pgdata`). Para escala mayor se añadirá
  PgBouncer + réplica de lectura en fase 2 (ver `docs/04-roadmap.md`).
- En producción publique solo `PUBLIC_HTTP_PORT` (el proxy).

## Documentación

1. `docs/01-arquitectura.md` — arquitectura y modelo multi-tenant
2. `docs/02-protocolo-agente.md` — heartbeat, mandatos, eventos, detección de bases de datos
3. `docs/03-sophos-xdr.md` — coexistencia con Sophos XDR (exclusiones, trust-pack)
4. `docs/04-roadmap.md` — fases y próximos entregables

## Seguridad

- Separación multi-tenant obligatoria (`tenant_id` en todas las tablas).
- Agentes con token propio (JWT por dispositivo), no credenciales de consola.
- Admin trail en cada cambio de la consola; salida SIEM/OCSF preparada.
- No se registran claves/secrets.
