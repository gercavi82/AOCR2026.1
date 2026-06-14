# Runbook Go-Live — Día D — Sistema AOCR

**Versión:** 2026-06-12  
**Ventana sugerida:** Sábado 06:00–10:00 o ventana acordada DGAC  
**Rollback máximo:** 30 minutos si smoke falla

---

## Roles en sala

| Rol | Responsabilidad |
|-----|-----------------|
| Dev Lead | Publicación, verificación DLL, rollback técnico |
| Infra | IIS, App Pool, SSL, smoke HTTP |
| DBA | Backup/restore, monitoreo BD y email_queue |
| QA | Smoke funcional por rol, sidebar, correos |
| PO | Go/no-go final |

---

## T-24 horas — Congelamiento y backup

| Hora | Acción | Comando / evidencia |
|------|--------|---------------------|
| T-24h | Congelar merges a rama release | Tag `release/golive-YYYYMMDD` |
| T-24h | Backup BD completo | `.\scripts\golive\backup-pre-deploy.ps1 -Password ***` |
| T-24h | Backup carpeta `publicacion1` | Incluido en script anterior |
| T-24h | Verificar backup restorable | DBA: restore en sandbox |
| T-24h | Validar infra pre-deploy | `validate-infra.ps1 -SiteName ...` |
| T-24h | Comunicar ventana a usuarios piloto | Correo institucional |

**Criterio continuar:** backup BD + app verificados; checklist SEC-06 secrets en prod.

---

## T-1 hora — Publicación

| Hora | Acción | Detalle |
|------|--------|---------|
| T-1h | Build Release | Visual Studio → Release → Build Solution |
| T-1h | Tests finales | 223/223 `AOCR.Tests` |
| T-1h | Publicar | Perfil `FolderProfile4` → `C:\AOCR\publicacion1` |
| T-1h | Verificar DLL | Timestamps `CapaPresentacion.dll`, `CapaNegocio.dll`, `CapaDatos.dll` |
| T-1h | Verificar assets | CSS/JS `VersionFront` en layout |
| T-1h | **No sobrescribir** | `App_Data/Logs`, certificados `.p12` locales del servidor |

```powershell
# Publicación desde VS o MSBuild
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  "CapaPresentacion\CapaPresentacion.csproj" /p:DeployOnBuild=true `
  /p:PublishProfile=FolderProfile4 /p:Configuration=Release
```

---

## T-0 — Activación

| Hora | Acción | Comando |
|------|--------|---------|
| T-0 | Reciclar App Pool | `.\scripts\golive\recycle-apppool-golive.ps1 -SiteName "..."` |
| T-0 | Smoke inmediato Live/Ready | `smoke-test-urls.ps1 -BaseUrl https://...` |
| T-0 | Verificar arranque | Sin 500 en `/Account/Login` |

**Si falla T-0:** ejecutar rollback (sección final).

---

## T+15 minutos — Smoke login y health

### Automatizado

```powershell
.\scripts\golive\smoke-test-urls.ps1 -BaseUrl "https://URL_PROD"
```

### Manual por rol (registrar captura + hora)

| Rol | URL entrada | Criterio | ☐ |
|-----|-------------|----------|---|
| RT | `/SolicitudAOCR` | Bandeja carga; compañía activa | ☐ |
| Coordinación | `/Tecnico` | Asignación inspector visible | ☐ |
| Inspector | `/RevisionDocumental` | Solo trámites asignados | ☐ |
| Financiero | `/Financiero` | Contador pendientes OK | ☐ |
| DIRDAC | `/Inspeccion` o bandeja dirección | Pendientes informe/AOCR | ☐ |

### Health

| Endpoint | Esperado |
|----------|----------|
| `/Health/Live` | 200 OK |
| `/Health/Ready` | 200 READY |
| `/Health/Details` | status healthy; postgresql OK |

---

## T+30 minutos — Rutas críticas

| Prueba | URL | Criterio | ☐ |
|--------|-----|----------|---|
| Tecnico | `/Tecnico` | Sin 500 | ☐ |
| Revisión documental | `/RevisionDocumental` | Sin 500; filtro inspector | ☐ |
| Sidebar | Cualquier layout | Contadores cargan (AJAX) | ☐ |
| Correo financiero | Aprobar pago prueba | 1 correo; event_key único | ☐ |
| Logs | `App_Data/Logs/AOCR_*.log` | Sin ERROR crítico repetido | ☐ |

```sql
-- Tras correo financiero
\i scripts/golive/sql/check-email-queue.sql
```

---

## T+1 hora — Monitoreo estabilidad

| Revisión | Acción |
|----------|--------|
| Logs aplicación | Últimas 200 líneas sin excepciones repetidas |
| IIS Failed Request | Sin pico 500 |
| email_queue | Pendientes < 100; ERROR = 0 sostenido |
| PostgreSQL | Conexiones pool estables |
| AS400 (opcional) | `/Health/As400` si facturación activa |
| Performance | Tiempo respuesta Detalle < 5s |

**Criterio estabilidad:** sin incidentes P1; PO confirma go-live sostenido.

---

## Rollback (si smoke T+15 falla)

1. **Detener tráfico** — sitio IIS offline o mantenimiento.
2. **Restaurar aplicación** — robocopy desde `C:\AOCR\backups\golive\{timestamp}\aplicacion`.
3. **Restaurar BD** — solo si deploy incluyó migración; usar dump T-24h.
4. **Reciclar App Pool**.
5. **Smoke** en versión anterior.
6. **Comunicar** rollback a stakeholders; post-mortem en 24h.

---

## Checklist rápido Día D

```
T-24h  ☐ Backup BD   ☐ Backup app   ☐ Restore probado
T-1h   ☐ Build        ☐ Tests 223/223 ☐ Publish
T-0    ☐ Recycle pool ☐ /Health/Live OK
T+15m  ☐ Login 5 roles ☐ /Health/Details healthy
T+30m  ☐ /Tecnico ☐ /RevisionDocumental ☐ Sidebar ☐ Correo
T+1h   ☐ Logs limpios ☐ email_queue OK ☐ PO aprueba
```

---

## Referencias

- [`CHECKLIST_PRODUCCION_AOCR.md`](CHECKLIST_PRODUCCION_AOCR.md)
- [`GATE_E_RESULTADO_20260612.md`](GATE_E_RESULTADO_20260612.md)
- [`GUIA_PRUEBAS_POST_REPUBLICACION.md`](GUIA_PRUEBAS_POST_REPUBLICACION.md)
- Scripts: `scripts/golive/`
