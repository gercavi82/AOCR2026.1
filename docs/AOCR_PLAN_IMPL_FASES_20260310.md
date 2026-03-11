# AOCR - Plan de Implementacion por Fases (2026-03-10)

## 1. Resumen funcional (diagramas BPMN)

### Emision o Renovacion AOCR
1. Solicitud inicial del explotador.
2. Carga y verificacion documental.
3. Ciclo de observacion y subsanacion.
4. Aceptacion documental.
5. Fase de inspeccion.
6. Elaboracion AOCR.
7. Revision AOCR.
8. Validacion AOCR.
9. Legalizacion.
10. Emision/recepcion AOCR.

### Inspecciones
1. Creacion solicitud de inspeccion.
2. Asignacion de inspector responsable.
3. Verificacion y aceptacion/observacion.
4. Subsanacion documental cuando aplica.
5. Gestion de viaticos y pago.
6. Ejecucion inspeccion.
7. Informe.
8. Resultado satisfactorio/no satisfactorio.
9. Cierre.

## 2. Mapa de modulos

1. `SolicitudAOCR` (wizard + guardado atomico + estados).
2. `Documento` (carga, validacion, observacion, subsanacion).
3. `AeronaveSolicitud` (persistencia por solicitud).
4. `HistorialEstado` (trazabilidad de transiciones).
5. `Tecnico` (asignacion inspector/tecnico responsable).
6. `Inspeccion` (flujo completo y resultado).
7. `AOCR final` (elaboracion, revision, validacion, legalizacion, emision).
8. `Notificaciones` (correo + in-app).

## 3. Que revisar primero en el proyecto actual

1. `SolicitudAOCRController.FormularioCompleto` y transaccion de guardado completo.
2. `AeronaveSolicitudDAO` y compatibilidad real de columnas (`codigo_solicitud/codigosolicitud`, `created_at`).
3. `DocumentoDAO` y cumplimiento de `chk_estado_documento`.
4. `HistorialEstadoDAO` y coexistencia tabla canonica/legacy.
5. `SolicitudAOCRController.Detalle` y transiciones AOCR visibles por rol.
6. `InspeccionController` y bloqueo AOCR final sin resultado satisfactorio.

## 4. Integracion Tecnico Responsable (OPINSPECTORES / OPIAR2)

### Fuente y reglas
- Tabla AS400: `OPIAR2`.
- Campos: `OPICED`, `OPINO2`, `OPIES1`, `OPITIP`.
- Filtros obligatorios:
  - `OPIES1 = 'AC'`.
  - `OPITIP = 'OPS' | 'AIR' | ambos`.

### Implementacion tecnica
1. DAO reutilizable: `CapaDatos/DAOs/InspectorAS400DAO.cs`.
2. Modelo de salida: `CapaDatos/Models/InspectorAs400Record.cs`.
3. Endpoint reusable: `Tecnico/ListarInspectoresActivos`.
4. UIs integradas:
   - `Views/Tecnico/AsignarInspector.cshtml`
   - `Views/Inspeccion/Crear.cshtml`
   - `Views/Inspeccion/Editar.cshtml`
5. Persistencia:
   - Solicitud: `tecnico_responsable_*`, `inspector_apoyo_*`.
   - Inspeccion: `inspector_principal_*`, `inspector_apoyo_*`.

## 5. Implementacion por fases (estado actual)

### Fase 1 - Mapa funcional
- Completada en este documento.

### Fase 2 - GAP vs implementacion
- Completada parcialmente:
  - Identificado y corregido uso de acciones legacy en detalle AOCR.
  - Identificada compatibilidad mixta BPMN/legacy en estados.

### Fase 3 - Arquitectura funcional/tecnica final
- Base implementada:
  - Estados normalizados.
  - Historial de estados tolerante a esquemas legacy/canonicos.
  - Persistencia dinamica por metadata de columnas.

### Fase 4 - Implementacion por modulos
- Documental: endurecido (`DocumentoDAO`) con resolucion dinamica de estado.
- Aeronaves: endurecido (`AeronaveSolicitudDAO`) sin depender de `created_at`.
- AOCR workflow:
  - `SolicitudAOCRController` con reglas de transicion por rol.
  - `Detalle.cshtml` actualizado a acciones reales BPMN.
  - Notificaciones in-app en cambios de estado.
- Inspecciones:
  - Selector de inspectores activo por tipo.
  - Validacion en backend contra AS400.
  - Flujo de estados y resultado funcional.

### Fase 5 - QA y checklist preliminar
- Ver seccion 6.

## 6. Checklist de pruebas minimas

1. Guardar solicitud AOCR con 0 aeronaves.
2. Guardar solicitud AOCR con 2 aeronaves.
3. Adjuntar documentos validos y confirmar que no viola `chk_estado_documento`.
4. Reenviar solicitud observada y validar cambio a `Subsanada`.
5. Asignar inspector principal desde OPINSPECTORES filtrando `OPS`.
6. Asignar inspector principal desde OPINSPECTORES filtrando `AIR`.
7. Confirmar que no aparecen inspectores con `OPIES1 <> 'AC'`.
8. Crear inspeccion y registrar resultado satisfactorio.
9. Intentar emitir AOCR sin inspeccion satisfactoria (debe bloquear).
10. Emitir AOCR con inspeccion satisfactoria (debe permitir).

## 7. Validacion tecnica de esta iteracion

- Build validado:
  - `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe CapaPresentacion\CapaPresentacion.csproj /t:Build /p:Configuration=Debug /nologo /v:minimal`
- Resultado:
  - Compila correctamente.
  - Persisten warnings preexistentes de referencias/obsoletos.

## 8. Scripts SQL relevantes

1. `scripts/sql/20260310_aocr_formulario_completo_hardening.sql`
2. `scripts/sql/20260310_aocr_flujo_bpmn_base.sql`
3. `scripts/sql/20260310_aocr_inspectores_opiar2.sql`
4. `scripts/sql/20260310_aocr_diagnostico_esquema.sql`
