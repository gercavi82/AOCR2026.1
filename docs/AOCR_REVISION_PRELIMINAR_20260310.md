# AOCR - Revision Preliminar Funcional/Tecnica (2026-03-10)

## 1) Resumen funcional de diagramas (base BPMN)

### Emision/Renovacion AOCR
1. Explotador inicia solicitud AOCR.
2. Adjunta documentacion.
3. DGAC revisa documentacion.
4. Si hay novedades: observa.
5. Explotador subsana y reingresa documentacion.
6. DGAC acepta documentalmente.
7. Se ejecuta fase de inspeccion.
8. AOCR pasa por elaboracion, revision, validacion y legalizacion.
9. AOCR se emite/recibe y se cierra.

### Inspecciones
1. Se crea solicitud de inspeccion.
2. DGAC asigna inspector responsable.
3. Se verifica solicitud y se acepta/rechaza.
4. Se gestiona planificacion.
5. Si aplica, se solicitan viaticos.
6. Se valida pago.
7. Se ejecuta inspeccion.
8. Se elabora informe.
9. Se registra resultado satisfactorio/no satisfactorio.
10. Se cierra la inspeccion o se generan observaciones para subsanacion.

## 2) Mapa de modulos AOCR (estado actual)

### Modulos principales
- Solicitud AOCR (frontend wizard + backend FormularioCompleto).
- Gestion documental (DocumentoDAO + uploads App_Data).
- Flota/Aeronaves (AeronaveSolicitudDAO).
- Historial de estados (HistorialEstadoDAO).
- Asignacion de inspectores (TecnicoController + SolicitudAOCRDAO.AsignarInspectores).
- Inspecciones (InspeccionController/BL/DAO + vistas Crear/Editar/Detalle/Planificacion).
- AOCR final (acciones de transicion en SolicitudAOCRController).
- Notificaciones base (correo en eventos clave).

### Integracion externa
- DB2 AS400 OPINSPECTORES/OPIAR2 via `InspectorAS400DAO`.
- DB2 AS400 companias via `EmpresaAS400DAO`.
- PostgreSQL para persistencia transaccional AOCR.

## 3) Analisis GAP (diagrama vs implementacion)

### Implementado o cubierto
- Guardado atomico de `FormularioCompleto` con rollback logico y limpieza de archivos.
- Correccion de compatibilidad de columnas en aeronaves (`created_at`, `codigo_solicitud`/`codigosolicitud`).
- Insercion de documentos resiliente al `chk_estado_documento`.
- Tolerancia controlada ante ausencia/incompatibilidad de historial de estados.
- Selector de tecnico/inspector desde OPINSPECTORES (activos, filtro `OPS/AIR/TODOS`, persistencia en solicitud e inspeccion).
- Validacion backend de tipo de inspector (`OPS`/`AIR`) en `InspeccionController` (ya no solo filtro visual en frontend).
- Flujo AOCR con transiciones server-side y control por rol.
- Bloqueo de avance AOCR final sin inspeccion satisfactoria.
- Flujo de viaticos y resultado en inspecciones (acciones backend + UI de detalle).

### Parcial
- Matriz de transiciones de inspeccion esta normalizada al core (`CREADA...CERRADA`) y mapea aliases BPMN.
- AOCR usa estados BPMN y legacy en paralelo para compatibilidad.
- Notificaciones existen en eventos principales, pero falta consolidar estrategia unica por canal/plantilla.

### Pendiente para cierre total
- Matriz formal unica de permisos por rol/estado para todas las acciones de AOCR e inspeccion.
- Catalogo formal de documentos requeridos por fase (hoy hay reglas, pero falta centralizacion completa por etapa).
- End-to-end de aprobacion financiera/viaticos con evidencias de pago y trazabilidad completa de validacion.
- Pruebas automaticas de regresion funcional por transiciones de estado.

## 4) Diseno tecnico objetivo (aplicado en esta iteracion)

### Estados AOCR
- `Solicitud Creada`
- `Documentacion Pendiente`
- `Observada`
- `Subsanada`
- `Aceptacion Documental`
- `En Inspeccion`
- `AOCR En Elaboracion`
- `AOCR En Revision`
- `AOCR Validado`
- `AOCR Legalizado`
- `AOCR Emitido/Recibido`

### Estados inspeccion (core operativo)
- `CREADA`, `PROGRAMADA`, `EN_CURSO`, `APLAZADA`, `FINALIZADA`, `APROBADA`, `RECHAZADA`, `CANCELADA`, `CERRADA`.
- Alias BPMN (ej. `VIATICOS_REQUERIDOS`, `RESULTADO_SATISFACTORIO`) se normalizan en capa BL.

### Persistencia DB (ajustes idempotentes)
- `scripts/sql/20260310_aocr_formulario_completo_hardening.sql`
- `scripts/sql/20260310_aocr_inspectores_opiar2.sql`
- `scripts/sql/20260310_aocr_flujo_bpmn_base.sql`
- `scripts/sql/20260310_aocr_diagnostico_esquema.sql` (diagnostico de columnas/constraints/indices)

### Selector de tecnico responsable
- Fuente obligatoria: `OPIAR2`.
- Campos usados: `OPICED`, `OPINO2`, `OPIES1`, `OPITIP`.
- Filtros: `OPIES1='AC'` + tipo `OPS`/`AIR` segun contexto.
- Endpoint reusable: `Tecnico/ListarInspectoresActivos`.
- Persistencia extendida: solicitud (`tecnico_responsable_*`) + inspeccion (`inspector_principal_*`, `inspector_apoyo_*`, incluyendo tipo cuando existe columna).

## 5) Implementacion realizada por bloques

### Documental y formulario completo
- `SolicitudAOCRController.FormularioCompleto`: flujo atomico, errores HTTP coherentes (400/500), normalizaciones y validaciones.
- `AeronaveSolicitudDAO`: insercion dinamica compatible con esquemas heterogeneos.
- `DocumentoDAO`: resolucion dinamica de estado compatible con check constraint.
- `HistorialEstadoDAO`: tolerancia controlada a esquemas legacy/canonicos.

### Inspecciones
- `InspeccionController`: acciones para viaticos, validacion de pago y resultado.
- `InspeccionBL`: validacion de transiciones.
- `InspeccionDAO`: soporte de columnas de viaticos/resultado, persistencia de inspector principal/apoyo AS400 y resolucion de estado compatible con el `CHECK` real (`chk_estado_inspeccion`) en modo core o BPMN.
- Vistas actualizadas: `Crear`, `Editar`, `Detalle`, `Index`.

### AOCR final
- Reglas de transicion por rol en `SolicitudAOCRController`.
- Bloqueo de `MarcarAocrEnElaboracion` y `EmitirAocr` si no existe inspeccion satisfactoria.

### Inspector/tecnico responsable
- `InspectorAS400DAO` reusable para consultas activas por tipo y busqueda por cedula.
- `TecnicoController` y `SolicitudAOCRDAO.AsignarInspectores` integrados con OPINSPECTORES.

## 6) QA minimo para demo/revision preliminar

### Casos obligatorios AOCR
- Guardar solicitud con 0 aeronaves.
- Guardar solicitud con 2 aeronaves.
- Guardar documentos validos sin violar `chk_estado_documento`.
- Confirmar que no falle por `created_at` en aeronaves.
- Confirmar manejo controlado de historial cuando tabla no exista/sea legacy.
- Confirmar rollback funcional si falla guardado de componentes criticos.

### Casos obligatorios inspeccion
- Asignar inspector principal/apoyo desde OPINSPECTORES (sin inactivos).
- Filtrar inspectores por `OPS`, `AIR`, `TODOS`.
- Solicitar viaticos y validar pago.
- Registrar resultado satisfactorio/no satisfactorio.
- Verificar transiciones de estado permitidas.

### Casos obligatorios AOCR final
- Intentar emitir AOCR sin inspeccion satisfactoria (debe bloquear).
- Emitir AOCR con inspeccion satisfactoria (debe permitir).

## 7) Checklist de revision preliminar

- Scripts SQL aplicados en ambiente de pruebas.
- Build limpio sin errores (warnings aceptados).
- Flujo AOCR completo ejecutado hasta estado emitido/recibido.
- Flujo inspeccion ejecutado con resultado y cierre.
- Selector OPINSPECTORES operativo, sin datos hardcodeados.
- Mensajeria de error/estado coherente para frontend.

## 8) Diagnostico real de esquema (PostgreSQL)

Diagnostico ejecutado con `scripts/dev/SchemaProbeNet` (2026-03-10):

- `aocr_tbaeronave_solicitud`
  - existe `codigosolicitud` (no `codigo_solicitud`)
  - no existe `created_at`
  - existe `fecha_registro`, `usuario_registro`
- `aocr_tbdocumento`
  - existe `created_at`, `created_by`
  - constraint `chk_estado_documento` permite: `Cargado`, `En Revisión`, `Aprobado`, `Rechazado`, `Subsanado`
- Historial
  - existe `aocr_tbhistorial_estado`
  - no existe `aocr_tbhistorialestado`
- `aocr_tbinspeccion`
  - constraint activo `chk_estado_inspeccion` con estados core (`CREADA`, `PROGRAMADA`, `EN_CURSO`, etc.)
  - no estaban las columnas de inspector principal/apoyo en ese ambiente al momento del diagnóstico
- `aocr_tbsolicitud`
  - contiene datos base de solicitud, pero no todas las columnas extendidas de inspector/técnico según el nuevo flujo

Implicación técnica:
- se mantiene compatibilidad runtime en DAOs con resolución dinámica de columnas
- los scripts SQL idempotentes son obligatorios para alinear entornos a BPMN extendido

## 9) Fases ejecutadas (orden solicitado)

### Fase 1 - Resumen de diagramas y mapa funcional
- Definidos en secciones 1 y 2.
- Flujo AOCR y flujo de inspecciones consolidados con estados objetivo.

### Fase 2 - GAP diagrama vs implementación
- Ejecutado en sección 3.
- Se detectó inconsistencia principal en inspecciones por mezcla de estados core/BPMN.

### Fase 3 - Arquitectura funcional/técnica final
- Ejecutada en sección 4.
- Se definió modelo con trazabilidad por estados + historial + selector institucional AS400.

### Fase 4 - Implementación por módulos (estado actual)
- Documental/formulario completo: transaccional y endurecido.
- Inspecciones: normalización BPMN en BL/DAO, transición por rol en backend, acciones y vistas alineadas.
- Asignación de técnico/inspectores: OPINSPECTORES/OPIAR2, filtro activo y por tipo OPS/AIR.
- AOCR final: bloqueo sin inspección satisfactoria + transiciones controladas.
- Historial/estados: fallback controlado por tabla legacy/canónica.
- Notificaciones: base operativa en hitos de cambio.

### Fase 5 - QA, checklist y criterios de revisión preliminar
- Definidos en secciones 6 y 7.
- Cobertura mínima para demo funcional ya estructurada.
