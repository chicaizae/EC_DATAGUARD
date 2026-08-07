# 03 · Compatibilidad con Sophos XDR

## Objetivo

Que Sophos XDR **no marque como sospechosa** la actividad de EC DATAGUARD (binario,
servicio, telemetría), manteniendo ambos productos activos en paralelo y sin desactivar
la protección del cliente.

## Principios

1. **Transparencia y firma**: los binarios del agente se firman (Authenticode en
   Windows, GPG/package manager en Linux) y se publica el `trust-pack` (hash + metadatos)
   para que el integrador lo registre en Sophos.
2. **No evasión**: el agente usa nombres de servicio previsibles y rutas estándar
   (`C:\Program Files\EcDataguard\Agent\` y `/opt/ecdataguard/agent/`). No altera
   comportamiento ni mutación.
3. **Telemetría legítima**: conexiones salientes HTTPS solo al servidor EC DATAGUARD
   (puerto 8080 o 443), documentadas en el `trust-pack`.
4. **Exclusiones guiadas**: solo los componentes propios de EC Datagard, revisables y
   auditables, sin debilitar el antivirus.

## Exclusiones recomendadas en Sophos (Intercept X / Management)

| Elemento | Valor de ejemplo |
|---|---|
| Ruta (Windows) | `C:\Program Files\EcDatag\Agent\` |
| Ruta (Linux) | `/opt/ecdataguard/agent/` |
| Proceso | `EcDataguardAgent.exe` |
| Servicio | `EcDataguardAgentSvc` |
| Publisher confiable | `Ecoilpet S.A.` (si el certificado está registrado) |
| Tráfico | Saliente a `https://<servidor>:8080/api` (solo dominios/instrumentos del servidor EC) |

## Trust-pack (paquete de confianza)

El agente ofrece `GET /api/agent/trust-pack` con una respuesta tipo:

```json
{
  "product": "EcDataguard Agent",
  "version": "1.0.0",
  "sha256": "<hash del paquete>",
  "signer": "Ecoilpet S.A.",
  "serviceName": "EcDataguardAgentSvc",
  "network": { "direction": "outbound", "ports": [443, 8080] },
  "xdrCompatible": true
}
```

Este JSON es el contrato que el administrador del cliente usa para dar de alta el
agente como aplicación confiable en Sophos (y, en general, en cualquier EDR).

## Checklist de integración en la plataforma

- [ ] Firmas de binarios y publicación de `trust-pack` por release.
- [ ] El heartbeat reporta `sophos.xdrCompatible=true` y `trustHashRegistered`.
- [ ] `docs/03-sophos-xdr.md` como referencia de instalación.
- [ ] Modo de prueba `--sophos-test`: el agente envía un heartbeat marcado
      `test=true` y la consola muestra "compatibilidad verificada".
- [ ] Registro de arranque del agente (`agent started`) para que el integrador confirme
      telemetría legítima.

## Nota

Las exclusiones se conceden únicamente para los componentes propios, con revisión
periódica y auditoría. EC DATAGUARD no desactiva protecciones de red/antivirus de
Sophos en el endpoint.