# AOCR - Diagnostico End-to-End 2026-05-28

## Alcance revisado

- Solicitud AOCR
- Flujo financiero base asociado a orden, pago, factura y auditoria
- Asignacion de inspector
- Inspeccion, LV/EAE, informe tecnico y resultado final
- AOCR, condiciones y limitaciones, coordinacion y firma institucional
- Dashboards, bandejas y seguridad por roles

## Mapa actual del flujo real

1. Orden de recaudacion y pago: `OrdenRecaudacionController`, `FinancieroController`, `AuditTrailService`.
2. Solicitud AOCR y documentos: `SolicitudAOCRController`, `DocumentoController`, `RevisionDocumentalService`.
3. Asignacion de inspector e inspeccion: `CoordinacionJefaturaController`, `InspeccionController`, `SolicitudAOCRDAO`, `InspeccionDAO`.
4. LV/EAE e informe tecnico: `InspeccionController`, `InspeccionInformeDAO`, `HallazgoDAO`, `ListaVerificacionOperacionalEaeDAO`.
5. Habilitacion AOCR: `CapaNegocio/Services/GeneracionAOCRService.cs`.
6. Revision AOCR en coordinacion/jefatura: `CoordinacionJefaturaController`, `Views/CoordinacionJefatura/ValidarAocr.cshtml`.
7. Certificado/firma institucional: `6_CertificadoController.cs`, `FirmaDigitalService`, `AocrFirmaDocumentoDAO`, `AocrFirmaPosicionDocumentoDAO`.

## Matriz resumida de estados reales

### EstadoSolicitud canonico actual

Archivo fuente: `CapaDatos/Constants/EstadoConstants.cs`

- `Solicitud Creada`
- `Documentacion Pendiente`
- `Observada`
- `Subsanada`
- `Aceptacion Documental`
- `Requiere Inspeccion`
- `Pendiente Asignacion RT`
- `En Inspeccion`
- `Generado Condiciones y Limitaciones`
- `En Revision Coordinador Final`
- `Enviado DCAV`
- `Firmado DCAV`
- `AOCR En Elaboracion`
- `AOCR En Revision`
- `AOCR Validado`
- `AOCR Legalizado`
- `AOCR Emitido/Recibido`
- `Finalizado`

### Hallazgo relevante

- Existe una segunda clase `CapaDatos/Constants/EstadosSolicitudAOCR.cs` con estados alternos (`RECEPCIONADO`, `ANALISIS_REQUISITOS`, etc.) que no coincide con la maquina canonica actual y genera riesgo de deriva conceptual.

## Matriz resumida de roles y bandejas activas

- RT: `SolicitudAOCR/Index`, `SolicitudAOCR/Detalle`, carga documental y descarga final.
- Financiero: `FinancieroController`, validacion de pagos, factura/FR3 y trazabilidad financiera.
- Coordinador/CoordinadorInspecciones: `CoordinacionJefatura/DashboardInspeccion`, asignacion de inspector, revision AOCR previa a DIRDAC.
- Inspector: `Inspeccion/Index`, `Inspeccion/Detalle`, revision documental, LV/EAE, informe tecnico, AOCR en elaboracion.
- DIRDAC/Direccion/Jefatura: decision institucional final de informe tecnico, validacion/firma institucional final.
- Administrador: acceso transversal.

## Inconsistencias verificadas

1. Auditoria AOCR incompleta: `aocr_audit_trail` se creaba con `accion VARCHAR(20)` y `modulo VARCHAR(50)`, insuficiente para eventos descriptivos del flujo AOCR actual.
2. Estados duplicados: `EstadoSolicitud` y `EstadosSolicitudAOCR` no representan la misma maquina de estados.
3. PDFs AOCR fragmentados: `Views/Certificado/CertificadoAOCR.cshtml` concentra una version institucional del reconocimiento, mientras coordinacion usa `AocrReconocimientoPdf.cshtml` y `AocrCondicionesLimitacionesPdf.cshtml` con estructura simplificada.
4. Los PDFs de coordinacion permitian generacion con campos obligatorios faltantes y mostraban `PENDIENTE DE COMPLETAR` en vez de bloquear con detalle exacto.
5. La trazabilidad de coordinacion dependia de `Debug.WriteLine` y no de auditoria persistente enriquecida.

## Botones o acciones bloqueados indebidamente detectados en esta iteracion

1. Coordinacion podia ver tarjetas `Listos firma` sin ruta operativa real hasta el ajuste previo del flujo `AOCR_EnElaboracion -> AOCR_EnRevision`.
2. La generacion/preview/descarga de documentos AOCR de coordinacion no verificaba campos institucionales obligatorios antes de renderizar el PDF.

## PDFs afectados

1. `CapaPresentacion/Views/Certificado/CertificadoAOCR.cshtml`
2. `CapaPresentacion/Views/CoordinacionJefatura/AocrReconocimientoPdf.cshtml`
3. `CapaPresentacion/Views/CoordinacionJefatura/AocrCondicionesLimitacionesPdf.cshtml`
4. Vista legacy adicional: `CapaPresentacion/Views/SolicitudAOCR/CertificadoAOCR.cshtml`

## Controladores, servicios, DAOs y vistas afectados o directamente relacionados

- `CapaPresentacion/Controllers/SolicitudAOCRController.cs`
- `CapaPresentacion/Controllers/CoordinacionJefaturaController.cs`
- `CapaPresentacion/Controllers/6_CertificadoController.cs`
- `CapaNegocio/SolicitudEstadoTransitionBL.cs`
- `CapaNegocio/Services/GeneracionAOCRService.cs`
- `CapaDatos/Services/AuditTrailService.cs`
- `CapaPresentacion/Views/CoordinacionJefatura/ValidarAocr.cshtml`
- `CapaPresentacion/Views/CoordinacionJefatura/AocrReconocimientoPdf.cshtml`
- `CapaPresentacion/Views/CoordinacionJefatura/AocrCondicionesLimitacionesPdf.cshtml`
- `CapaPresentacion/Views/Certificado/CertificadoAOCR.cshtml`

## Scripts SQL necesarios

1. `scripts/20260528_expand_aocr_audit_trail.sql`
2. `scripts/20260601_sync_audit_idempotency.sql` alineado para longitudes y estados de auditoria.

## Diagnostico puntual contra AOCR1.pdf

- El sistema no esta unificado aun sobre una sola plantilla de dos paginas para reconocimiento + condiciones y limitaciones.
- La vista institucional `Views/Certificado/CertificadoAOCR.cshtml` contiene mayor fidelidad de encabezado, texto legal y ancla de firma, pero no sustituye por si sola la segunda pagina de condiciones.
- Las vistas de coordinacion contienen campos editables utiles para el workflow, pero su presentacion seguia siendo de trabajo interno y no una salida oficial cerrada.

## Correcciones aplicadas en esta iteracion

1. Se habilito y alineo la revision AOCR de coordinacion para `Listos firma` con envio real a DIRDAC y devolucion con observacion obligatoria.
2. Se amplio la auditoria `aocr_audit_trail` a `accion/modulo VARCHAR(100)` y se agregaron `estado_anterior` y `estado_nuevo` tanto en runtime como en SQL idempotente.
3. `AuditTrailService.RegistrarCambioEstado` ahora persiste explicitamente `estado_anterior` y `estado_nuevo`.
4. `CoordinacionJefaturaController` ahora bloquea preview, visualizacion y generacion AOCR cuando faltan campos obligatorios y devuelve el detalle exacto.
5. Las plantillas PDF de coordinacion dejaron de mostrar `PENDIENTE DE COMPLETAR` para campos opcionales y usan `No aplica`.
6. La salida oficial AOCR1 ahora se consolida en `Views/Certificado/CertificadoAOCR.cshtml` como PDF institucional de dos paginas; el flujo normal de Coordinacion reutiliza esa misma plantilla para preview, PDF para firma y PDF final, y al firmarse sincroniza la ruta final con `aocr_tbcertificado`.

## Pendientes criticos para cerrar el flujo 100%

1. Consolidar una plantilla institucional unica de AOCR1 de dos paginas para reconocimiento y condiciones/limitaciones sin duplicidad entre `Certificado` y `CoordinacionJefatura`.
2. Llevar trazabilidad AOCR rica a auditoria persistente, no solo a logs de debug.
3. Completar la matriz formal separando `EstadoSolicitud`, `EstadoInspeccion`, `EstadoInformeTecnico`, `ResultadoTecnicoFinal`, `EstadoAOCR`, `EstadoCondicionesLimitaciones` y `EstadoNC` en un artefacto operativo verificable.
4. Ejecutar QA por rol con evidencia de flujo satisfactorio y no satisfactorio contra la base actual.