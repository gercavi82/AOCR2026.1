# 1) Preparación (antes de tocar código)
- [ ] **Crear rama hotfix**: `git checkout -b hotfix/estabilizacion-aocr`.
- [ ] **Backup/restore de BD (si aplica)**:
  - PostgreSQL: `pg_dump` y prueba de `pg_restore` en entorno QA.
  - DB2 AS/400: **Pendiente de verificación**: confirmar procedimiento de backup con Infra y documentarlo.
- [ ] **Limpieza y build**:
  - Eliminar `bin/` y `obj/` de cada proyecto.
  - `nuget restore` / `msbuild /t:Rebuild /p:Configuration=Release`.
- [ ] **Capturar log de compilación**:
  - Guardar salida de build en archivo (`msbuild ... > build.log`).
  - Listar errores con rutas y líneas.

**Validación**: build Release finaliza sin errores; `build.log` archivado con timestamp.

# 2) Fase 1: Que compile y ejecute (P0)
**Objetivo:** Build Release limpio, sin conflictos de paquetes.

**Pasos**
- [ ] **Revisar NuGet**: consolidar versiones de librerías PDF (iTextSharp/Select.HtmlToPdf) y dependencias comunes. **Pendiente de verificación**: revisar conflictos en `packages.config` y en referencias de proyectos.【F:CapaPresentacion/packages.config†L1-L40】
- [ ] **Eliminar referencias duplicadas/incompatibles**: revisar `*.csproj` con referencias repetidas o versiones mezcladas. **Pendiente de verificación**.
- [ ] **Rebuild Release**: confirmar que no hay errores.

**Criterios de aceptación**
- Build Release exitoso (cero errores).
- Paquetes NuGet consolidados (documentar versiones finales).

# 3) Fase 2: Estabilizar el flujo crítico end-to-end (P0)
**Objetivo:** Flujo orden→comprobante→financiero→factura→correo funciona de punta a punta en DEV.

**Identificar flujo principal**
- Orden: `OrdenRecaudacionController` (crear/estado/descargar PDF).【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L1-L474】
- Pago/financiero: `PagoController` (validar/rechazar).【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】
- PDF: `PdfGeneratorService` (generación).【F:CapaPresentacion/Services/PdfGeneratorService.cs†L1-L159】
- Email: `EmailService` (envío).【F:CapaDatos/Services/EmailService.cs†L1-L236】

**Pasos**
- [ ] **Mover lógica de Controllers a servicios de Negocio**: extraer validaciones de pago/archivo a `CapaNegocio` y dejar Controllers como orquestadores ligeros.【F:CapaPresentacion/Controllers/OrdenRecaudacionController.cs†L281-L399】【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】
- [ ] **Definir servicio orquestador** (ej. `OrdenRecaudacionOrchestrator` en `CapaNegocio`) con entradas/salidas claras: crear orden, registrar pago, generar PDF, notificar. **Pendiente de verificación**: diseñar interface y DTOs.

**Validación**
- Flujo completo en DEV con datos reales de prueba (orden→pago→PDF→correo).

# 4) Fase 3: Seguridad obligatoria (P0)
**Objetivo:** cierre de riesgos críticos de seguridad.

**CSRF / AntiForgery**
- [ ] Añadir `[ValidateAntiForgeryToken]` en todos los POST sensibles (`PagoController`, `DocumentoController`, `OrdenRecaudacionController`).【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】【F:CapaPresentacion/Controllers/DocumentoController.cs†L200-L239】

**Roles y autorización**
- [ ] Restringir acciones financieras a roles `Financiero/Administrador`.【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】

**Validación de modelos**
- [ ] Validar `ModelState` y sanitizar salidas en Views (usar `Html.Encode`).【F:CapaPresentacion/Filters/SecurityFilters.cs†L33-L63】

**Subida de archivos segura**
- [ ] Validar tamaño, extensión, MIME y magic bytes.
- [ ] Renombrar con GUID + hash.
- [ ] Guardar fuera del webroot (ej. App_Data o storage dedicado).
- [ ] Prevenir path traversal (Path.GetFileName + ruta fija).
- [ ] Registrar metadatos en BD (nombre, hash, tamaño, tipo).

**Secretos/configuración**
- [ ] Extraer credenciales de `Web.config` y `EmpresaAS400DAO` a variables/KeyVault y rotar claves.【F:CapaPresentacion/Web.config†L9-L38】【F:CapaDatos/DAOs/EmpresaAS400DAO.cs†L1-L47】
- [ ] Aplicar transforms por ambiente.

**Headers recomendados**
- [ ] Configurar CSP, HSTS, X-Frame-Options, X-Content-Type-Options, Referrer-Policy.【F:CapaPresentacion/Web.config†L1-L120】

**Validación**
- Escaneo manual de endpoints (sin rol no se puede aprobar/rechazar).
- Upload de archivo falso se rechaza.
- No hay secretos en repo.

# 5) Fase 4: Integridad de datos (P0)
**Objetivo:** operaciones críticas consistentes y SQL seguro.

- [ ] **SQL parametrizado** en todos los DAOs. **Pendiente de verificación**: revisar DAOs fuera de OrdenRecaudacion. 【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L603-L694】
- [ ] **Transacciones** para multi-tabla (crear orden + detalle + documentos + estados).【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L603-L694】
- [ ] **Conexiones** con `using`, timeouts y pooling; manejar errores sin filtrar detalles internos.
- [ ] **Compatibilidad DB2/PG**: separar responsabilidades y documentar qué entidades viven en cada base. **Pendiente de verificación**.

**Validación**
- Pruebas de fallo parcial no dejan estado inconsistente.

# 6) Fase 5: Robustez operativa (P1)
- [ ] **Manejo centralizado de errores** (filtro global, páginas de error, no stacktrace en producción).【F:CapaPresentacion/Web.config†L42-L78】【F:CapaPresentacion/App_Start/FilterConfig.cs†L1-L14】
- [ ] **Logging estructurado** con correlación por `NumeroOrden/CodigoSolicitud` (Serilog/NLog).【F:CapaDatos/DAOs/OrdenRecaudacionDAO.cs†L885-L899】
- [ ] **Auditoría** de cambios de estado (quién/cuándo/qué) para pagos y órdenes.【F:CapaPresentacion/Controllers/PagoController.cs†L64-L118】

**Validación**
- Logs correlados y consultables; auditoría disponible para cambios de estado.

# 7) Fase 6: Correo y PDF resilientes (P1)
- [ ] **Correo fuera del request**: implementar cola simple (EmailQueue) + reintentos. **Pendiente de verificación**: definir infraestructura. 【F:CapaDatos/Services/EmailService.cs†L1-L236】
- [ ] **PDF controlado**: validar datos de entrada, manejar errores, registrar en BD la generación. **Pendiente de verificación**.

**Validación**
- Envíos de correo no bloquean requests; reintentos auditados.
- PDF generado con registro de metadatos.

# 8) Fase 7: Evitar regresiones (P2)
- [ ] **Tests mínimos** (5–10) para flujo crítico. **Pendiente de verificación**: framework actual de pruebas. 【F:AOCR.sln†L1-L35】
- [ ] **Checklist IIS** (DEV/QA/PROD) documentado.
- [ ] **CI/CD** con restore/build/test y artefactos. **Pendiente de verificación**.

**Validación**
- Tests pasan en CI; checklist IIS firmado por QA/Infra.

# 9) Checklist Final “Listo para Producción”
- [ ] Endpoints críticos con `[Authorize]`.
- [ ] CSRF activo en todos los POST sensibles.
- [ ] Secrets fuera del repo y rotados.
- [ ] Upload seguro (MIME+magic bytes+hash+fuera webroot).
- [ ] Descargas con ownership/rol.
- [ ] Transacciones en operaciones multi-tabla.
- [ ] SQL parametrizado en todos los DAOs.
- [ ] Timeouts/pooling configurados.
- [ ] `customErrors` activo y mensajes genéricos.
- [ ] Headers de seguridad configurados (CSP/HSTS/etc.).
- [ ] Logging estructurado con correlación.
- [ ] Auditoría de cambios de estado.
- [ ] Correo con cola/reintentos.
- [ ] PDF con registro de metadatos.
- [ ] Backups DB2/PG automatizados.
- [ ] Procedimiento de restore probado.
- [ ] Health check implementado.
- [ ] Monitoreo y alertas activas.
- [ ] Plan de rollback documentado.
- [ ] CI/CD operativo.
- [ ] Tests mínimos del flujo crítico pasan.
- [ ] README y guía de despliegue IIS.
- [ ] Variables por ambiente documentadas.
- [ ] Auditoría de dependencias NuGet completada.
- [ ] QA validó flujo end-to-end en entorno controlado.
