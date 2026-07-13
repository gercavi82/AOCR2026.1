# Envio formal conjunto de documentos a DCAV

## Diagnostico y causa raiz

El flujo anterior tenia dos operaciones independientes: generar los PDF y ejecutar `AocrDcavRevisionService.EnviarRevisionDcav`. La segunda validaba indicadores generales, cambiaba solamente el estado central mediante `TransactionScope` y llamaba a una notificacion. No actualizaba `aocr_tbdocumento_generado`, no registraba las versiones/PDF exactos, no tenia clave idempotente propia y permitia estados intermedios creados por el generador legado.

Metodos involucrados: `InspectorDocumentosFinalesController.GenerarPdfAocr`, `GenerarPdfCondiciones`, el antiguo `AocrDcavController.EnviarRevisionDcav`, `AocrDcavRevisionService.EnviarRevisionDcav`, `AocrEstadoProcesoService.CambiarEstado`, `AocrDcavDAO.ObtenerPendientesRevisionDocumentos` y `AocrProcesoNotificacionService.NotificarDocumentosPendientesRevisionDcav`.

Despues de generar, cada documento quedaba `GENERADO`, pero el proceso podia permanecer habilitado o pasar a `DOCUMENTOS_EN_REVISION_INSPECTOR`. La bandeja DCAV consulta `PENDIENTE_REVISION_DOCUMENTOS_DCAV`; por ello generar no garantizaba visibilidad. Dos solicitudes HTTP o un fallo entre operaciones podian producir envio parcial, historial incompleto o notificacion repetida.

La refactorizacion establece un solo comando backend que vuelve a resolver solicitud, inspeccion, Inspector, documentos y PDF vigentes. No confia en los identificadores de JavaScript y compara los enviados exclusivamente como control de concurrencia.

## Implementacion

`IEnvioDocumentosDcavService.FinalizarYEnviar` ejecuta una transaccion Npgsql `SERIALIZABLE` con advisory lock por solicitud/inspeccion. Valida proceso, Informe aprobado, LV/EAE firmada, campos funcionales, compania, Inspector, versiones, firma, estado, metadatos PDF, archivo fisico, tamano y SHA-256.

La clave es:

`Solicitud:Inspeccion:AocrId:VersionAocr:CondicionesId:VersionCondiciones:ENVIAR_DOCUMENTOS_DCAV`

Dentro de la misma transaccion:

1. bloquea y resuelve el expediente;
2. detecta reintentos;
3. actualiza ambos documentos exactos a `ENVIADO_DCAV`;
4. crea el estado central `PENDIENTE_REVISION_DOCUMENTOS_DCAV` con version optimista;
5. registra historial con IDs, versiones, hashes, IP, correlation e idempotencia;
6. registra los eventos de auditoria;
7. crea notificaciones idempotentes para usuarios activos `DIRECTOR_CERTIFICACIONES_DCAV`;
8. registra idempotencia y confirma.

El correo se encola despues del commit usando exclusivamente correos reales de usuarios activos del rol. Un fallo de cola no revierte un envio ya confirmado y queda en log.

`ENVIADO_DCAV` no pertenece a los estados editables ni generables, por lo que ambos documentos quedan bloqueados hasta una devolucion formal de DCAV.

## Interfaz

La unica accion visible es `FINALIZAR REVISION Y ENVIAR A DCAV` en `/aocr/InspectorDocumentosFinales/Detalle`. Exige confirmacion y deshabilita el boton en el primer clic. Las pantallas heredadas solo enlazan a la seccion exclusiva y el endpoint anterior esta marcado `NonAction`.

La bandeja DCAV usa el estado central canonico y muestra AOCR/Condiciones con sus versiones y la accion `Revisar`. El contador documental del sidebar usa `AocrDcavDAO.ObtenerPendientesRevisionDocumentos`, la misma consulta base de la bandeja.

## Despliegue

1. Respaldar `aocr_proceso_estado`, `aocr_proceso_estado_historial`, `aocr_proceso_idempotencia`, `aocr_tbdocumento_generado`, `aocr_tbdocumento_inspeccion`, `aocr_tbauditoria` y `aocr_tbnotificacion`.
2. Confirmar que las fases 004 y 006 estan aplicadas.
3. Ejecutar `scripts/007_envio_documentos_dcav.sql` en QA. El script aborta ante dependencias ausentes o documentos vigentes duplicados.
4. Ejecutar el diagnostico PDF administrador y exigir cero hallazgos.
5. Probar envio, doble clic, reintento y rollback inducido.
6. Desplegar binarios y reciclar el Application Pool.

## Reversion

`scripts/007_envio_documentos_dcav_rollback.sql` elimina los indices de esta fase y revierte solo estados intermedios marcados por la migracion. No revierte paquetes enviados realmente: hacerlo requiere una devolucion funcional auditada desde DCAV, nunca SQL manual.

## Logs

Los logs se agrupan bajo `[INSPECTOR_DCAV]`, `[IDEMPOTENCY]` y `[CONCURRENCY]`, incluyendo entrada, validaciones, bloqueo, estado, notificacion, commit, error y rollback.

## Alcance excluido

No se implemento decision final DCAV, firma DIRDAC, cierre final, correo final al RT, AS400, finanzas ni rediseño general.
