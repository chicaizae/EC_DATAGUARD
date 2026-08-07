# 01 · Arquitectura de EC DATAGUARD (On-Premise multitenant)

## 1. Visión

EC DATAGUARD es una plataforma centralizada de **DLP (Data Loss Prevention) +
clasificación de la información y protección de puertas (IRM)**, operada **On-Premise**
por el proveedor. Un **único centro de mando** (consola web) administra **varias
empresas (tenants)**; el proveedor (superadmin) controla todas las empresas y cada
empresa solo ve su propio alcance.

## 2. Capacidades de "cloud" en On-Premise

- **Heartbeat**: el agente reporta estado cada N segundos; la consola conoce en
  tiempo real qué equipos están vivos, protegidos, degradados o sin protección.
- **Canal de mandatos (command & control)**: la consola envía mandatos (aplicar
  políticas, cambiar config, reiniciar agente, renovar inventario) y el agente los
  recibe en el siguiente heartbeat, los ejecuta y reporta el resultado.
- **Escalabilidad**: servidor stateless en .NET 8 sobre Linux; PostgreSQL 16 como
  repositorio central; particle/particjón por `tenant_id` preparada para escalar.
- **Telemetría por lotes**: los agentes suben eventos en lotes acotados (JSONB).

## 3. Modelo funcional (traducción del manual de preguridad)

| Manual (con soluciones en PDF) | Fase 1 en código |
|---|---|
| DETECT · Dashboard / Insights | Dashboard con widgets agregados; Insights derivados de eventos/políticas |
| RESPOND · destinations, clasificación, políticas | Entidades `Destination`, `Classification`, `Policy`; acciones Allow/Log/Notify/Block/BlockWithOverride |
| ANALYZE · behavior, discovery | Consultas sobre eventos y artefactos (incl. detección de bases de datos) |
| MANAGE · devices, users, reports | Dispositivos + usuarios + licencias + inventario de BD |
| SETTINGS · cuentas, admin trail, SIEM | Roles, Admin trail, integración OCSF/SIEM |

## 4. Vistas de alto nivel

```
+-----------------------+            +----------------------------+
| Agentes empresa A      |            |    Servidor Linux (prod)  |
| (Windows7+, Linux)     |  HTTPS     |  .NET 8 API + Consola web |
+-----------+-----------+----------->|  PostgreSQL 16            |
                                   |  +----------------------------+
+
```

Ver Mermaid en el repositorio (`docs/01.agencia` no; consulte readme).

## 5. Multi-tenant

La columna `tenant_id` existe en todas las tablas de datos. `Tenant` (empresa) tiene
código único, nombre, plan, estado. El superadmin ve todos los tenants; cada admin de
tenant ve solo el suyo. Los tokens de agente embarcan `tenant_id`.

```sql
CREATE TABLE tenants (
    id         UUID PRIMARY KEY,
    codigo     TEXT UNIQUE NOT NULL,
    nombre     TEXT NOT NULL,
    plan       TEXT NOT NULL DEFAULT 'Enterprise',
    activo     BOOLEAN NOT NULL DEFAULT TRUE,
    creado_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

## 6. Contrato agente-servidor

- Hidden heartbeat JSON en `docs/02-protocolo-agente.md`.
- `POST /api/agent/heartbeat` → mandatos pendientes (JSON).
- `POST /api/agent/events` → lote de eventos de operaciones.
- `POST /api/agent/commands/{id}/ack` → confirmación de ejecución de mandato.

### 6.1 Clasificación y motor de políticas (fase 1)

- `Classification` define cómo se marca información sensible (regex / entidades / tipo
  de archivo; extensión a OCR/AI en fase 2).
- `Policy` combina scope (usuario/equipo), conditions (destinos/flujos), `action`,
  prioridad. Se evalúa de arriba abajo; aplica la primera coincidencia.
- Al recibir un `EventReport` el motor evalúa y **persiste el resultado** con la
  acción aplicable (`Allow/Log/Notify/Block/...`) para que la consola lo muestre.

## 7. Detección de bases de datos en servidores

El agente hace:

1. **Detección por proceso/servicio**: `sqlservr`, `postgres`, `mysqld`, `mariadbd`,
   `oracle`, `mongod`, `redis-server`, `elasticsearch`.
2. **Escaneo de puertos TCP** conocidos (1433, 5432, 3306, 1521, 27017, 6379, 9200) en
   `127.0.0.1` y (opcional) en la subred.
3. Persistencia en `device_db_artifacts` y exposición en la consola (Data discovery / BD).

## 8. Compatibilidad con Sophos XDR

El binario se firma, publica hash (`trust-pack`) y cohabita con Sophos mediante
exclusiones mínimas en direcciones y telemetría legítima. Detalle en `03-sophos-xdr.md`.

## 9. Requisitos (servidor Linux de producción)

- Ubuntu Server 22.04+ (x86_64), 8 GB RAM, 80 GB disco (Docker + Postgres).
- Docker Engine 20.10+ y Compose v2.
- Puertos: 8080 (API), 8085 (consola web). Puede haber sido un proxy inverso y TLS.

## 10. Implementación tec.es

| Componente | Proyecto |
|---|---|
| Dominio | `src/Server/EcDataguard.Domain` |
| Casos de uso | `src/Server/EcDataguard.Application` |
| Infraestructura (EF/Postgres, JWT) | `src/Server/EcDataguard.Infrastructure` |
| API REST | `src/Server/EcDataguard.Api` |
| Consola web (Blazor Server) | `src/Server/EcDataguard.Web` |
| Contrato de transporte | `src/Contracts` |
| Agente | `src/Agent/EcDataguard.Agent` |
| Despliegue | `deploy/` |

## 11. Escalabilidad (fase 2)

- Particionado por tenant/mes de `events`.
- Worker de ingesta separado (cola).
- Read replicas Postgres.
- Helm chart para K8s cuando se supere capacidad de una sola VM.