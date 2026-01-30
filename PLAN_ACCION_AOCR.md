# 1) Objetivo del Plan
Este plan busca prevenir fallas de **seguridad**, **integridad de datos**, **estabilidad operativa**, **dependencias/paquetes** y **despliegue** para preparar AOCR en producción (IIS/Windows Server) con DB2 AS/400, PostgreSQL, subida de comprobantes, correo y generación de PDF. El foco es el flujo crítico orden→comprobante→aprobación→factura→correo.【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L1-L474】【F:CapaPresentacion/Controllers/PagoController.cs†L1-L118】【F:CapaDatos/Services/EmailService.cs†L1-L236】

# 2) Alcance y Supuestos
**Incluye**
- Flujo crítico: Orden de Recaudación → pago/comprobante → aprobación/rechazo financiero → generación PDF → notificación por correo.【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L281-L474】【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】【F:CapaPresentacion/Services/PdfGeneratorService.cs†L1-L159】【F:CapaDatos/Services/EmailService.cs†L1-L236】
- Capas: Presentación (MVC), Negocio (BL), Datos (DAO), Utilidades (si aplica).【F:AOCR.sln†L1-L35】
- Entorno: IIS/Windows Server, DB2 AS/400, PostgreSQL.

**No incluye**
- Reescritura completa de arquitectura (solo refactor incremental).
- Migración tecnológica (por ejemplo, .NET Core).
- Rediseño UX/Front-end completo.

# 3) Priorización (P0/P1/P2)
**Criterios**
- **P0**: Riesgo crítico inmediato (seguridad/finanzas), acceso no autorizado, pérdida de datos, incumplimiento.
- **P1**: Riesgo alto/medio operativo o de trazabilidad; no crítico inmediato pero afecta estabilidad.
- **P2**: Mejora de calidad, deuda técnica y documentación.

**Lista priorizada (mínimo 15 ítems)**
1. **P0** – Endpoints de pago deben requerir rol y CSRF; hoy no hay `[Authorize]` ni anti-forgery y se muta por GET. **Riesgo**: cambios no autorizados. **Capa**: Presentación. **Evidencia**: `PagoController`. **Entregable**: endpoints POST con `[Authorize(Roles="Financiero,Administrador")]` y `[ValidateAntiForgeryToken]`.【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】
2. **P0** – Subida de archivos sin validación real de contenido y en webroot. **Riesgo**: malware/filtración. **Capa**: Presentación. **Evidencia**: `OrdenRecaudacionController`, `SolicitudAOCRController`. **Entregable**: validación MIME+magic bytes, almacenamiento fuera de webroot. 【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L327-L374】【F:CapaPresentacion/Controllers/SolicitudAOCRController.cs†L180-L214】
3. **P0** – Secretos en código y config (DB2/SMTP/PG). **Riesgo**: fuga de credenciales. **Capa**: Datos/Presentación. **Evidencia**: `EmpresaAS400DAO`, `Web.config`. **Entregable**: secretos externos + rotación. 【F:CapaDatos/DAOs/EmpresaAS400DAO.cs†L1-L47】【F:CapaPresentacion/Web.config†L9-L38】
4. **P0** – Errores internos expuestos (`ex.Message`) y `customErrors` Off. **Riesgo**: enumeración de fallas. **Capa**: Presentación. **Evidencia**: `Web.config`, `OrdenRecaudacionController`. **Entregable**: mensajes genéricos + logging interno + `customErrors` On. 【F:CapaPresentacion/Web.config†L42-L78】【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L200-L474】
5. **P0** – Operaciones críticas sin transacción (pago + estado). **Riesgo**: inconsistencia. **Capa**: Datos. **Evidencia**: `OrdenRecaudacionDAO` + flujo en controller. **Entregable**: transacción DB. 【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L385-L399】【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L603-L694】
6. **P0** – Descarga de documentos sin validación de ownership/rol. **Riesgo**: fuga de datos. **Capa**: Presentación. **Evidencia**: `DocumentoController.Descargar`. **Entregable**: autorización por rol/propiedad. 【F:CapaPresentacion/Controllers/DocumentoController.cs†L167-L186】
7. **P0** – Validación de documentos después de guardar (archivo inválido persiste). **Riesgo**: integridad/abuso. **Capa**: Presentación/Negocio. **Evidencia**: `DocumentoController`, `DocumentoBL`. **Entregable**: validar antes de guardar. 【F:CapaPresentacion/Controllers/DocumentoController.cs†L118-L186】【F:CapaNegocio/DocumentoBL.cs†L170-L206】
8. **P1** – Manejo centralizado de excepciones insuficiente. **Riesgo**: fallas sin control. **Capa**: Presentación. **Evidencia**: `FilterConfig` solo `HandleError`. **Entregable**: filtro global + middleware de error. 【F:CapaPresentacion/App_Start/FilterConfig.cs†L1-L14】
9. **P1** – Logging/auditoría sin correlación. **Riesgo**: baja trazabilidad. **Capa**: Datos. **Evidencia**: `Trace` en DAO. **Entregable**: Serilog/NLog con correlation ID. 【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L885-L899】
10. **P1** – Correo síncrono sin cola/reintento. **Riesgo**: latencia/fallos. **Capa**: Datos. **Evidencia**: `EmailService` usa `SmtpClient`. **Entregable**: cola + reintentos. 【F:CapaDatos/Services/EmailService.cs†L1-L236】
11. **P1** – Timeouts/pooling DB2 no configurados en código. **Riesgo**: bloqueos. **Capa**: Datos. **Evidencia**: `EmpresaAS400DAO` sin settings. **Entregable**: timeouts/pooling en connection string y retry. 【F:CapaDatos/DAOs/EmpresaAS400DAO.cs†L1-L47】
12. **P1** – Headers de seguridad no configurados. **Riesgo**: XSS/clickjacking. **Capa**: Presentación. **Evidencia**: `Web.config` sin CSP/HSTS. **Entregable**: headers en IIS/web.config. 【F:CapaPresentacion/Web.config†L1-L120】
13. **P2** – Conflictos NuGet/compatibilidad de PDF libs. **Riesgo**: build inestable. **Capa**: Solution. **Evidencia**: **Pendiente de verificación** (revisar `packages.config`). **Entregable**: consolidación versiones. 【F:CapaPresentacion/packages.config†L1-L40】
14. **P2** – Tests mínimos inexistentes. **Riesgo**: regresiones. **Capa**: Solution. **Evidencia**: **Pendiente de verificación** (no se observa proyecto de pruebas). **Entregable**: smoke tests flujo crítico. 【F:AOCR.sln†L1-L35】
15. **P2** – Documentación y CI/CD faltante. **Riesgo**: despliegues inconsistentes. **Capa**: Solution. **Evidencia**: **Pendiente de verificación** (no README/CI). **Entregable**: README + pipeline. 【F:AOCR.sln†L1-L35】

# 4) Plan 30 días (Semana 1–4)
## Semana 1 (P0): Seguridad mínima + flujo crítico estable
**Objetivo semanal:** cerrar riesgos críticos de acceso no autorizado y exposición de datos.

**Checklist**
- [ ] Convertir `PagoController.Validar/Rechazar` a POST con `[Authorize(Roles="Financiero,Administrador")]` y `[ValidateAntiForgeryToken]`.【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】
- [ ] Activar `customErrors` y eliminar mensajes internos para el usuario final.【F:CapaPresentacion/Web.config†L42-L78】【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L200-L474】
- [ ] Externalizar secretos de `Web.config` y `EmpresaAS400DAO`, y rotar credenciales.【F:CapaPresentacion/Web.config†L9-L38】【F:CapaDatos/DAOs/EmpresaAS400DAO.cs†L1-L47】
- [ ] Upload seguro: validar MIME+magic bytes y guardar fuera de webroot (comprobantes/documentos).【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L327-L374】【F:CapaPresentacion/Controllers/SolicitudAOCRController.cs†L180-L214】

**Archivos/capas afectadas**
- `CapaPresentacion/Controllers`, `CapaPresentacion/Web.config`, `CapaDatos/DAOs`.

**Criterios de aceptación**
- Acciones financieras requieren rol y token CSRF.
- Secretos removidos del repo.
- Archivos sensibles no accesibles por URL directa.

**Riesgos + mitigación**
- Cambio de rutas de archivo → agregar migración/alias temporal y pruebas manuales.

**Dependencias**
- Acceso a credenciales/infra para rotación y configuración segura.

## Semana 2 (P0/P1): Transacciones + SQL parametrizado + manejo de errores
**Objetivo semanal:** asegurar consistencia de datos y manejo robusto de errores.

**Checklist**
- [ ] Transacción DB para `RegistrarPago` + `CambiarEstadoOrden` (pago + estado).【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L385-L399】【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L603-L694】
- [ ] Validar documentos antes de guardar y validar ownership en descargas.【F:CapaPresentacion/Controllers/DocumentoController.cs†L118-L186】【F:CapaPresentacion/Controllers/DocumentoController.cs†L167-L186】
- [ ] Revisión de DAOs para confirmar SQL parametrizado (documentar hallazgos). **Pendiente de verificación**: revisar DAOs restantes además de OrdenRecaudacion. 【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L603-L694】

**Archivos/capas afectadas**
- `CapaDatos/DAOs`, `CapaNegocio`, `CapaPresentacion/Controllers`.

**Criterios de aceptación**
- Pago y estado se registran de forma atómica.
- Descargas solo para usuarios autorizados.
- Checklist de revisión de DAOs con evidencia.

**Riesgos + mitigación**
- Cambios en SQL → pruebas en QA y respaldo antes de despliegue.

**Dependencias**
- Semana 1 completada (seguridad base + config).

## Semana 3 (P1): Logging/auditoría + correo con cola/reintentos
**Objetivo semanal:** visibilidad operativa y resiliencia de notificaciones.

**Checklist**
- [ ] Implementar logging estructurado con correlación por `CodigoSolicitud`/`NumeroOrden`.【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L885-L899】
- [ ] Auditoría de cambios de estado en pagos/órdenes (tabla o log).【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】
- [ ] Desacoplar correo del request con cola/reintentos. **Pendiente de verificación**: definir infraestructura de cola disponible. 【F:CapaDatos/Services/EmailService.cs†L1-L236】

**Archivos/capas afectadas**
- `CapaDatos/Services`, `CapaDatos/DAOs`, `CapaPresentacion`.

**Criterios de aceptación**
- Logs correlados y consultables.
- Envío de correo no bloquea transacciones HTTP.

**Riesgos + mitigación**
- Sobrecarga de logs → definir niveles y retención.

**Dependencias**
- Acceso a infraestructura de logging/cola (Infra/DevOps).

## Semana 4 (P1/P2): NuGet/paquetes + pruebas mínimas + CI/CD + documentación
**Objetivo semanal:** estabilidad de dependencias y publicación del repo.

**Checklist**
- [ ] Auditoría de paquetes NuGet (itext/Select.HtmlToPdf/etc.) y consolidación de versiones. **Pendiente de verificación**: mapear conflictos. 【F:CapaPresentacion/packages.config†L1-L40】
- [ ] Crear pruebas mínimas de flujo crítico. **Pendiente de verificación**: definir framework/pruebas actuales. 【F:AOCR.sln†L1-L35】
- [ ] Pipeline CI/CD básico (restore/build/tests). **Pendiente de verificación**: disponibilidad de GitHub Actions. 【F:AOCR.sln†L1-L35】
- [ ] Documentación (README, despliegue IIS, variables por entorno). **Pendiente de verificación**: no encontrado en repo. 【F:AOCR.sln†L1-L35】

**Archivos/capas afectadas**
- `packages.config`, raíz del repo, pipeline CI.

**Criterios de aceptación**
- Build reproducible en CI.
- Dependencias consolidadas y documentadas.

**Riesgos + mitigación**
- Conflictos de versiones → pruebas de build en QA.

**Dependencias**
- Semanas 1–3 completadas.

# 5) Quick Wins (48 horas)
1. **Cambiar endpoints financieros a POST + CSRF + Authorize**. Validación: intento sin rol debe fallar (403).【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】
2. **Habilitar `customErrors` y mensajes genéricos**. Validación: error intencional no expone stack trace. 【F:CapaPresentacion/Web.config†L42-L78】
3. **Mover comprobantes fuera de webroot**. Validación: URL directa no devuelve archivo. 【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L327-L374】
4. **Validar magic bytes PDF/imagen**. Validación: archivo falso es rechazado. 【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L327-L374】
5. **Agregar control de ownership en descargas**. Validación: usuario no dueño recibe 403. 【F:CapaPresentacion/Controllers/DocumentoController.cs†L167-L186】
6. **Rotar credenciales en AS/400**. Validación: acceso por credenciales antiguas falla. 【F:CapaDatos/DAOs/EmpresaAS400DAO.cs†L1-L47】
7. **Agregar límites de tamaño centralizados**. Validación: archivo >10MB es rechazado. 【F:CapaPresentacion/Web.config†L30-L120】
8. **Registrar auditoría mínima en pagos**. Validación: registro con usuario/fecha queda persistido. 【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】
9. **Revisión rápida de DAOs para SQL parametrizado**. Validación: checklist firmado por dev. 【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L603-L694】
10. **Definir plantilla de despliegue IIS (variables y transforms)**. Validación: config por ambiente documentada. 【F:CapaPresentacion/Web.config†L1-L120】

# 6) Checklist Producción (IIS)
- [ ] Configuración por ambiente (DEV/QA/PROD) con transforms.
- [ ] Secretos fuera del repo (KeyVault/variables IIS).【F:CapaPresentacion/Web.config†L9-L38】
- [ ] Headers de seguridad (CSP, HSTS, X-Frame-Options, X-Content-Type-Options, Referrer-Policy).【F:CapaPresentacion/Web.config†L1-L120】
- [ ] Backups y plan de restauración DB2/PG.
- [ ] Logs centralizados y monitoreo con alertas.
- [ ] Health checks y verificación post-deploy.
- [ ] Plan de rollback documentado.

# 7) Matriz de Riesgos
| Riesgo | Probabilidad | Impacto | Detección | Mitigación | Dueño sugerido |
|---|---|---|---|---|---|
| Acceso no autorizado a aprobación de pagos | Alta | Alto | Revisión de logs y pruebas de rol | `[Authorize]` + CSRF + POST | Dev/QA |
| Fuga de comprobantes en webroot | Media | Alto | Pruebas de URL directa | Mover fuera de webroot + control de acceso | Dev/Infra |
| Fuga de secretos en repo | Alta | Alto | Auditoría de repositorio | Externalizar secretos + rotación | Infra/Dev |
| Inconsistencia pago/estado | Media | Alto | Auditoría y reconciliación | Transacciones DB | Dev |
| Envío de correo fallido en request | Media | Medio | Logs SMTP | Cola/reintentos | Dev/Infra |
| Caídas por timeouts DB2 | Media | Medio | Logs de conexión | Timeouts/pooling + reintentos | Infra |
| Fallos por dependencias NuGet | Media | Medio | CI build | Consolidar versiones | Dev |
| Errores sin manejo central | Media | Medio | Alertas de app | Filtros globales | Dev/QA |

# 8) Definición de “Listo para Producción”
1. Todos los endpoints críticos con `[Authorize]` y CSRF.
2. Secretos fuera del repo y rotados.
3. Subida de archivos validada (MIME + magic bytes) y almacenamiento seguro.
4. Transacciones para operaciones multi-tabla críticas.
5. `customErrors` activo y mensajes genéricos.
6. Logging estructurado con correlación por orden/solicitud.
7. Auditoría mínima de cambios de estado financieros.
8. Pipeline CI/CD básico operativo.
9. Dependencias NuGet consolidadas y documentadas.
10. Checklist de despliegue IIS validado en QA.
