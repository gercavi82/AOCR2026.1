# Checklist de producción — Sistema AOCR DGAC

**Versión:** 2026-06-12  
**Release objetivo:** v1.0.0.4+ (Gates A–E)  
**Entorno:** Preproducción → Producción  
**Publicación:** `FolderProfile4` → `C:\AOCR\publicacion1`

---

## Instrucciones

- Marque **☑** cuando la validación esté completa con evidencia adjunta.
- **Bloqueante go-live:** ítems marcados con 🔴.
- Evidencia sugerida: captura, log, salida script, ticket, ruta archivo.
- Scripts: `scripts/golive/validate-infra.ps1`, `backup-pre-deploy.ps1`, `smoke-test-urls.ps1`.

---

## 1. Seguridad 🔴

| ☐ | ID | Control | Criterio | Evidencia | Responsable |
|---|-----|---------|----------|-----------|-------------|
| ☐ | SEC-01 | Autorización `[AocrAuthorize]` | Gates E aprobados; URL directa → 403 | `docs/GATE_E_RESULTADO_20260612.md` | Dev |
| ☐ | SEC-02 | CSRF | POST críticos con `[ValidateAntiForgeryToken]` | Revisión Inspeccion/Solicitud | Dev |
| ☐ | SEC-03 | Upload | Extensión/tamaño (`MaxUploadSize`, RT_MaxFileSizeMb) | `Web.config` + DocumentoController | Dev |
| ☐ | SEC-04 | Headers | X-Frame-Options, CSP, X-Content-Type-Options | `Web.config` §customHeaders | Infra |
| ☐ | SEC-05 | HSTS | SSL productivo + regla rewrite HSTS habilitada | IIS + módulo URL Rewrite | Infra |
| ☐ | SEC-06 | Secrets 🔴 | **Sin passwords en Web.config prod**; usar `AOCR_*` env vars | Variables entorno servidor | Seguridad |
| ☐ | SEC-07 | Certificado .p12 | Password **no** en texto plano en prod | `AOCR_AOCRCERTIFICADOINSTITUCIONALPASSWORD` | Seguridad |
| ☐ | SEC-08 | SSL productivo | Certificado válido, cadena completa, TLS 1.2+ | IIS binding HTTPS | Infra |
| ☐ | SEC-09 | Sesión | Timeout 20 min (`SessionTimeout`) | Prueba inactividad | QA |

**Nota crítica (dev):** el `Web.config` del repo contiene credenciales de desarrollo. **No publicar tal cual a producción.** Migrar a:

| Variable entorno | Uso |
|------------------|-----|
| `AOCR_CONNSTR_POSTGRESQL` | BD principal |
| `AOCR_AS400_PASSWORD` | AS400 |
| `AOCR_EMAIL_USERNAME` / `AOCR_EMAIL_PASSWORD` | SMTP |
| `AOCR_AOCRCERTIFICADOINSTITUCIONALPASSWORD` | Firma .p12 |

---

## 2. Integridad de datos 🔴

| ☐ | ID | Control | Criterio | Evidencia | Responsable |
|---|-----|---------|----------|-----------|-------------|
| ☐ | DAT-01 | PostgreSQL prod | `AOCR_CONNSTR_POSTGRESQL` apunta a BD productiva | `validate-infra.ps1` INF-07/08 | DBA |
| ☐ | DAT-02 | Estados canónicos | Sin uso activo de `EstadosSolicitudAOCR` legacy | Tests + `AocrEstadoService` | Dev |
| ☐ | DAT-03 | email_queue event_key | Índice único; sin duplicados pendientes | `check-email-queue.sql` | DBA |
| ☐ | DAT-04 | Transiciones | Matriz `EstadoSolicitud` respetada en workflow | Tests 223/223 | Dev |

---

## 3. Manejo de errores

| ☐ | ID | Control | Criterio | Evidencia | Responsable |
|---|-----|---------|----------|-----------|-------------|
| ☐ | ERR-01 | 403 institucional | `Error/NoAutorizado` en web; JSON 403 AJAX | Prueba ADM-1 manual | QA |
| ☐ | ERR-02 | Sin 500 en smoke | T+30m sin errores 500 en rutas clave | Log IIS + smoke script | QA |
| ☐ | ERR-03 | Circuit breaker AS400 | `/Health/As400` degradado controlado | Dashboard Health | Infra |

---

## 4. Headers HTTP

| ☐ | ID | Header | Valor esperado | Evidencia |
|---|-----|--------|----------------|-----------|
| ☐ | HDR-01 | X-Frame-Options | SAMEORIGIN | curl -I |
| ☐ | HDR-02 | X-Content-Type-Options | nosniff | curl -I |
| ☐ | HDR-03 | Content-Security-Policy | Configurado (Web.config) | curl -I |
| ☐ | HDR-04 | Strict-Transport-Security | max-age=31536000 (solo HTTPS prod) | curl -I HTTPS |

---

## 5. Observabilidad 🔴

| ☐ | ID | Control | Criterio | Evidencia | Responsable |
|---|-----|---------|----------|-----------|-------------|
| ☐ | OBS-01 | `/Health/Live` | HTTP 200 "OK" | smoke-test | Infra |
| ☐ | OBS-02 | `/Health/Ready` | HTTP 200 cuando PG OK | smoke-test | Infra |
| ☐ | OBS-03 | `/Health/Details` | postgresql + disk + email_queue | JSON respuesta | Infra |
| ☐ | OBS-04 | Logs App_Data | `App_Data/Logs/AOCR_*.log` escribibles | Permisos + archivo del día | Infra |
| ☐ | OBS-05 | Rotación logs | Política retención definida (≥30 días) | Procedimiento ops | Infra |
| ☐ | OBS-06 | Dashboard Admin | `/Health/Dashboard` solo Administrador | Login admin | QA |

---

## 6. Auditoría

| ☐ | ID | Control | Criterio | Evidencia | Responsable |
|---|-----|---------|----------|-----------|-------------|
| ☐ | AUD-01 | Intentos no autorizados | `AocrAuthorize` registra denegación | Log auditoría | Seguridad |
| ☐ | AUD-02 | Historial solicitud | Cambios estado trazables | Detalle #12 historial | QA |
| ☐ | AUD-03 | email_queue | correlation_id + event_key en envíos | Query BD | DBA |

---

## 7. Resiliencia correo / PDF 🔴

| ☐ | ID | Control | Criterio | Evidencia | Responsable |
|---|-----|---------|----------|-----------|-------------|
| ☐ | COR-01 | SMTP productivo | Relay `mail.aviacioncivil.gob.ec:25` accesible | Telnet / envío prueba | Infra |
| ☐ | COR-02 | Remitente | `no_reply@aviacioncivil.gob.ec` habilitado | Correo recibido en bandeja prueba | Infra |
| ☐ | COR-03 | Cola | Procesador email_queue drena PENDIENTE | `check-email-queue.sql` | DBA |
| ☐ | COR-04 | Idempotencia | Reintento no duplica (event_key) | Gate D tests | Dev |
| ☐ | PDF-01 | Cert LV inspector | .p12 vigente en `App_Data/firma` | Lista certificados | QA |
| ☐ | PDF-02 | Cert informe inspector | Mismo u otro .p12 según usuario | Firma prueba | QA |
| ☐ | PDF-03 | Cert DIRDAC | .p12 dirección disponible | Firma AOCR prueba | QA |
| ☐ | PDF-04 | PDF AOCR/CL | Formato institucional sin variables internas | PDF manual Gate E4 | QA |

---

## 8. Backups 🔴

| ☐ | ID | Control | Criterio | Evidencia | Responsable |
|---|-----|---------|----------|-----------|-------------|
| ☐ | BAK-01 | Backup diario BD | Job programado y exitoso últimas 24h | Log backup | DBA |
| ☐ | BAK-02 | Restore probado | Restore a entorno aislado ≤ 30 días | Acta restore | DBA |
| ☐ | BAK-03 | T-24h pre-deploy | `backup-pre-deploy.ps1` ejecutado | Ruta `C:\AOCR\backups\golive\` | Infra |
| ☐ | BAK-04 | Carpeta app | Copia `publicacion1` en backup T-24h | robocopy manifest | Infra |

---

## 9. Monitoreo

| ☐ | ID | Control | Criterio | Evidencia | Responsable |
|---|-----|---------|----------|-----------|-------------|
| ☐ | MON-01 | IIS failed req | Logging habilitado | IIS config | Infra |
| ☐ | MON-02 | Alertas disco | Espacio App_Data > 20% libre | Monitor servidor | Infra |
| ☐ | MON-03 | email_queue ERROR | Alerta si ERROR > umbral | Query + procedimiento | DBA |

---

## 10. Despliegue 🔴

| ☐ | ID | Control | Criterio | Evidencia | Responsable |
|---|-----|---------|----------|-----------|-------------|
| ☐ | DEP-01 | Build Release | Sin errores compilación | MSBuild log | Dev |
| ☐ | DEP-02 | Tests | 223/223 unitarios OK | vstest output | Dev |
| ☐ | DEP-03 | Publicación | `FolderProfile4` → `publicacion1` | Timestamps DLL | Dev |
| ☐ | DEP-04 | App Pool recycle | T-0 post deploy | recycle script log | Infra |
| ☐ | DEP-05 | Rollback documentado | Procedimiento restore BD + app | GO_LIVE_RUNBOOK | Infra |

---

## 11. Tests

| ☐ | ID | Gate | Veredicto código | E2E manual | Referencia |
|---|-----|------|------------------|------------|------------|
| ☐ | TST-A | Gate A | OK | Pendiente IIS | GATE_A |
| ☐ | TST-B | Gate B | Parcial | Pendiente #12 | GATE_B |
| ☐ | TST-C | Gate C | OK bug FirmadoCoordinador | Pendiente | GATE_C |
| ☐ | TST-D | Gate D | OK | Pendiente NC | GATE_D |
| ☐ | TST-E | Gate E | OK 223 tests | Pendiente ADM-1 browser | GATE_E |

---

## 12. Documentación

| ☐ | ID | Artefacto | Estado | Export PDF |
|---|-----|-----------|--------|------------|
| ☐ | DOC-01 | Manuales usuario/técnico | Existen MD | `docs/export/` |
| ☐ | DOC-02 | Capturas PNG 42 | Parcial | `docs/images/` |
| ☐ | DOC-03 | Release notes usuario | Pendiente v1.0 go-live | Crear post-capturas |
| ☐ | DOC-04 | Capacitación RT/Inspector/Coord | Plan sesiones | PO |
| ☐ | DOC-05 | Seguimiento CSV | Actualizado | `SEGUIMIENTO_PUBLICACION_AOCR.csv` |

---

## 13. QA E2E 🔴

| ☐ | ID | Escenario | Rol | Criterio |
|---|-----|-----------|-----|----------|
| ☐ | E2E-01 | Login RT | RT | Dashboard carga |
| ☐ | E2E-02 | Login Coordinación | Coord | Bandeja asignación |
| ☐ | E2E-03 | Login Inspector | Inspector | Solo trámites asignados |
| ☐ | E2E-04 | Login Financiero | Financiero | Contador sidebar OK |
| ☐ | E2E-05 | Login DIRDAC | DIRDAC | Pendientes dirección |
| ☐ | E2E-06 | Sidebar contadores | Todos | Coinciden con bandejas |
| ☐ | E2E-07 | Correo financiero | Financiero | 1 correo, event_key único |

---

## 14. Firmas de aprobación go-live 🔴

| Rol | Nombre | Fecha | Firma | Observaciones |
|-----|--------|-------|-------|---------------|
| Dev Lead | | | | Gates A–E código OK |
| DBA | | | | Backup/restore probado |
| Seguridad | | | | Secrets migrados; headers OK |
| QA Lead | | | | Smoke + E2E aprobados |
| Infra | | | | IIS/SSL/SMTP OK |
| PO / Negocio | | | | Aceptación funcional |

**Veredicto final go-live:** ☐ APROBADO  ☐ APROBADO CON OBSERVACIONES  ☐ RECHAZADO

**Observaciones:**

---

## Comandos rápidos

```powershell
# Validar infra (servidor IIS)
.\scripts\golive\validate-infra.ps1 -PublishPath C:\AOCR\publicacion1 -SiteName "NombreSitio" -BaseUrl "https://aocr.dgac.gob.ec"

# Backup T-24h
.\scripts\golive\backup-pre-deploy.ps1 -Password $env:PGPASSWORD

# Smoke T+15m
.\scripts\golive\smoke-test-urls.ps1 -BaseUrl "https://aocr.dgac.gob.ec"

# Reciclar T-0
.\scripts\golive\recycle-apppool-golive.ps1 -SiteName "NombreSitio"
```

```bash
# Cola correos
psql ... -f scripts/golive/sql/check-email-queue.sql
```
