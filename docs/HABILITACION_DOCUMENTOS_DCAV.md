# Habilitación automática de documentos tras aprobación DCAV

## Alcance

Este cambio P0 cubre exclusivamente la transición:

`PENDIENTE_REVISION_INFORME_DCAV -> DOCUMENTOS_HABILITADOS_INSPECTOR`

No implementa segunda revisión DCAV, firma DIRDAC, envío al RT ni cierre del expediente.

## Diagnóstico y causa raíz

El flujo anterior de `AocrDcavRevisionService.AprobarInformeTecnico` cambiaba primero el estado y luego intentaba generar AOCR y Condiciones con servicios independientes. Los errores de documento se registraban como advertencias y la operación continuaba. Cada DAO abría su propia conexión, por lo que `TransactionScope` no representaba una transacción PostgreSQL única. Además, la búsqueda se limitaba a la solicitud, usaba tipos de borrador distintos a los consumidos por Firma AOCR y no garantizaba inspección, compañía, inspector, vigencia o versión.

La generación de PDF es una operación posterior sobre un borrador habilitado. No crea el derecho funcional a revisar un documento ni reemplaza la creación/recuperación del registro editable.

## Flujo implementado

`HabilitacionDocumentosFinalesService` abre una conexión y una transacción `SERIALIZABLE`, bloquea solicitud, inspección, informe y estado central, y valida autenticación, rol DCAV, estado/versiones esperados, vigencia del informe, firmas del informe y LV/EAE, resultado satisfactorio, inspector, compañía y solicitud activa.

Dentro de esa misma transacción:

1. consulta la clave idempotente;
2. obtiene o crea `RECONOCIMIENTO`;
3. obtiene o crea `CONDICIONES_LIMITACIONES`;
4. asigna ambos al inspector y los deja en `EN_REVISION_INSPECTOR`;
5. registra aprobación del informe por DCAV;
6. ejecuta el servicio central de transición y su historial;
7. registra una auditoría funcional;
8. crea una notificación interna idempotente;
9. registra los IDs de resultado en idempotencia;
10. confirma la transacción.

El correo al usuario real del inspector se encola después del commit. Cualquier excepción anterior produce rollback, por lo que no puede quedar confirmado un solo documento ni un estado parcial.

## Tablas reales utilizadas

| Responsabilidad | Tabla |
|---|---|
| Solicitud/compañía | `aocr_tbsolicitud` |
| Inspección/inspector | `aocr_tbinspeccion` |
| Informe técnico/versiones/firma | `aocr_tbinforme_inspeccion` |
| LV/EAE/versiones/firma | `aocr_tblv_operacional_eae` |
| Borradores y PDF AOCR/Condiciones | `aocr_tbdocumento_generado` |
| Estado central | `aocr_proceso_estado` |
| Historial | `aocr_proceso_estado_historial` |
| Idempotencia | `aocr_proceso_idempotencia` |
| Auditoría | `aocr_tbauditoria` |
| Campana interna | `aocr_tbnotificacion` |
| Cola de correo | `email_queue` |

## Inventario y duplicados

El script [`scripts/004_habilitacion_documentos_dcav.sql`](../scripts/004_habilitacion_documentos_dcav.sql) contiene consultas de inventario y duplicados que no eliminan ni corrigen registros. El despliegue aborta antes de crear el índice único si detecta más de un documento vigente para solicitud + inspección + tipo.

La verificación de lectura desde esta sesión no pudo completarse porque el host de PowerShell no aplicó los redirects de ensamblado de Npgsql de la aplicación. No se alteró la base configurada ni se declara un resultado de cero duplicados sin evidencia. El reporte SQL debe ejecutarse y conservarse como gate obligatorio del despliegue.

## Bandeja y contador del inspector

La bandeja central incluye `DOCUMENTOS_HABILITADOS_INSPECTOR` y solo muestra una fila cuando el inspector asignado posee el par vigente AOCR + Condiciones. El contador invoca la misma consulta de bandeja. La vista Firma AOCR muestra dos bloques independientes con estado, versión, fecha de habilitación, edición/guardado de datos, previsualización y generación de PDF.

## Despliegue

1. Respaldar las tablas afectadas.
2. Ejecutar primero el reporte de duplicados incluido al final de `004_habilitacion_documentos_dcav.sql`.
3. Si devuelve filas, detener el despliegue y resolver cada expediente mediante decisión funcional; no borrar automáticamente.
4. Ejecutar `scripts/001_estado_central_upgrade.sql` si aún no fue aplicado.
5. Ejecutar `scripts/004_habilitacion_documentos_dcav.sql` en una transacción de despliegue.
6. Publicar las capas Datos, Negocio y Presentación.
7. Probar una aprobación controlada y un reintento con la misma clave; ambos deben devolver los mismos IDs.
8. Confirmar que bandeja y contador del inspector coinciden.

## Reversión

Revertir el binario a la versión anterior y eliminar solamente los índices nuevos indicados al final del script. No eliminar columnas ni documentos, historial, auditoría, notificaciones o claves creadas mientras el cambio estuvo activo. La reversión de datos requiere análisis por expediente.

## Verificación

- Compilación: `MSBuild AOCR.sln /t:Build /p:Configuration=Debug`.
- Pruebas focalizadas: `HabilitacionDocumentosFinalesTests` cubre los 24 escenarios solicitados, incluidas validaciones funcionales y contratos de integración entre coordinador, DAO, transición, bandeja, contador y vista.
- Logs operativos: buscar los prefijos `[DCAV]`, `[AOCR]`, `[CONDICIONES]`, `[WORKFLOW]` e `[IDEMPOTENCY]` descritos en el requerimiento.
