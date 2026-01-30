# 1) Resumen Ejecutivo
- Credenciales sensibles en texto plano para DB2/AS400 en código, riesgo alto de fuga y cumplimiento (acceso directo a sistema legado).【F:CapaDatos/DAOs/EmpresaAS400DAO.cs†L1-L47】
- ConnectionStrings y SMTP con usuario/clave en `Web.config`, expone secretos en repositorio y facilita uso indebido de la BD/correo.【F:CapaPresentacion/Web.config†L9-L38】
- Endpoints de aprobación/rechazo de pagos sin `[Authorize]` ni CSRF y además por GET, permiten cambios no autorizados del estado financiero.【F:CapaPresentacion/Controllers/PagoController.cs†L1-L118】
- Subida de comprobantes/archivos sin validación fuerte de contenido (solo extensión) y guardado en webroot, riesgo de malware y exposición pública de documentos.【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L327-L374】【F:CapaPresentacion/Controllers/SolicitudAOCRController.cs†L180-L214】
- Mensajes de error internos (ex.Message) expuestos y `customErrors` desactivado, facilita enumeración de fallos y explotación dirigida.【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L200-L474】【F:CapaPresentacion/Web.config†L42-L78】
- Validación de documentos ocurre después de guardar archivo, genera ventanas de riesgo y archivos basura/inválidos en el servidor.【F:CapaPresentacion/Controllers/DocumentoController.cs†L118-L186】【F:CapaNegocio/DocumentoBL.cs†L170-L206】
- Descarga de documentos sin chequeo de ownership/rol específico, posible acceso a archivos de terceros por ID.【F:CapaPresentacion/Controllers/DocumentoController.cs†L167-L186】
- Operaciones críticas no son transaccionales (registro pago + cambio estado), dejando inconsistencia ante fallos parciales.【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L385-L399】【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L603-L694】
- Notificaciones por correo existen pero no están integradas al flujo crítico (orden/pago), se pierde trazabilidad al usuario/financiero.【F:CapaDatos/Services/EmailService.cs†L1-L236】
- Observabilidad insuficiente: `Trace` sin correlación y sin auditoría central; dificulta investigación y cumplimiento.【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L885-L899】

# 2) Prioridades P0 / P1 / P2 (Tabla)
| Prioridad | Tema | Hallazgo | Evidencia | Riesgo | Acción recomendada | Esfuerzo |
|---|---|---|---|---|---|---|
| P0 | Secretos/connectionStrings | Cadena AS/400 hardcodeada con credenciales. | `CapaDatos/DAOs/EmpresaAS400DAO.cs` | Seguridad/ Cumplimiento | Mover a secretos por entorno (IIS/KeyVault/variables), rotar credenciales. | M |
| P0 | Secretos/SMTP | `Web.config` incluye usuario/clave de BD y SMTP. | `CapaPresentacion/Web.config` | Seguridad/ Cumplimiento | Usar transforms + vault, eliminar del repo y rotar. | M |
| P0 | Roles/Authorize | PagoController no tiene `[Authorize]` y permite aprobación/rechazo. | `CapaPresentacion/Controllers/PagoController.cs` | Integridad/Seguridad | Restringir a `Financiero/Administrador`, validar ownership. | M |
| P0 | AntiForgery/CSRF | Acciones mutables de pago por GET sin token. | `CapaPresentacion/Controllers/PagoController.cs` | Integridad | Cambiar a POST + `[ValidateAntiForgeryToken]`. | S |
| P0 | Subida de archivos | Guardado en webroot con validación solo por extensión. | `CapaPresentacion/Controllers/OrdenRecaudacionController.cs`, `SolicitudAOCRController.cs` | Seguridad/Integridad | Validar MIME+magic bytes, almacenar fuera de webroot, nombre seguro/hash. | M |
| P0 | Exposición de errores | `customErrors` Off y mensajes con `ex.Message`. | `CapaPresentacion/Web.config`, `OrdenRecaudacionController.cs` | Seguridad/Disponibilidad | Activar `customErrors`, logging interno, mensajes genéricos. | S |
| P0 | Transacciones | Registro de pago y cambio de estado no atómico. | `OrdenRecaudacionController.cs`, `OrdenRecaudacionDAO.cs` | Integridad | Crear transacción DB para pago + cambio estado. | M |
| P0 | SQL Injection | **No encontrado** SQL concatenado en DAOs críticos (usan parámetros). | `CapaDatos/DAOs/OrdenRecaudacionDAO.cs` | Seguridad | Mantener parametrización y revisar DAOs restantes. | S |
| P1 | Excepciones centralizadas | No se observa filtro global robusto; errores dispersos. | `CapaPresentacion/App_Start/FilterConfig.cs` | Disponibilidad | Implementar filtro global + páginas de error. | M |
| P1 | Logging/Auditoría | Uso de `Trace` sin correlación ni auditoría central. | `CapaDatos/DAOs/OrdenRecaudacionDAO.cs` | Cumplimiento/Mantenimiento | Serilog/NLog + correlación por orden/solicitud. | M |
| P1 | Correo (cola/reintento) | Envío SMTP síncrono y sin cola; no integrado al flujo. | `CapaDatos/Services/EmailService.cs` | Disponibilidad | Implementar cola/reintentos y desacoplar del request. | M |
| P1 | Timeouts/Pooling | DB2 AS/400 sin timeout/pooling configurado en código. | `CapaDatos/DAOs/EmpresaAS400DAO.cs` | Disponibilidad | Configurar timeouts/pooling y políticas de reintento. | M |
| P1 | NuGet/Versiones | **No encontrado** análisis de conflictos; requiere revisión de packages.config. | `CapaPresentacion/packages.config`, `CapaDatos/packages.config` | Mantenimiento | Consolidar versiones y revisar dependencias duplicadas. | M |
| P2 | Refactor incremental | Lógica de negocio en Controllers (subida, validaciones). | `DocumentoController.cs`, `OrdenRecaudacionController.cs` | Calidad | Mover validaciones a BL/servicios y usar interfaces. | L |
| P2 | Tests mínimos | **No encontrado** proyecto de pruebas. | Solución | Calidad | Crear smoke tests para flujos críticos. | M |
| P2 | Documentación/CI | **No encontrado** README/CI/CD. | Raíz del repo | Mantenimiento | Agregar README, pipeline de build/test, templates. | M |

# 3) Plan de Acción 30 días (por semanas)
## Semana 1: Acciones P0 urgentes (con checklist)
**Objetivo:** Cerrar riesgos críticos de seguridad y exposición.

**Tareas (checklist)**
- [ ] Extraer secretos de `Web.config` y `EmpresaAS400DAO` a variables/KeyVault; rotar credenciales.【F:CapaPresentacion/Web.config†L9-L38】【F:CapaDatos/DAOs/EmpresaAS400DAO.cs†L1-L47】
- [ ] Cambiar `PagoController.Validar/Rechazar` a POST con `[Authorize]` y `[ValidateAntiForgeryToken]` (roles financiero).【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】
- [ ] Activar `customErrors` y eliminar mensajes `ex.Message` hacia el usuario final.【F:CapaPresentacion/Web.config†L42-L78】【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L200-L474】
- [ ] Política de subida: validar MIME+magic bytes y guardar fuera de webroot para comprobantes/documentos.【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L327-L374】【F:CapaPresentacion/Controllers/SolicitudAOCRController.cs†L180-L214】

**Archivos/capas afectadas**
- `CapaPresentacion` (Controllers, Web.config), `CapaDatos` (DAOs/AS400).

**Criterios de aceptación**
- Ningún secreto queda en repo.
- Endpoints financieros requieren rol y token CSRF.
- Errores ya no exponen detalles internos.
- Archivos sensibles no son accesibles por URL directa.

**Riesgos y mitigación**
- Riesgo de downtime por cambios de config → usar transforms por ambiente y despliegue gradual.

## Semana 2: Integridad de datos/transacciones + estabilización
**Objetivo:** Evitar inconsistencias en operaciones críticas.

**Tareas (checklist)**
- [ ] Transaccionar pago + cambio de estado de orden (DAO).【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L385-L399】【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L603-L694】
- [ ] Validar documentos antes de guardar físicamente (mover validación a BL).【F:CapaPresentacion/Controllers/DocumentoController.cs†L118-L186】【F:CapaNegocio/DocumentoBL.cs†L170-L206】
- [ ] Agregar chequeo de ownership/rol en descargas de documentos.【F:CapaPresentacion/Controllers/DocumentoController.cs†L167-L186】

**Archivos/capas afectadas**
- `CapaDatos` (DAO), `CapaNegocio` (BL), `CapaPresentacion` (Controllers).

**Criterios de aceptación**
- Pago + cambio de estado son atómicos.
- No se guarda archivo inválido.
- Descargas autorizadas por rol/propietario.

**Riesgos y mitigación**
- Cambios de flujo → pruebas de regresión manuales en entorno QA.

## Semana 3: Observabilidad + correo robusto
**Objetivo:** Trazabilidad y resiliencia de notificaciones.

**Tareas (checklist)**
- [ ] Implementar logging estructurado y auditoría por orden/solicitud.【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L885-L899】
- [ ] Integrar correo del flujo (orden/pago) y añadir cola/reintentos.【F:CapaDatos/Services/EmailService.cs†L1-L236】

**Archivos/capas afectadas**
- `CapaDatos/Services`, `CapaPresentacion` (Controllers), configuración de logging.

**Criterios de aceptación**
- Logs correlados por orden/solicitud.
- Envíos de correo no bloquean requests y tienen reintentos.

**Riesgos y mitigación**
- Sobrecarga de logging → definir niveles y retención.

## Semana 4: Calidad + CI + documentación + cierre de deuda técnica
**Objetivo:** Estabilizar el ciclo de entrega y documentación.

**Tareas (checklist)**
- [ ] Crear pipeline CI (restore/build/tests) y documentar despliegue IIS.【F:AOCR.sln†L1-L35】
- [ ] Agregar README y guía de configuración por ambientes. (No encontrado en repo)
- [ ] Definir pruebas mínimas de flujo crítico (orden/pago/documento). (No encontrado en repo)

**Archivos/capas afectadas**
- Raíz del repo, proyectos de solución.

**Criterios de aceptación**
- Pipeline ejecuta build y checks básicos.
- Documentación publicada y reproducible.

**Riesgos y mitigación**
- Falta de tiempo → priorizar documentación y pipeline básico.

# 4) Quick Wins (48 horas)
1. Convertir `PagoController.Validar/Rechazar` a POST + CSRF + `[Authorize]`.【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】
2. Activar `customErrors` y ocultar mensajes internos.【F:CapaPresentacion/Web.config†L42-L78】【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L200-L474】
3. Mover credenciales de `EmpresaAS400DAO` a config segura.【F:CapaDatos/DAOs/EmpresaAS400DAO.cs†L1-L47】
4. Cambiar carpeta de comprobantes a fuera del webroot (App_Data).【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L327-L374】
5. Validar firma PDF (magic bytes) antes de guardar documentos en `DocumentoController`.【F:CapaPresentacion/Controllers/DocumentoController.cs†L118-L186】
6. Validar ownership en `DocumentoController.Descargar`.【F:CapaPresentacion/Controllers/DocumentoController.cs†L167-L186】
7. Añadir tamaño máximo centralizado para uploads (config).【F:CapaPresentacion/Web.config†L30-L120】
8. Registrar evento de pago validado/rechazado en auditoría (tabla/log).【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】
9. Consolidar roles (Solicitante/Financiero) en endpoints críticos.【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L12-L279】
10. Integrar notificación de pago al solicitante usando `EmailService`.【F:CapaDatos/Services/EmailService.cs†L135-L175】

# 5) Reglas de Oro para evitar regresiones
1. Toda acción que muta datos debe ser POST + CSRF.
2. Endpoints financieros deben requerir rol explícito.
3. Nunca guardar archivos sin validar extensión + MIME + firma.
4. Archivos sensibles se almacenan fuera del webroot.
5. Transacciones para operaciones multi-tabla.
6. No exponer `ex.Message` al usuario final.
7. Secretos siempre fuera del repo y rotados por entorno.
8. Logs con correlación por `CodigoSolicitud`/`NumeroOrden`.
9. DAOs solo con SQL parametrizado.
10. Controllers sin lógica de negocio pesada (mover a BL).
11. Correo debe ir a cola y nunca bloquear el request.
12. Cambios de dependencias se validan con pipeline CI.
