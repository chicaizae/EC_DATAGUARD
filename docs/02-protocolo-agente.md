# 02 · Protocolo agente-servidor

Transporte: HTTPS + JSON sobre REST. Base del servidor: `https://<host>:8080/api`.

Autenticación del agente: `Authorization: Bearer <device_token>` (JWT emitido por el
servidor al apartar el dispositivo). El token contiene `tenant_id` y `device_id`.

Las fechas van en UTC (ISO-8601: `yyyy-MM-ddTHH:mm:ssZ`).

## 1. Apartado del dispositivo

El proveedor crea el dispositivo desde la consola (`POST /api/console/tenants/{id}/devices`)
y recibe un `device_token` único por agente/empresa. **Cada empresa usa sus propios
agentes y tokens** (agentes específicos por empresa).

## 2. Heartbeat

```
POST /api/agent/heartbeat
```

Cuerpo (ejemplo):

```json
{
  "deviceId": "a1b2c3d4-...",
  "hostname": "srv-finanzas-01",
  "os": "windows",
  "osVersion": "10.0.19045",
  "agentVersion": "1.0.0",
  "protectionState": "protected",
  "protectionDetails": ["kernel", "content-scan"],
  "configRevision": 3,
  "userName": "edison.chicaiza",
  "networkInterfaces": [
    { "name": "eth0", "ip": "192.168.1.10", "mac": "AA:BB:CC:DD:EE:FF" }
  ],
  "databases": [
    { "engine": "postgresql", "host": "127.0.0.1", "port": 5432, "instance": "pg14", "reachable": true }
  ],
  "sophos": { "xdrCompatible": true, "trustHashRegistered": true },
  "uptimeSeconds": 259200
}
```

Respuesta 200 (mandatos pendientes; el array puede ir vacío):

```json
{
  "serverTimeUtc": "2026-08-07T10:00:00Z",
  "commands": [
    {
      "commandId": "c-1001",
      "type": "ApplyPolicy",
      "payload": { "policySetVersion": 4, "policies": [] },
      "issuedUtc": "2026-08-07T09:59:00Z"
    },
    {
      "commandId": "c-1002",
      "type": "SetConfig",
      "payload": { "heartbeatIntervalSeconds": 30, "collection": { "databases": true, "events": true } }
    },
    {
      "commandId": "c-1003",
      "type": "RestartAgent",
      "payload": {}
    }
  ]
}
```

Tipos de mandato (fase 1): `ApplyPolicy`, `SetConfig`, `RestartAgent`, `UpdateAgent`,
`QuarantineDevice`, `RefreshInventory`.

## 3. Confirmación de mandato

```
POST /api/agent/commands/{commandId}/ack
```

```json
{ "status": "succeeded", "detail": "politicas aplicadas (3 reglas)", "appliedUtc": "..." }
```

`status`: `succeeded | failed | skipped`. Los fallos se reintentan en el próximo heartbeat.

## 4. Eventos

```
POST /api/agent/events
```

Lote máximo: 200 eventos por request.

| kind | Descripción |
|---|---|
| `file_op` | Operación de archivo (create/copy/move/delete/print/rename) |
| `usb` | Conexión de dispositivo externo |
| `web` | Visita web (dominio, categoría) |
| `app` | Uso de aplicación |
| `db_found` | Base de datos descubierta |
| `config_error` | Error local del agente |

```json
{
  "events": [
    {
      "eventId": "e-1",
      "kind": "file_op",
      "occurredUtc": "2026-08-07T09:30:00Z",
      "actor": { "userName": "edison.chicaiza", "processName": "OUTLOOK.EXE", "pid": 1234 },
      "operation": "copy",
      "filePath": "C:\\Users\\edison.chicaiza\\Desktop\\informe.pdf",
      "destinationType": "external_storage",
      "destinationDetail": "USB: Kingston 64GB (S/N ABC123)",
      "fileSizeBytes": 254300,
      "fileHashSha256": "ab12...",
      "contentScan": { "done": true, "classifications": ["PII/CCPA", "Financiero"] }
    }
  ]
}
```

Respuesta:

```json
{ "accepted": 1, "rejected": 0, "nextUploadAllowedUtc": "..." }
```

## 5. Manejo de errores

| Código | Significado | Acción del agente |
|---|---|---|
| 401 | Token inválido o vencido | Detenerse e informar (requiere nuevo apartado) |
| 429 | Demasiadas solicitudes | Backoff exponencial |
| 503 | Servidor en mantenimiento | Reintentar con backoff; no detener protección local |

## 6. Mandato ApplyPolicy (descriptor)

```json
{
  "policySetVersion": 4,
  "policies": [
    {
      "id": "p-9",
      "name": "Bloquear USB con datos PII",
      "enabled": true,
      "priority": 1,
      "scope": { "teams": ["T-1"], "users": [] },
      "conditions": { "destinations": ["external_storage"], "classifications": ["PII/CCPA"] },
      "action": "Block",
      "insightTrigger": "Always"
    }
  ]
}
```

En fase 1 el payload del mandato incluye el descriptor completo; el agente lo aplica
localmente y reporta `ack`.

## 7. Detección de bases de datos

El agente escanea (ciclo configurable, por defecto 24 h):

1. **Procesos/servicios** en ejecución: `sqlservr`, `postgres`, `mysqld`, `mariadbd`,
   `oracle`, `mongod`, `redis-server`, `elasticsearch`.
2. **Puertos TCP** en `127.0.0.1` y (si `collection.networkScan=true`) en la subred:
   1433 (MSSQL), 5432 (PostgreSQL), 3306 (MySQL), 1521 (Oracle), 27017 (MongoDB),
   6379 (Redis), 9200 (Elasticsearch).

Cada hallazgo emite un evento `db_found` (`engine`, `host`, `port`, `instance`,
`reachable`), se consolida en `device_db_artifacts` y se muestra en la consola
(Data discovery / Servidores).