# 04 · Roadmap

## Fase 0 — Fundación (entregado en este repositorio)

- [x] Servidor multi-tenant (Dominio, Aplicación, Infraestructura, API)
- [x] Consola web Blazor (Dashboard, Tenants, Dispositivos, Políticas, Eventos, Admin trail)
- [x] Agente Windows/Linux: heartbeat, detección de bases de datos, ejecución de mandatos
- [x] Motor de políticas y clasificación base (persistencia de resultados)
- [x] Docker Compose para servidor Linux
- [x] Documentación (arquitectura, protocolo, Sophos, roadmap)

## Fase 1 — Piloto con empresas reales

- [x] Onboarding de agentes por empresa (instalador genera agente específico del tenant,
      token por dispositivo persistido con hash, revocación y reemisión desde consola)
- [ ] Evaluación de políticas en el agente: Windows 🚧 monitoreo listo
      (FileSystemMonitor con FileSystemWatcher, ClipboardMonitor Win32, UsbMonitor con DriveInfo,
      clasificación local y envío de eventos DLP vía heartbeat); pendientes: aplicar políticas del
      servidor en el agente y detección Kernel. Linux (fanotify/inotify) queda como trabajo aparte
- [x] Clasificación real de contenido: regex + entidades (PII, tarjetas, info financiera)
- [x] Tipo real de archivo básico (extensión + firmas mágicas comunes)
- [ ] OCR con Tesseract — pendiente: requiere binarios nativos (tesseract + traineddata);
      el pipeline ya acepta ContentScan.Classifications; falta el componente OCR
- [x] Consola: tablas con filtros y exportación CSV básica para Eventos, Insights y Admin trail
- [x] Consola: detalle de dispositivos/eventos
- [x] Consola: exportación XLSX real (Eventos, Insights y Admin trail) (xlsx)
- [x] Consola: layouts avanzados — Dashboard con tarjetas KPI, cobertura y paneles
- [x] Insights (severidad + triage) y Admin trail con búsqueda/exportación
- [x] Reportes programados (XLSX) por correo — suscripción en consola, planificador cada min,
      ReportXlsxBuilder (OOXML nativo), SMTP configurable (ReportMail); PDF queda aparte
- [x] Integración SIEM: webhook JSON (OCSF) para Insights y Admin trail
- [x] Licenciamiento por usuario activo y control de consumo

## Fase 2 — Escalabilidad >2000 endpoints

- [ ] Particionado de `events` por tenant + mes; worker de ingesta asíncrona
- [ ] Read replicas PostgreSQL para analítica
- [ ] Chart Helm para Kubernetes
- [ ] Shadow copy (copia del archivo del incidente, cifrada, con retención)
- [ ] Protección frente a IA (asistentes), Git y mensajería (protocolo de destinatios)
- [ ] Sucesos de expertos: exclusiones, Volume aware, bypass temporal con override

## Fase 3 — Cloud dentro del servidor

- [ ] Clasificación asistida con IA local para etiquetas de archivos (AI tags)
- [ ] Resumen de Insights con IA (AI summary)
- [ ] Virtual reports (URL guardada) y layouts
- [ ] Tiempo real a la consola con SignalR (heartbeat time-realtime)
- [ ] Integraciones M365 / Google Workspace on-prem (inflama de archivos compartidos)

## KPIs sugeridos para la consola

- Endpoints protegidos / degradados / no protegidos (por tenant y total)
- Insights abiertos High/Medium
- Eventos DLP por canal (USB, web, correo, IM, print, Git, cloud)
- % de eventos bloqueados vs. disparados
- Bases de datos descubiertas por servidor (db_found)
- Cobertura de clasificación (operaciones clasificadas / relevantes)
