# Fase final — Infraestructura, evidencias y estado go-live

**Fecha:** 2026-06-12  
**Release:** v1.0.0.4+ (Gates A–E)  
**Veredicto pre-go-live:** **LISTO EN CÓDIGO** — **PENDIENTE VALIDACIÓN EN SERVIDOR IIS/PROD**

---

## Resumen ejecutivo

Se entregaron checklist de producción firmable, runbook Día D, plantilla de acta, scripts de validación/backup/smoke y referencias cruzadas al seguimiento CSV. La validación automatizada desde la estación de desarrollo confirma **223/223 tests unitarios** y artefactos de publicación; la validación IIS/SSL/SMTP/backup en producción **debe ejecutarse en el servidor** con los scripts incluidos.

---

## Evidencia disponible en repo (ahora)

| Área | Evidencia | Estado |
|------|-----------|--------|
| Tests unitarios | 223/223 OK (Release) | ✅ |
| Gate E seguridad | `GATE_E_RESULTADO_20260612.md` + 9 tests ADM-1 | ✅ |
| Health endpoints | `HealthController`: `/Health/Live`, `/Ready`, `/Details` | ✅ código |
| Headers seguridad | `Web.config` customHeaders (CSP, X-Frame, etc.) | ✅ config |
| SMTP remitente | `no_reply@aviacioncivil.gob.ec` en config + `AocrEmailService` | ✅ config |
| email_queue idempotencia | `event_key` + Gate D/E | ✅ código |
| Backup script | `scripts/db/backup_aocr_before_cleanup.ps1` | ✅ |
| Go-live scripts | `scripts/golive/*` | ✅ nuevo |
| Documentación | Manuales MD + checklist doc 100% | ⚠️ PNG parcial |
| Publicación | Perfil `FolderProfile4` → `C:\AOCR\publicacion1` | ⚠️ verificar en servidor |

---

## Validación infraestructura (15 ítems)

| # | Ítem | Implementación en código | Validación servidor |
|---|------|--------------------------|---------------------|
| 1 | IIS App Pool .NET 4.8 | Target `net48` | `validate-infra.ps1` INF-06 |
| 2 | SSL productivo | Rewrite HTTPS comentado* | Infra binding + HSTS |
| 3 | Permisos App_Data | Health check disk write | INF-04 |
| 4 | AOCR_CONNSTR_POSTGRESQL | `SecureConfigurationService` | INF-07 |
| 5 | Backup diario | Script pg_dump | Job DBA + BAK-01 |
| 6 | Restore probado | Procedimiento runbook | DBA acta |
| 7 | AS400 ODBC | `AS400HealthCheck`, P9ConnectionString | `/Health/As400` |
| 8 | Credenciales env vars | `AOCR_*` prefix | SEC-06 🔴 |
| 9 | SMTP productivo | mail.aviacioncivil.gob.ec:25 | COR-01 telnet |
| 10 | no_reply habilitado | FromEmail config | COR-02 correo prueba |
| 11 | email_queue | Health Details + SQL | INF-09 / check-email-queue.sql |
| 12 | Certificados .p12 | App_Data/firma | INF-10 + PDF-01..03 |
| 13 | Seguridad CSRF/upload/headers | Gates E + Web.config | SEC-01..04 |
| 14 | Observabilidad logs/health | App_Data/Logs + Health | OBS-01..05 |
| 15 | Rollback | backup-pre-deploy.ps1 | BAK-03/04 |

\* **HSTS/HTTPS redirect** requiere módulo **IIS URL Rewrite** instalado; actualmente comentado en `Web.config` para evitar error 500.19. Habilitar en producción tras instalar módulo.

---

## Hallazgo crítico pre-producción 🔴

El `Web.config` del repositorio contiene **credenciales en texto plano** (BD, AS400, password certificado .p12). **Acción obligatoria antes de go-live:**

1. Crear variables de entorno en el servidor IIS (ver tabla en `CHECKLIST_PRODUCCION_AOCR.md` §SEC-06).
2. Limpiar valores sensibles del `Web.config` publicado o usar `Web.Release.config` transform.
3. Validación Seguridad firma SEC-06 y SEC-07.

---

## Scripts entregados

```
scripts/golive/
├── validate-infra.ps1      # Infra + health + certs + PG
├── backup-pre-deploy.ps1   # T-24h BD + app
├── smoke-test-urls.ps1     # T+15m HTTP
├── recycle-apppool-golive.ps1
└── sql/
    └── check-email-queue.sql
```

### Ejemplo ejecución (servidor IIS)

```powershell
cd C:\AOCR\AOCR05-01-2026\AOCR1\AOCR\scripts\golive

# Pre-deploy
$env:PGPASSWORD = '***'
.\validate-infra.ps1 -PublishPath C:\AOCR\publicacion1 -SiteName "AOCR" -BaseUrl "https://URL_PROD"

# T-24h
.\backup-pre-deploy.ps1 -Password $env:PGPASSWORD

# T+15m
.\smoke-test-urls.ps1 -BaseUrl "https://URL_PROD"
```

---

## Checklist producción — resumen por categoría

| Categoría | Ítems | Bloqueantes 🔴 |
|-----------|-------|----------------|
| Seguridad | 9 | SEC-06, SEC-07, SEC-08 |
| Integridad datos | 4 | DAT-01, DAT-03 |
| Errores | 3 | ERR-02 |
| Headers | 4 | HDR-04 (prod HTTPS) |
| Observabilidad | 6 | OBS-01..03 |
| Auditoría | 3 | — |
| Correo/PDF | 8 | COR-01..03, PDF-04 |
| Backups | 4 | BAK-01..03 |
| Monitoreo | 3 | — |
| Despliegue | 5 | DEP-01..04 |
| Tests gates | 5 | E2E pendientes |
| Documentación | 5 | DOC-02 capturas |
| QA E2E | 7 | E2E-01..07 |
| Firmas | 6 roles | Todas 🔴 |

Documento completo: [`CHECKLIST_PRODUCCION_AOCR.md`](CHECKLIST_PRODUCCION_AOCR.md)

---

## Go-live Día D — estado

| Fase | Documento | Estado |
|------|-----------|--------|
| T-24h backup | `GO_LIVE_RUNBOOK_DIA_D.md` | Procedimiento listo |
| T-1h publish | Runbook + FolderProfile4 | Pendiente ejecución |
| T-0 recycle | `recycle-apppool-golive.ps1` | Pendiente |
| T+15m smoke | Runbook + smoke script | Pendiente |
| T+30m rutas | Runbook | Pendiente |
| T+1h monitoreo | Runbook | Pendiente |
| Acta final | `ACTA_GO_LIVE_TEMPLATE.md` | Plantilla lista |

---

## Documentación y seguimiento

| Entregable | Ubicación | Estado |
|------------|-----------|--------|
| Manuales PDF | `docs/export/` | Regenerar post-capturas |
| Capturas PNG | `docs/images/` | Parcial (ver CHECKLIST_DOCUMENTACION_100) |
| Release notes usuario | Crear `RELEASE_NOTES_GO_LIVE.md` | Pendiente |
| Seguimiento CSV | `docs/export/editable/SEGUIMIENTO_PUBLICACION_AOCR.csv` | Filas GL-* definidas |
| Capacitación | Plan PO | Pendiente sesiones |

---

## Criterios de aceptación fase final

| # | Criterio | Estado |
|---|----------|--------|
| 1 | Infraestructura validada | ⏳ Scripts listos; ejecución servidor pendiente |
| 2 | SMTP funcionando | ⏳ Prueba en prod |
| 3 | Certificados vigentes | ⏳ Verificar en servidor |
| 4 | Backup y rollback probados | ⏳ DBA |
| 5 | Checklist producción firmado | ⏳ Plantilla entregada |
| 6 | Smoke test aprobado | ⏳ Día D |
| 7 | Logs limpios | ⏳ T+1h |
| 8 | Estabilidad primera hora | ⏳ Día D |
| 9 | Go-live aprobado | ⏳ Acta pendiente |

---

## Veredicto

| Ámbito | Veredicto |
|--------|-----------|
| Código + tests + documentación operativa | **APTO para ventana go-live** |
| Infra producción (IIS/SSL/SMTP/backup) | **NO VALIDADO** — ejecutar checklist en servidor |
| Go-live | **NO EJECUTADO** — usar runbook Día D |

**Próximo paso recomendado:** ejecutar `validate-infra.ps1` y `backup-pre-deploy.ps1` en el servidor IIS de preproducción, corregir SEC-06 (secrets), firmar checklist, programar ventana Día D con acta.
