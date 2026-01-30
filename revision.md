# Resumen Ejecutivo
El repositorio contiene una solución en capas (MVC + BL + DAO + modelos) con dos proyectos web MVC (AOCR y CapaPresentacion) y capas de datos/modelos/negocio/utilidades. El flujo crítico de órdenes/recaudación está principalmente en `CapaPresentacion/Controllers/OrdenRecaudacionController.cs`, `CapaDatos/DAOs/OrdenRecaudacionDAO.cs`, generación de PDF en `CapaPresentacion/Services/PdfGeneratorService.cs` y notificaciones por correo en `CapaDatos/Services/EmailService.cs` (no integrado al flujo).【F:AOCR.sln†L1-L35】【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L1-L474】【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L1-L909】【F:CapaPresentacion/Services/PdfGeneratorService.cs†L1-L159】【F:CapaDatos/Services/EmailService.cs†L1-L236】

Riesgos críticos detectados: credenciales en texto plano (DB2 AS/400 hardcodeado y PostgreSQL/SMTP en `Web.config`), endpoints de pago sin autorización/CSRF, subida de archivos sin validaciones robustas ni aislamiento del webroot y manejo de errores que filtra mensajes internos. Estos puntos impactan seguridad, cumplimiento y disponibilidad. Se recomienda mitigar de forma incremental con hardening de seguridad web, refactor de carga de archivos y uso de secretos por entorno.【F:CapaDatos/DAOs/EmpresaAS400DAO.cs†L1-L47】【F:CapaPresentacion/Web.config†L1-L120】【F:CapaPresentacion/Controllers/PagoController.cs†L1-L118】【F:CapaPresentacion/Controllers/SolicitudAOCRController.cs†L180-L214】【F:CapaPresentacion/Controllers/DocumentoController.cs†L98-L186】

# Arquitectura Actual (por proyectos/capas)
- **AOCR (MVC5)**: Proyecto web MVC adicional con controllers, views, scripts y configuración propia (`AOCR/Web.config`). Posible legado/duplicado de `CapaPresentacion`.【F:AOCR.sln†L1-L35】
- **CapaPresentacion (MVC5)**: UI, Controllers, Views, Filters y Services. Aquí vive gran parte del flujo de órdenes, documentos y pagos (`OrdenRecaudacionController`, `DocumentoController`, `PagoController`, `SolicitudAOCRController`).【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L1-L474】【F:CapaPresentacion/Controllers/DocumentoController.cs†L1-L263】【F:CapaPresentacion/Controllers/PagoController.cs†L1-L118】【F:CapaPresentacion/Controllers/SolicitudAOCRController.cs†L1-L214】
- **CapaNegocio (BL)**: Lógica de negocio y validaciones. Ejemplo: `DocumentoBL` valida extensiones y estados de la solicitud antes de persistir.【F:CapaNegocio/DocumentoBL.cs†L1-L206】
- **CapaDatos (DAO/Repositories/Services)**: Acceso a datos PostgreSQL (Npgsql/Dapper), DB2 AS/400 (IBM iSeries), servicios de correo y PDF. Ej.: `OrdenRecaudacionDAO`, `EmpresaAS400DAO`, `EmailService`.【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L1-L909】【F:CapaDatos/DAOs/EmpresaAS400DAO.cs†L1-L47】【F:CapaDatos/Services/EmailService.cs†L1-L236】
- **CapaModelo**: Modelos/entidades del dominio (Solicitud, Orden, Pago, Documento, etc.).【F:AOCR.sln†L1-L35】
- **CapaUtilidades**: Proyecto utilitario (ClassLibrary1) referenciado en la solución; no se revisó en detalle (no se encontraron archivos clave de flujo).【F:AOCR.sln†L1-L35】

**Flujo principal (Controllers/BL/DAO/Utilidades):**
- **Controllers**: `OrdenRecaudacionController` (creación/estado/pagos/descarga PDF), `PagoController` (validación/rechazo de pagos), `DocumentoController` (subida/aprobación/rechazo), `SolicitudAOCRController` (solicitud + documentos + pago).【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L1-L474】【F:CapaPresentacion/Controllers/PagoController.cs†L1-L118】【F:CapaPresentacion/Controllers/DocumentoController.cs†L1-L263】【F:CapaPresentacion/Controllers/SolicitudAOCRController.cs†L1-L214】
- **BL**: `DocumentoBL` valida tamaño/extensión y estados de solicitud (pero guarda archivo antes de validar).【F:CapaNegocio/DocumentoBL.cs†L85-L206】【F:CapaPresentacion/Controllers/DocumentoController.cs†L118-L186】
- **DAO**: `OrdenRecaudacionDAO` (PostgreSQL), `PagoDAO` (pagos), `EmpresaAS400DAO` (DB2).【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L1-L909】【F:CapaDatos/DAOs/EmpresaAS400DAO.cs†L1-L47】

# Flujo Principal (end-to-end) + riesgos por paso
> Caso crítico evaluado: **Orden de Recaudación / Pago / PDF / Notificaciones**.

1) **Solicitante crea orden/solicitud**
- **Entrada**: datos de orden y usuario desde `OrdenRecaudacionController` (Nueva/Editar/Generar).【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L61-L210】
- **Salida**: registro en `aocr_or_orden` (DAO) y estado `BORRADOR` → `GENERADA`.【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L139-L210】【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L603-L626】
- **Validaciones**: `ModelState` en Editar; estados y totales antes de Generar.【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L120-L210】
- **Persistencia**: Npgsql parametrizado en `OrdenRecaudacionDAO`.【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L603-L626】
- **Puntos de falla**: uso de `Session` para identidad (puede expirar), ausencia de transacciones al cambiar estado y operación de negocio (separado). Mitigar con transacción y validaciones de autorización basadas en identidad/claims.

2) **Solicitante sube comprobante**
- **Entrada**: `RegistrarPago` recibe monto, factura, método y archivo `ComprobanteArchivo`.【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L281-L399】
- **Salida**: archivo guardado en `~/Content/documents/pagos` y registro en `aocr_tbpago`.【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L327-L374】【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L634-L694】
- **Validaciones**: extensión (PDF/JPG/PNG) y tamaño 10MB; no valida MIME/magic bytes ni se normaliza nombre más allá de timestamp; almacenamiento en webroot expone archivos por URL directa.【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L327-L374】
- **Persistencia**: `RegistrarPago` en DAO; sin transacción conjunta con cambio de estado a `PAGADA` (riesgo de inconsistencia).【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L385-L399】【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L603-L694】
- **Riesgos**: exposición de comprobantes, falta de validación del contenido real, y rollback parcial. Mitigar con validación MIME+firma, almacenamiento fuera de webroot y transacción (pago + estado).

3) **Notificación a financiero**
- **No encontrado en flujo**: No hay llamada explícita para notificar a financiero tras registrar pago; existe `EmailService` pero no está integrado en el controlador. Se recomienda integrar un evento/servicio de notificación y/o cola de mensajes.【F:CapaDatos/Services/EmailService.cs†L1-L236】

4) **Financiero aprueba o rechaza**
- **Entrada**: `PagoController.Validar/Rechazar` cambia `Estado` del pago y registra usuario/fecha. Usa GET sin `[Authorize]` ni CSRF.【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】
- **Salida**: actualización de pago vía BL/DAO (no muestra cambio en estado de orden).【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】
- **Riesgos**: aprobación/rechazo sin autorización ni token CSRF; endpoints mutables por GET. Mitigar con `[Authorize(Roles="Financiero")]`, `[HttpPost]` y `[ValidateAntiForgeryToken]`.

5) **Generación de factura (PDF) y adjunto**
- **Entrada**: `DescargarPDF` solicita datos a DAO y genera PDF con `PdfGeneratorService`.【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L452-L474】【F:CapaPresentacion/Services/PdfGeneratorService.cs†L1-L159】
- **Salida**: PDF retornado al usuario; no se persiste a disco ni se adjunta a correo en el flujo actual. Para adjuntar, existe `EmailService` con capacidad de adjunto, pero no se invoca en el flujo.【F:CapaDatos/Services/EmailService.cs†L1-L236】
- **Riesgos**: sin auditoría de descargas; riesgo de exfiltración si no se valida ownership (en este endpoint se valida por usuario en DAO).【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L735-L799】

6) **Notificación al solicitante y habilitación del siguiente paso**
- **No encontrado**: hay `EnviarNotificacionPago` en `EmailService`, pero no hay evidencia de uso. No se observa cambio de estado de orden tras validación financiera en `PagoController`.【F:CapaDatos/Services/EmailService.cs†L135-L175】【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】
- **Mitigación**: integrar notificación + transición de estado (ej. `PAGADA` → `VALIDADA`), y registrar auditoría.

# Hallazgos Prioritizados (tabla P0/P1/P2)
| Prioridad | Título | Qué encontraste (ruta/archivo) | Riesgo | Recomendación concreta | Esfuerzo |
|---|---|---|---|---|---|
| **P0** | Credenciales hardcodeadas DB2 AS/400 | `CapaDatos/DAOs/EmpresaAS400DAO.cs` contiene cadena con usuario/clave en texto plano. | Seguridad/ Cumplimiento | Mover a secretos por ambiente (variables/IIS config transforms), cifrar en repositorio y rotar credenciales. | M |
| **P0** | Secretos y credenciales en Web.config | `CapaPresentacion/Web.config` expone user/pass PostgreSQL y SMTP. | Seguridad/ Cumplimiento | Usar `configSource`/transform + secretos por ambiente; eliminar del repo y rotar. | M |
| **P0** | Endpoints de pago sin Auth/CSRF y mutaciones por GET | `CapaPresentacion/Controllers/PagoController.cs` no tiene `[Authorize]`, usa GET para `Validar/Rechazar` y no hay anti-forgery. | Seguridad/Integridad | Agregar `[Authorize(Roles="Financiero")]`, `[HttpPost]`, `[ValidateAntiForgeryToken]`, y verificar ownership. | M |
| **P0** | Subida de archivos sin validación real de contenido y en webroot | `OrdenRecaudacionController.RegistrarPago` guarda en `~/Content/documents/pagos`; `SolicitudAOCRController.ProcesarArchivos` guarda en `~/Uploads/AOCR`. | Seguridad/Integridad | Validar MIME + magic bytes, renombrar con GUID/Hash, almacenar fuera de webroot y servir vía endpoint autorizado. | M |
| **P0** | Mensajes de error internos expuestos | `OrdenRecaudacionController` y `PagoController` retornan `ex.Message`; `Web.config` con `customErrors` Off. | Seguridad/Disponibilidad | Habilitar `customErrors` y logging interno; devolver mensajes genéricos. | S |
| **P0** | Contraseña/hash por defecto fijo para registros | `UsuarioController` asigna hash constante para nuevos usuarios. | Seguridad | Implementar flujo de creación segura (reset inicial, hash per-user, salt). | M |
| **P1** | Validación tardía de documentos (guarda antes de validar) | `DocumentoController.Subir` guarda archivo antes de `DocumentoBL.ValidarDocumento`. | Integridad | Validar extensión/tamaño y firma antes de escribir disco. | S |
| **P1** | Descarga de documentos sin chequeo de ownership/rol | `DocumentoController.Descargar` no valida propietario ni rol, solo busca por ID. | Seguridad | Validar autorización por solicitud y rol antes de descargar. | M |
| **P1** | Aprobación financiera no actualiza estado de orden | `PagoController.Validar/Rechazar` no cambia estado en `aocr_or_orden`. | Integridad | Actualizar estado en transacción o evento (p.ej. `PAGADA` → `VALIDADA/RECHAZADA`). | M |
| **P1** | Falta de headers de seguridad | `Web.config` no configura CSP/HSTS/XFO/X-CTO. | Seguridad | Agregar headers en `web.config`/middleware. | S |
| **P1** | Falta de políticas de cookie (Secure/HttpOnly/SameSite) | Configuración de forms auth no muestra flags. | Seguridad | Configurar `requireSSL`, `httpOnlyCookies`, `SameSite`. | S |
| **P2** | Logging y auditoría incompletos | `OrdenRecaudacionDAO` usa `Trace` sin correlación; no hay logs centralizados. | Mantenimiento | Implementar Serilog/NLog con correlación por orden/solicitud. | M |
| **P2** | Estructura duplicada de proyectos web | `AOCR` y `CapaPresentacion` coexisten; riesgo de divergencia. | Mantenimiento | Consolidar y eliminar duplicados con plan de migración. | L |

# Recomendaciones de Seguridad
- **AntiForgery y validación de modelos**: asegurar `[ValidateAntiForgeryToken]` en todos los POST de mutación (especialmente `PagoController.Validar/Rechazar`, `DocumentoController.CambiarEstado`) y aplicar `ValidateModelAttribute` donde proceda.【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】【F:CapaPresentacion/Controllers/DocumentoController.cs†L200-L239】【F:CapaPresentacion/Filters/SecurityFilters.cs†L33-L63】
- **Roles/Authorize**: endurecer roles para operaciones financieras y de revisión documental; `PagoController` carece de `[Authorize]` y debe restringirse a `Financiero/Administrador`.【F:CapaPresentacion/Controllers/PagoController.cs†L1-L118】
- **Headers de seguridad**: configurar CSP, HSTS, X-Frame-Options, X-Content-Type-Options, Referrer-Policy en `Web.config`/IIS (no se observan).【F:CapaPresentacion/Web.config†L1-L120】
- **Sanitización en Views**: asegurar `@Html.Encode` o helpers al renderizar datos del usuario; el EmailService ya sanitiza HTML, pero no es usado en UI.【F:CapaDatos/Services/EmailService.cs†L223-L233】
- **Cookies**: activar `requireSSL`, `httpOnlyCookies`, y `SameSite` en forms auth; actualmente no están definidos en `Web.config`.【F:CapaPresentacion/Web.config†L30-L120】
- **Endoints de descarga**: proteger descargas (Documentos/PDF) con verificación de propietario y rol; `DocumentoController.Descargar` no valida autorización específica.【F:CapaPresentacion/Controllers/DocumentoController.cs†L167-L186】

# Recomendaciones de Datos y Transacciones
- **Parametrización**: la mayoría de DAOs usa parámetros Npgsql (buena práctica). Mantener y extender a consultas DB2 si se parametrizan en el futuro (actualmente son consultas fijas).【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L603-L694】【F:CapaDatos/DAOs/EmpresaAS400DAO.cs†L16-L40】
- **Transacciones**: operaciones de pago + cambio de estado (`RegistrarPago` y `CambiarEstadoOrden`) deben ser transaccionales para evitar inconsistencias en caso de fallos parciales.【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L385-L399】【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L603-L694】
- **Timeouts**: se ve `CommandTimeout=60` en `Web.config` para PostgreSQL, pero DB2 no tiene configuración de timeout/pooling en código. Agregar parámetros y manejo de reintentos para AS/400.【F:CapaPresentacion/Web.config†L9-L26】【F:CapaDatos/DAOs/EmpresaAS400DAO.cs†L1-L47】
- **Interfaces/UnitOfWork**: aunque existe `IOrdenRecaudacionDAO`, otros DAOs no exponen interfaces. Recomendado uniformar para test/DI.【F:CapaDatos/DAOs/IOrdenRecaudacionDAO.cs†L1-L33】

# Recomendaciones de Observabilidad
- **Logging estructurado**: reemplazar `System.Diagnostics.Trace` por Serilog/NLog con correlación (CódigoSolicitud/NumeroOrden). El DAO ya deja comentarios para logging centralizado pero no está implementado.【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L885-L899】
- **Auditoría**: asegurar registro de cambios de estado (aprobación/rechazo de pagos) y adjuntar usuario/fecha. Actualmente se setean campos en `PagoController`, pero sin registro centralizado y sin correlación a la orden.【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】
- **Errores**: habilitar `customErrors` y un filtro global de excepciones; hoy está `customErrors` en `Off` y hay mensajes con `ex.Message` al usuario final.【F:CapaPresentacion/Web.config†L42-L78】【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L200-L474】

# Recomendaciones para Publicación del Repo
- **README.md completo**: no se encontró README en raíz; agregar setup, prerequisitos (.NET Framework 4.7.2), DB2/AS400, PostgreSQL, variables por ambiente y pasos de despliegue.【F:AOCR.sln†L1-L35】
- **CONTRIBUTING / LICENSE / CHANGELOG / CODE_OF_CONDUCT**: no encontrados; agregar plantillas para PR/Issues y licencia. (No encontrado en el repo).
- **.editorconfig / .gitignore**: no evidenciados; agregar estándares de formato y exclusiones de bin/obj/secretos. (No encontrado en el repo).
- **Eliminar secretos**: mover cadenas de conexión y SMTP fuera del repositorio; aplicar transformaciones por entorno en `Web.config`.【F:CapaPresentacion/Web.config†L9-L38】【F:CapaDatos/DAOs/EmpresaAS400DAO.cs†L8-L18】
- **Documentación de arquitectura**: agregar diagramas de capas y flujo (IIS + DB2/PG). Referenciar `OrdenRecaudacionController` y `OrdenRecaudacionDAO` como flujo base.【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L1-L474】【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L1-L909】

# Checklist de Producción (con casillas)
- [ ] Secretos fuera del repo (DB2/PG/SMTP) y rotados.【F:CapaDatos/DAOs/EmpresaAS400DAO.cs†L8-L18】【F:CapaPresentacion/Web.config†L9-L38】
- [ ] CSRF en POSTs críticos (`PagoController`, `DocumentoController`).【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】【F:CapaPresentacion/Controllers/DocumentoController.cs†L200-L239】
- [ ] Roles y autorización por acción (financiero vs solicitante).【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L12-L279】【F:CapaPresentacion/Controllers/PagoController.cs†L1-L118】
- [ ] Validación de archivos (tamaño, extensión, magic bytes, almacenamiento fuera de webroot).【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L327-L374】【F:CapaPresentacion/Controllers/SolicitudAOCRController.cs†L180-L214】
- [ ] Headers de seguridad (CSP, HSTS, XFO, XCTO, Referrer-Policy).【F:CapaPresentacion/Web.config†L1-L120】
- [ ] Logs estructurados y auditoría por Orden/Solicitud.【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L885-L899】
- [ ] Pipeline CI/CD (restore/build/tests) + análisis estático.
- [ ] Backups DB + DR + monitoreo.

# Plan de Acción 30 días (Semana 1–4 con entregables)
**Semana 1**
- Inventario de secretos y extracción de `Web.config`/`EmpresaAS400DAO` a variables/KeyVault. Entregable: config por ambiente y secretos rotados.【F:CapaPresentacion/Web.config†L9-L38】【F:CapaDatos/DAOs/EmpresaAS400DAO.cs†L8-L18】
- Hardening básico: `customErrors` y mensajes genéricos; logging central mínimo.【F:CapaPresentacion/Web.config†L42-L78】【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L885-L899】

**Semana 2**
- Refactor de endpoints financieros: `[Authorize]`, `[HttpPost]`, `[ValidateAntiForgeryToken]`, y cambio de estado transaccional (pago + orden).【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L281-L399】
- Política de carga de archivos (validación MIME/magic bytes + almacenamiento fuera de webroot).【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L327-L374】【F:CapaPresentacion/Controllers/SolicitudAOCRController.cs†L180-L214】

**Semana 3**
- Integración de notificaciones: usar `EmailService` para notificar a financiero y solicitante; registrar auditoría y eventos de estado.【F:CapaDatos/Services/EmailService.cs†L1-L236】
- Implementar headers de seguridad en IIS/Web.config y políticas de cookies seguras.【F:CapaPresentacion/Web.config†L30-L120】

**Semana 4**
- CI/CD (GitHub Actions): build, tests, análisis estático y artefactos; documentación de despliegue IIS con transforms por ambiente.【F:AOCR.sln†L1-L35】
- Documentación pública: README + diagramas + plantillas de issues/PR. (No encontrado en repo).
