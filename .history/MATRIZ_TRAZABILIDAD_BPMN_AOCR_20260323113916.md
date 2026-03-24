# Matriz de Trazabilidad BPMN AOCR

Fecha: 2026-03-23

## Estado global

- Implementado: flujo principal de solicitud, revision documental, inspeccion, elaboracion, revision, validacion, legalizacion y emision.
- Parcial: existen hitos BPMN que no estan modelados con artefacto/regla explicita en codigo (detalle abajo).

## Matriz BPMN vs Codigo

| Actividad BPMN | Estado | Evidencia en codigo |
|---|---|---|
| Revisar documento por item | Implementado | `RevisarDocumentoItem` en `CapaPresentacion/Controllers/SolicitudAOCRController.cs` |
| Cerrar revision documental con reglas | Implementado | `FinalizarRevisionDocumental` en `CapaPresentacion/Controllers/SolicitudAOCRController.cs` |
| Paso a Observada/Aceptacion Documental | Implementado | `FinalizarRevisionDocumental` + `CambiarEstadoConReglasAocr` en `CapaPresentacion/Controllers/SolicitudAOCRController.cs` |
| Subsanar y volver al flujo | Implementado | `SubsanarPost` en `CapaPresentacion/Controllers/SolicitudAOCRController.cs` |
| Solicitar inspeccion | Implementado | `SolicitarInspeccion` en `CapaPresentacion/Controllers/SolicitudAOCRController.cs` |
| Registrar hallazgo/NC | Implementado | `RegistrarHallazgo` en `CapaPresentacion/Controllers/InspeccionController.cs` |
| Cerrar inspeccion con control de hallazgos | Implementado | `CerrarInspeccion` en `CapaNegocio/InspeccionBL.cs` |
| Elaboracion AOCR (solo con inspeccion satisfactoria) | Implementado | `MarcarAocrEnElaboracion` + `SolicitudTieneInspeccionSatisfactoria` en `CapaPresentacion/Controllers/SolicitudAOCRController.cs` |
| Revision/Validacion AOCR | Implementado | `MarcarAocrEnRevision`, `AprobarPorJefatura` en `CapaPresentacion/Controllers/SolicitudAOCRController.cs` |
| Legalizacion AOCR | Implementado | `Legalizar` en `CapaPresentacion/Controllers/SolicitudAOCRController.cs` |
| Emision AOCR | Implementado | `EmitirAocr` en `CapaPresentacion/Controllers/SolicitudAOCRController.cs` |
| Matriz canonica de transiciones | Implementado | `CambiarEstadoConReglasAocr` y `EsTransicionAocrPermitida` en `CapaNegocio/SolicitudEstadoTransitionBL.cs` |
| Artefacto explicito "Acta de inspeccion" | Parcial | No se encontro referencia explicita a `acta_inspeccion` en controladores/negocio/datos |
| Tipos documentales BPMN (DOCP/DICP/DIGAC) | Parcial | No se encontro validacion/regla explicita con esos codigos en controladores/negocio/datos |

## Correccion aplicada en esta iteracion

- Se corrigio el cierre de revision documental para usar la fachada de infraestructura y mantener consistencia del flujo:
  - `CapaPresentacion/Controllers/SolicitudAOCRController.cs`
  - Cambio: uso de `_solicitudAocrInfraBL.RegistrarEventoHistorialRevision(...)`.

## Para llegar al 100%

1. Definir si el BPMN exige `Acta de inspeccion` como entidad/archivo obligatorio.
2. Si es obligatorio, implementar:
   - tipo documental en registro/versionado,
   - validacion previa a cierre/avance,
   - visualizacion en detalle de solicitud/inspeccion.
3. Definir si DOCP/DICP/DIGAC son codigos obligatorios de documento.
4. Si son obligatorios, implementar reglas de completitud por tipo antes de `FinalizarRevisionDocumental`.
