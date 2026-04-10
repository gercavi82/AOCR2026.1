# Auditoria y Unificacion del Modulo de Inspeccion AOCR

## Resumen ejecutivo

- El modulo de inspeccion ya tiene una base funcional amplia: estados canonicos, historial, NC, subsanacion, revalidacion, cierre e integracion inicial con OR.
- El principal problema actual no es ausencia total de logica, sino dispersion: controladores, BL y servicios comparten responsabilidades.
- El hueco mas visible frente al flujo objetivo era la falta de correos centralizados por evento de inspeccion. Se cubre con `InspeccionCorreoService` sin eliminar lo existente.

## Compatibilidad diagrama vs proyecto

### Partes que si coinciden

1. Cargar informe de inspeccion.
2. Evaluar resultado satisfactorio o no satisfactorio.
3. Generar no conformidades.
4. Subsanacion del RT sobre documentacion.
5. Revalidacion por inspector.
6. Validacion y cierre por coordinacion.
7. Integracion posterior con habilitacion de OR.
8. Legalizacion/firma final del AOCR en flujo de solicitud, no dentro del controller de inspeccion.

### Partes que no coinciden completamente

1. El flujo de inspeccion no controla directamente el envio a DIRDAC.
2. La firma/legalizacion esta resuelta en `SolicitudAOCRController` y `Direccion/CoordinacionLegal`, no en inspeccion.
3. Las notificaciones del workflow eran internas via `NotificacionBL`, pero no por correo estructurado.
4. El estado `FIRMADA` no existe en inspeccion; existe `AOCR_Legalizado` y `AOCR_EmitidoRecibido` en solicitud.

## Estados recomendados y alineacion real

### Persistidos en inspeccion

- `SOLICITUD_INSPECCION_CREADA`
- `VERIFICACION_SOLICITUD`
- `ACEPTADA`
- `OBSERVADA`
- `SUBSANADA`
- `VIATICOS_REQUERIDOS`
- `PAGO_VALIDADO`
- `EN_INSPECCION`
- `INFORME_ELABORADO`
- `RESULTADO_SATISFACTORIO`
- `RESULTADO_NO_SATISFACTORIO`
- `OBSERVACION_DOCUMENTAL`
- `CERRADA`

### Core de negocio

- `BORRADOR`
- `EN_REVISION`
- `CON_NC`
- `SUBSANACION`
- `REVALIDACION`
- `APROBADA`
- `RECHAZADA`
- `CERRADA`

### Estados relacionados fuera de inspeccion

- Solicitud AOCR: `AOCR_Validado`, `AOCR_Legalizado`, `AOCR_EmitidoRecibido`
- Documentos: `PENDIENTE`, `OBSERVADO`, `ACEPTADO` a nivel de validacion documental y hallazgos
- OR: `GENERADA`, `PAGADA`, `COMPLETADA`

## Flujo unificado propuesto

1. Sistema crea o programa inspeccion.
2. Coordinacion verifica solicitud y acepta/observa.
3. Inspector ejecuta inspeccion y registra informe tecnico.
4. Inspector registra hallazgos/NC cuando aplica.
5. Sistema notifica a RT y coordinacion.
6. RT subsana y carga documentos.
7. Sistema notifica a inspector y coordinacion.
8. Inspector revalida.
9. Si persisten NC, vuelve a subsanacion o se solicita nueva inspeccion.
10. Si todo esta conforme, coordinacion cierra inspeccion.
11. Integracion valida habilitacion de OR.
12. El flujo de solicitud eleva AOCR a legalizacion/firma final.

## Tramo final AOCR en solicitud

### Estados reales del tramo final

1. `AOCR_Validado`
2. `Aprobada`
3. `AOCR_Legalizado`
4. `AOCR_EmitidoRecibido`

### Controladores y acciones reales

1. `DireccionController.ValidacionFinal`
	- exige que la solicitud este en `AOCR_Validado`
	- si aprueba, transiciona a `Aprobada`
2. `DireccionController.Legalizar`
	- exige que la solicitud este en `Aprobada`
	- legaliza y transiciona a `AOCR_Legalizado`
3. `DireccionController.EmitirAOCRConfirm`
	- exige que la solicitud este en `AOCR_Legalizado`
	- transiciona a `AOCR_EmitidoRecibido`
4. `CoordinacionLegalController.GenerarCertificados`
	- trabaja sobre solicitudes legalizadas o emitidas para el tramo documental/legal posterior

### Encaje con DIRDAC

1. No existe un controller separado de `DIRDAC` dentro del modulo de inspeccion.
2. El punto real de salida institucional ocurre en solicitud una vez que inspeccion y validaciones previas dejaron el expediente en condicion `AOCR_Validado`.
3. Si se quiere modelar `ENVIO_DIRDAC`, debe tratarse como hito documental o notificacion del flujo de solicitud, no como nuevo estado operativo de inspeccion.

## BPMN textual

### Swimlane Sistema AOCR

- Crear expediente de inspeccion
- Validar transicion de estado
- Registrar auditoria
- Validar documentos requeridos por etapa
- Encolar correos por evento
- Habilitar OR cuando corresponda

### Swimlane Inspector

- Ejecutar inspeccion
- Registrar informe tecnico
- Generar NC
- Revalidar subsanacion
- Solicitar nueva inspeccion cuando aplique

### Swimlane Coordinador

- Verificar solicitud de inspeccion
- Validar subsanaciones
- Cerrar inspeccion
- Elevar expediente para tramo posterior

### Swimlane RT

- Revisar NC notificadas
- Subsanar no conformidades
- Subir documentos soporte

### Swimlane DIRDAC / Legalizacion

- Revisar expediente AOCR validado
- Legalizar / firmar AOCR
- Notificar cierre final

## Acciones por rol

### Inspector

- Registrar informe de inspeccion
- Registrar no conformidades
- Revalidar subsanacion
- Solicitar nueva inspeccion

### Coordinador / Jefatura

- Verificar solicitud
- Observar o aceptar
- Validar subsanaciones
- Cerrar inspeccion

### RT

- Subsanar no conformidades
- Cargar documentos observados

### Direccion / Coordinacion Legal / Director General

- Legalizar AOCR
- Emitir decision final

## Correos automaticos implementados o alineados

### Politica compartida de destinatarios

- `NotificacionDestinatarioPolicyService` centraliza grupos por evento y rol.
- Inspeccion y solicitud AOCR ya consumen la misma politica.
- La politica deja preparado el grupo financiero para OR y futuras notificaciones transversales.
- Grupos formales actuales:
	- `OPERADOR_SOLICITANTE`
	- `REPRESENTANTE_TECNICO`
	- `INSPECTOR_ASIGNADO`
	- `COORDINACION_INSPECCION`
	- `COORDINACION_LEGAL`
	- `DIRECCION_FINAL`
	- `FINANCIERO`

### Ya existentes ahora en workflow

- `NC_GENERADAS`
- `DOCUMENTOS_SUBSANADOS`
- `DEVOLUCION_INSPECCION`
- `APROBACION_INSPECCION`
- `REVALIDACION_OK`
- `REVALIDACION_RECHAZADA`

### Destinatarios resueltos

- RT: `CorreoRepresentanteTecnico` y fallback a `Email`
- Inspector: por `CodigoTecnico` o `CodigoInspector` cuando resuelven usuario
- Coordinacion: `CoordinadorInspecciones`, `Coordinador`, `JefaturaTecnica`
- Tramo legal/direccion: `CoordinacionLegal`, `CoordinadorLegal`, `DirectorGeneral`, `Direccion`

### Eventos de solicitud AOCR ahora encolados

- `AOCR_APROBADO_DIRECCION`
- `AOCR_LEGALIZADO`
- `AOCR_EMITIDO_RECIBIDO`

## Duplicaciones e inconsistencias detectadas

1. `InspeccionService` todavia conserva logica historica y wrappers hacia `InspeccionWorkflowService`.
2. `InspeccionController` mezcla orquestacion, reglas de rol, armado de ViewBag y carga documental.
3. `UsuarioDAO.ListarPorRol` ya debe priorizar `usuario_rol + rol`; el campo `usuario.rol` solo debe quedar como fallback legacy.
4. El tramo de firma/legalizacion no debe duplicarse en inspeccion, porque ya existe en solicitud.
5. Validacion documental y OR se ejecutan correctamente por servicios, pero no estaban explicitados en correo/eventos.

## Archivos clave del flujo real

- `CapaPresentacion/Controllers/InspeccionController.cs`
- `CapaNegocio/Services/InspeccionService.cs`
- `CapaNegocio/Services/InspeccionWorkflowService.cs`
- `CapaNegocio/Services/ValidacionDocumentalService.cs`
- `CapaNegocio/Services/IntegracionInspeccionOrService.cs`
- `CapaPresentacion/Views/Inspeccion/Detalle.cshtml`
- `CapaDatos/Constants/EstadosInspeccion.cs`
- `CapaDatos/Constants/EstadosInspeccionCore.cs`
- `CapaPresentacion/Controllers/SolicitudAOCRController.cs`
- `CapaPresentacion/Controllers/5_DireccionController.cs`
- `CapaPresentacion/Controllers/CoordinacionLegalController.cs`

## Implementacion incremental recomendada

1. Consolidar el workflow en `InspeccionWorkflowService` como punto unico para eventos de negocio.
2. Mantener `InspeccionService` como facade de compatibilidad.
3. Mantener legalizacion/firma en solicitud y direccion, sin duplicarla en inspeccion.
4. Centralizar correo de inspeccion en `InspeccionCorreoService` con cola no bloqueante.
5. En segunda fase, reemplazar resolucion de destinatarios por rol para usar `usuario_rol` ademas de `usuario.rol`.
6. En tercera fase, incorporar plantillas por evento y adjunto AOCR legalizado al final del flujo de solicitud.

## Riesgos

1. Resolver inspector por `CodigoInspector` puede fallar si ese campo no siempre representa `idusuario`.
2. `ListarPorRol` depende del campo legado `usuario.rol`.
3. No existen correos de aeropuertos involucrados estructurados en el modelo actual.
4. Si el worker de `EmailQueueService` no esta operativo, el encolado no asegura entrega real.

## Resultado esperado tras este ajuste

- Flujo de inspeccion mas alineado con el proyecto real.
- Un solo ciclo de NC en terminos de negocio.
- Correos estructurados y no bloqueantes para eventos criticos del workflow.
- Integracion respetuosa con el resto del sistema sin romper rutas ni estados existentes.