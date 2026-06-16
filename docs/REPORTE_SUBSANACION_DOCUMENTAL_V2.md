# Reporte subsanación documental AOCR v2

Fecha: 2026-06-11

## Diagnóstico de causa raíz

1. **Correo al RT fragmentado**: cada devolución individual disparaba `EmailHelper.EnviarEmail` sin `event_key`, generando correos duplicados y asunto distinto al institucional.
2. **Bloqueo RT incompleto**: `Documento/Subir` permitía cargas en solicitud `Observada`; la vista `Subsanar` no mostraba documentos aceptados/bloqueados.
3. **Revisión documental poco ergonómica**: `Documento/Lista` usaba botones sueltos Aceptar/Devolver sin guardado batch ni combos por fila.
4. **Versionamiento parcial**: la subsanación creaba nueva versión pero no marcaba la anterior como `VERSION_ANTERIOR`.
5. **Lógica dispersa**: criterio `PuedeSubsanar` duplicado entre controlador, vista y modelo.

## Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `CapaDatos/Constants/EstadoDocumentoInstitucional.cs` | Catálogo y normalización de estados documentales |
| `CapaNegocio/Services/DocumentoSubsanacionService.cs` | Servicio central: bloqueo RT, clasificación, correo con `event_key` |
| `CapaPresentacion/Controllers/SolicitudAOCRController.cs` | `GuardarRevisionDocumental`, correo consolidado, subsanación versionada |
| `CapaPresentacion/Controllers/DocumentoController.cs` | Bloqueo `Subir` en `Observada`; sin correo por documento individual |
| `CapaPresentacion/Models/SolicitudAOCRViewModel.cs` | `DocumentosBloqueados`, flags de subsanación |
| `CapaPresentacion/Views/SolicitudAOCR/Subsanar.cshtml` | Dos secciones + mensaje institucional + validación JS |
| `CapaPresentacion/Views/Documento/Lista.cshtml` | Combos por documento + Guardar revisión documental + responsive |
| `AOCR.Tests/Unit/DocumentoSubsanacionTests.cs` | Casos 1, 2, 3 y event_key |

## Correo RT

- Cola: `EmailQueueService`
- Tipo: `DOCUMENTOS_DEVUELTOS_INSPECTOR`
- `event_key`: `DOCUMENTOS_DEVUELTOS_INSPECTOR_{SolicitudId}_{DocId1}_{DocId2...}`
- Asunto: `Sistema AOCR - Documentos devueltos para subsanación`
