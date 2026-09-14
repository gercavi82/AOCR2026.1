# Resultado parcial — acciones sin aceptación funcional

Rama: `fix/ac04-ac11-ac12-integracion-20260914`. Base: `b81baf365758dd530997529ae6b3115ae37376be`. Se conservó el archivo ajeno sin seguimiento. No hay commit ni despliegue de estos cambios.

## Cambios realizados

| Archivo | Cambio |
|---|---|
| CapaPresentacion/Controllers/CoordinacionJefaturaController.cs | DevolverAlInspector y RemitirADircav rechazan sesión ausente con 401, toman ID de las claves del login y exigen COORDINADOR activo. Eliminados ambos ID=1 predeterminados. La selección documental y las decisiones incluyen devoluciones de DIRCAV. |
| CapaPresentacion/Controllers/RevisionDocumentalController.cs | GuardarRevisionDocumental rechaza identidad inválida y rol activo distinto de INSPECTOR; finalizar sin documentos o con pendientes devuelve 422, sin afirmar finalización. Las decisiones previamente guardadas se conservan. |
| CapaPresentacion/Controllers/DircavController.cs | ObtenerUsuarioIdActual utiliza UserContextAccessor, coherente con UserId/IdUsuario establecidos por AccountController. |
| CapaNegocio/Services/RevisionDocumentalCoordinadorService.cs | Validaciones tempranas de ID, observaciones inválidas sin sustitución silenciosa, regla de completitud y estados documentales compartidos. |
| CapaNegocio/Services/DircavDesignacionService.cs | AceptarDocumentacion y DevolverAlCoordinador rechazan ID ausente y exigen DIRCAV exacto para operaciones nuevas. |
| CapaNegocio/Services/CoordinacionBandejaService.cs | Misma selección de estados para bandeja, contador y decisiones; incluye DEVUELTO_COORDINADOR y DEVUELTO_COORDINADOR_POR_DIRCAV. |
| AOCR.Tests/Unit/Ac04CoordinadorRevisionDocumentalTests.cs | Pruebas ejecutables de sesión, rol activo, identidad, observación, completitud y estados; incluye sesión válida creada conforme al login para evitar un rechazo universal inadvertido. |

No se modificaron DAO, modelos, vistas, JavaScript ni migraciones en esta corrección parcial. No se crearon estados. La reversión de estos cambios es de código; no requiere rollback SQL. No se escribieron datos operativos durante la inspección.

## Evidencias

- Baseline: 726 pruebas correctas (`baseline.trx`), compilación incluida Razor (`baseline-build.log`).
- Resultado final: 756 pruebas correctas, cero fallos (30 casos adicionales); `final-tests.log` y `final.trx`. Compilación correcta en `final-build.log`.
- Las pruebas nuevas ejercitan métodos de controlador/servicio; no son tráfico HTTP contra IIS. No acreditan el comportamiento de Forms Authentication, antiforgery ni respuestas reales 200/404/409/422/500 del sitio.
- Inspección de esquema: `schema.json`, reproducible con `read-schema.py`. Solo metadatos, sin credenciales ni datos de expedientes.
- No se modificó Razor; la última compilación omite repetir la precompilación que pasó en baseline.

## Trabajo necesario antes de aceptar

1. AC-04: unificar estado, decisión, historial, auditoría y cola en transacción; controlar concurrencia/versiones; corregir habilitación técnica prematura y revisar rutas alternativas, permisos específicos y notificaciones.
2. Validar AC-04 en base aislada con expedientes y usuarios de prueba. Está pendiente la identificación del ambiente autorizado solicitada al usuario.
3. AC-11: corregir identidad DIRDAC, revisar el permiso constante de la ruta heredada, completar integración del informe y ambas firmas, versiones y devoluciones después de superar AC-04.
4. AC-12: comprobar relaciones actuales RT-compañía e inspector-asignación, versiones vigentes, entrega y reintentos con ambos adjuntos; revisar mecanismos de cierre paralelos.
5. Ejecutar recorrido completo con certificados de prueba, worker SMTP controlado, IIS bajo /aocr y siete tamaños responsive. Preparar evidencias y entonces solicitar aceptación funcional.

Estas brechas son trabajo pendiente real. Compilar y obtener pruebas unitarias correctas no cierra AC-04, AC-11 ni AC-12.
