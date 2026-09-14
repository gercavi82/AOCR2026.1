# Diagnóstico AC-04, AC-11 y AC-12 — 2026-09-14

## Línea base

- Rama inicial: `fix/cierre-flujo-aocr`.
- Commit inicial: `b81baf365758dd530997529ae6b3115ae37376be`.
- Rama de trabajo: `fix/ac04-ac11-ac12-integracion-20260914`.
- Árbol inicialmente con un archivo ajeno sin seguimiento: `-tree -r --name-only HEAD`. Conservado.
- Compilación Debug de AOCR.Tests y referencias, incluida precompilación Razor: correcta; advertencia previa de compatibilidad de itext.commons.
- Pruebas iniciales: 726 correctas, filtro `FullyQualifiedName~AOCR.Tests.Unit&TestCategory!=Integration`. Evidencias: baseline-build.log, baseline-tests.log, baseline.trx.
- No se ejecutaron pruebas que escriben en la BD configurada. Fr3OutboxIntegrationTests contiene un destino predeterminado y ejecuta DDL desde Setup. Se solicitó identificar el ambiente autorizado.

## Inventario y flujo encontrado

| Componente | Archivos/clases principales |
|---|---|
| Inspector documental | CapaPresentacion/Controllers/RevisionDocumentalController.cs, DocumentoController.cs |
| Inspector técnico | CapaPresentacion/Controllers/InspeccionController.cs |
| Coordinador | CapaPresentacion/Controllers/CoordinacionJefaturaController.cs |
| Autoridades separadas | CapaPresentacion/Controllers/DircavController.cs, DirdacController.cs |
| Estados y bandejas | CapaNegocio/Services/AocrEstadoService.cs, AocrFlujoService.cs, InspectorBandejaService.cs, CoordinacionBandejaService.cs, DircavBandejaService.cs, DirdacBandejaService.cs |
| Revisión documental | CapaNegocio/Services/RevisionDocumentalCoordinadorService.cs, CapaDatos/DAOs/RevisionDocumentalCoordinadorDAO.cs, SolicitudAOCRDAO.cs |
| Firma y documentos | CapaPresentacion/Controllers/FirmaAocrController.cs; CapaNegocio/Services/FirmaDigitalService.cs, DocumentoFirmaService.cs, CondicionesLimitacionesService.cs, DocumentosFinalesWorkflowService.cs, AocrFinalWorkflowService.cs |
| Persistencia final | CapaDatos/DAOs/AocrFinalWorkflowDAO.cs, DocumentosFinalesWorkflowDAO.cs, AocrDocumentoGeneradoDAO.cs, AocrFirmaDocumentoDAO.cs, CondicionesLimitacionesDAO.cs, EntregaFinalDAO.cs |
| Correo | CapaNegocio/Services/SolicitudAocrCorreoService.cs (encola), EntregaFinalService.cs; EncolarAUsuariosRol en AocrFinalWorkflowDAO |
| Modelos | CapaDatos/Models/RevisionDocumentalCoordinadorRegistro.cs; CapaModelo/EntregaFinalModels.cs, DocumentosFinalesResultado.cs; CapaPresentacion/Models/ViewModels/RevisionDocumentalIndexViewModel.cs |
| Vistas y JS embebido | CapaPresentacion/Views/RevisionDocumental/Index.cshtml; Views/Dircav/{Bandeja,Detalle,RevisionCl}.cshtml; Views/Dirdac/{Bandeja,Detalle}.cshtml; Views/RT/DocumentosFinales.cshtml; Views/Inspeccion/DocumentosFinales.cshtml; Views/CoordinacionJefatura |
| Autorización | CapaPresentacion/Filters/{SecurityFilters,RequirePermissionAttribute,AocrAjaxAuthorizeAttribute,ValidateAntiForgeryTokenFromHeaderAttribute}.cs |
| Navegación | CapaPresentacion/Services/SidebarMenuService.cs; Helpers/SidebarMenuBuilder.cs, SidebarPermissionHelper.cs; CapaNegocio/Services/AocrSidebarCounterService.cs |
| Tablas referenciadas | aocr_tbsolicitud, aocr_revision_documental_coordinador, aocr_inspector_reasignacion_historial, aocr_tbhistorialestado / aocr_tbhistorial_estado, aocr_tbdocumento_generado, aocr_tbfirma_documento, aocr_tbcondiciones_limitaciones, aocr_entrega_final, aocr_entrega_documento, aocr_entrega_destinatario |
| Migraciones existentes | scripts/sql/20260722_revision_documental_coordinador.sql; 014_flujo_final_documentos_independientes*.sql; 20260904_ac11_flujo_legalizacion*.sql; 20260904_ac12_entrega_final*.sql; 20260908_ac12_reparar_permisos.sql |
| Pruebas | AOCR.Tests/Unit/Ac04CoordinadorRevisionDocumentalTests.cs, Ac04MatrizPruebasFlujoInspectorCoordinadorDireccionTests.cs, Ac11LegalizacionWorkflowTests.cs, Ac12EntregaFinalTests.cs, DocumentosFinalesWorkflowTests.cs, AocrFinalWorkflowAuthorizationTests.cs |

Flujo documental encontrado: Inspector guarda decisiones → registra finalización/oficio → cambia solicitud a PENDIENTE_COORDINADOR → Coordinador devuelve a DEVUELTO_INSPECTOR o remite a PENDIENTE_DIRCAV → DIRCAV acepta documentación. Estas operaciones están repartidas entre conexiones y no constituyen una única transacción.

Flujo final encontrado: servicios de C&L → remisión explícita de AOCR a DIRDAC → firma AOCR → FIRMAS_COMPLETAS → solicitud de entrega → LISTO_PARA_ENTREGA y cola. Hay implementaciones paralelas de documentos finales que requieren verificar qué ruta usa cada vista antes de unificarlas.

## Hallazgos comprobados en código antes de cambios

| Archivo / clase / método | Problema y consecuencia | Corrección necesaria |
|---|---|---|
| CoordinacionJefaturaController.DevolverAlInspector / RemitirADircav | Usa UsuarioId=1 cuando falla el contexto; puede atribuir decisiones a otro usuario. | Rechazar con 401 antes de consultar el expediente; identidad exclusivamente de sesión. |
| RevisionDocumentalController.GuardarRevisionDocumental | Finalizar con pendientes entra en guardado parcial pero responde éxito de finalización; no hay rechazo inicial explícito de UsuarioId ausente. | Responder 422 si quedan pendientes y 401 sin identidad; mantener guardado parcial identificable. |
| RevisionDocumentalCoordinadorService.RemitirADircav | NormalizarObservacion puede devolver null para HTML o exceso de longitud y sustituirse por texto predeterminado. | Rechazar datos inválidos antes de consultar o escribir. |
| RevisionDocumentalCoordinadorService.DevolverAlInspector / RemitirADircav | CambiarEstado y RegistrarDecision usan conexiones independientes; resultado de RegistrarDecision ignorado. | Transacción única con bloqueo del expediente, estado esperado, decisión, historial y outbox. |
| RevisionDocumentalCoordinadorDAO.RegistrarDecision | Al aceptar habilita LV e Informe y modifica asignación; mezcla revisión del Coordinador con habilitación reservada a la aceptación/designación DIRCAV. | Separar remisión documental de habilitación técnica y conservar historial. |
| RevisionDocumentalCoordinadorDAO.RegistrarFinalizacionInspector | UPSERT modifica fecha/observación en repetición; estado de solicitud se cambia fuera de esta transacción. | Control de concurrencia y reintento por versión en una transacción. |
| SolicitudAOCRDAO.CambiarEstado | UPDATE sin estado esperado; dos solicitudes concurrentes pueden prosperar. | Añadir operación transaccional específica para AC-04 sin alterar consumidores ajenos. |
| DircavDesignacionService.EsDircavAutorizado / AceptarDocumentacion | Acepta alias DCAV; aceptación usa Actualizar y auditoría separada con catch vacío; identidad no validada al inicio. | Rol operativo DIRCAV exacto, identidad positiva, aceptación/retorno atómicos. |
| DircavController (Authorize) | Permite DIRCAV, DCAV y Administrador a nivel de clase; algunos métodos aplican comprobaciones adicionales. | Revisar todas las acciones y separar consulta de operaciones; no usar alias como autoridad nueva. |
| SolicitudAocrCorreoService.NotificarEvento | Usa cola, pero se invoca después del cambio y se ignoran fallos desde AC-04. No se observó SMTP directo en estos métodos. | Encolar dentro de la misma transacción de decisión, SMTP posterior. |
| EntregaFinalDAO.Solicitar | Bloquea y encola ambos destinatarios dentro de transacción; comprueba versiones y archivos. Falta demostrar integración real y reintentos. | Pruebas con BD aislada, PDFs firmados y worker controlado. |
| EntregaFinalDAO.ListarDocumentos / AutorizarDescarga | Autorización usa destinatario histórico y compañía opcional; no se ve revalidación de asignación vigente ni catálogo RT-compañía actual en estas consultas. | Validar relaciones actuales en backend, incluyendo revocación y cambio de inspector. |
| Ac04MatrizPruebasFlujoInspectorCoordinadorDireccionTests | Varias pruebas comprueban constantes o presencia de cadenas, no ejecutan el recorrido ni concurrencia. | Incorporar pruebas de comportamiento y de persistencia; no equiparar 726 verdes con aceptación funcional. |

No se confirmó todavía una llamada que fuerce la decisión del usuario mediante un booleano constante. El `true` de ConstruirResumenRevisionDocumental es formato del resumen, no evidencia por sí sola de una transición forzada. El origen del Inspector en AC-04 sí apunta a Coordinador; la atomicidad y los caminos alternativos siguen siendo brechas.

## Restricciones de aceptación

No se considera terminada ninguna acción. Faltan validación del esquema desplegado y migraciones aplicadas, pruebas transaccionales, recorrido con los cinco roles, certificados de prueba, worker SMTP y prueba visual en las siete resoluciones. No avanzar al cierre AC-12 mientras AC-04 siga sin validación funcional.

## Comprobación posterior del esquema y de la sesión

Se consultaron exclusivamente metadatos de la conexión configurada en Web.config, en sesión PostgreSQL read-only. `schema.json` contiene columnas e índices; `read-schema.py` permite repetir la inspección sin imprimir la conexión. Existe `aocr_tbhistorial_estado`; no existe `aocr_tbhistorialestado`. Las demás tablas seleccionadas sí existen. Esto no acredita que todas las migraciones estén aplicadas ni autoriza usar esta base para pruebas con escrituras.

AccountController, al establecer la sesión, guarda el ID interno en `UserId` e `IdUsuario`, y el login en `CodigoUsuario`. Por ello se reutilizó UserContextAccessor.TryGetUserId para las decisiones del Coordinador y para ObtenerUsuarioIdActual de DircavController. El helper anterior de DIRCAV leía `UsuarioId`, que no es la clave establecida por el login inspeccionado.

Hallazgos adicionales pendientes de la fase AC-11: DirdacController.CrearActor lee `UsuarioId` o convierte `CodigoUsuario` a entero, en vez de las claves reales del login. DircavController.DevolverAocrDirdac construye el actor con `TienePermiso=true`; aunque el servicio impone rol DIRDAC y puede rechazarlo, ese permiso constante debe eliminarse al revisar la ruta. No se atribuye a este hallazgo una aprobación operativa exitosa sin prueba.
