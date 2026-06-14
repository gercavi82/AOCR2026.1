# Acta de Go-Live — Sistema AOCR DGAC

**Fecha go-live:** __________________  
**Hora inicio ventana:** __________________  
**Hora cierre ventana:** __________________  
**Versión desplegada:** __________________  
**URL producción:** __________________  
**Entorno BD:** __________________  

---

## 1. Participantes

| Rol | Nombre | Presente |
|-----|--------|----------|
| Dev Lead | | ☐ |
| DBA | | ☐ |
| Seguridad | | ☐ |
| QA | | ☐ |
| Infra / IIS | | ☐ |
| PO / Negocio | | ☐ |

---

## 2. Evidencias presentadas

| Evidencia | Ruta / referencia | ☐ |
|-----------|-------------------|---|
| Backup T-24h BD | `C:\AOCR\backups\golive\...` | ☐ |
| Backup T-24h aplicación | Idem | ☐ |
| Restore probado (fecha) | | ☐ |
| validate-infra.ps1 | Salida adjunta | ☐ |
| Tests unitarios 223/223 | vstest log | ☐ |
| smoke-test-urls T+15m | Salida adjunta | ☐ |
| check-email-queue.sql | Query post correo | ☐ |
| CHECKLIST_PRODUCCION firmado | docs/CHECKLIST_PRODUCCION_AOCR.md | ☐ |

---

## 3. Cronología Día D

| Hora | Hito | Resultado | Observaciones |
|------|------|-----------|---------------|
| T-24h | Backup | ☐ OK ☐ N/A | |
| T-1h | Publicación | ☐ OK ☐ Fallo | |
| T-0 | Recycle App Pool | ☐ OK | |
| T+15m | Smoke health + login | ☐ OK ☐ Fallo | |
| T+30m | Tecnico / RevisionDocumental | ☐ OK ☐ Fallo | |
| T+1h | Monitoreo estabilidad | ☐ OK ☐ Fallo | |

---

## 4. Smoke test por rol

| Rol | Login OK | Pantalla principal OK | Sin 500 | Evidencia |
|-----|----------|----------------------|---------|-----------|
| RT | ☐ | ☐ | ☐ | |
| Coordinación | ☐ | ☐ | ☐ | |
| Inspector | ☐ | ☐ | ☐ | |
| Financiero | ☐ | ☐ | ☐ | |
| DIRDAC | ☐ | ☐ | ☐ | |

---

## 5. Health checks

| Endpoint | HTTP | postgresql | email_queue | Veredicto |
|----------|------|------------|-------------|-----------|
| /Health/Live | | N/A | N/A | |
| /Health/Ready | | | N/A | |
| /Health/Details | | | | |

---

## 6. Incidentes durante ventana

| ID | Hora | Severidad | Descripción | Acción | Estado |
|----|------|-----------|-------------|--------|--------|
| | | | | | |
| | | | | | |

---

## 7. Rollback ejecutado

☐ No aplicó  
☐ Sí — hora: ______ motivo: ________________________________

---

## 8. Veredicto final

☐ **GO-LIVE APROBADO** — Sistema estable; operación normal autorizada.  
☐ **GO-LIVE APROBADO CON OBSERVACIONES** — Ver ítems pendientes.  
☐ **GO-LIVE RECHAZADO / ROLLBACK** — Ver incidentes.

**Observaciones y pendientes post go-live (72h):**

1.  
2.  
3.  

---

## 9. Firmas

| Rol | Nombre | Firma | Fecha/Hora |
|-----|--------|-------|------------|
| Dev Lead | | | |
| DBA | | | |
| Seguridad | | | |
| QA Lead | | | |
| Infra | | | |
| PO | | | |

---

*Plantilla AOCR — completar el día del go-live y archivar en repositorio de documentación institucional.*
