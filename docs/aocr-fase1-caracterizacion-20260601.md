# AOCR - Fase 1 Caracterizacion Operativa 2026-06-01

## Objetivo

Congelar el comportamiento operativo actual antes de refactorizar. Esta fase no cambia reglas de negocio; solo deja evidencia verificable de estados, roles, endpoints y efectos laterales criticos.

## Fuente de verdad operativa actual

- Estados canonicos de solicitud: `CapaDatos/Constants/EstadoConstants.cs`
- Reglas de transicion de solicitud: `CapaNegocio/SolicitudEstadoTransitionBL.cs`
- Seguridad contextual real: `CapaPresentacion/Filters/SecurityFilters.cs`
- Flujo documental y cierre AOCR: `CapaPresentacion/Controllers/SolicitudAOCRController.cs`
- Flujo de inspeccion y decision institucional final: `CapaPresentacion/Controllers/InspeccionController.cs`
- Validacion y documentos AOCR institucionales: `CapaPresentacion/Controllers/CoordinacionJefaturaController.cs`
- Habilitacion de generacion AOCR: `CapaNegocio/Services/GeneracionAOCRService.cs`

## Conductas congeladas en Fase 1

### Revision documental

- `AccionMasivaRevisionDocumental` mantiene el routing actual:
  - todos aceptados -> `Aceptacion Documental`
  - con observaciones o devoluciones -> `Observada`
- `FinalizarRevisionDocumental` mantiene el mismo contrato de salida.

### Subsanacion del RT

- `SubsanarPost` crea nuevas versiones documentales con estado `PENDIENTE_REVISION_SUBSANACION`.
- La solicitud vuelve a `Subsanada`.
- El inspector sigue siendo notificado desde `NotificarInspectorDocumentacionSubsanada`.

### Cierre por descarga final

- `DescargarAceptacionDocumental` conserva el acoplamiento actual entre descarga final del RT y transicion a `Finalizado`.
- `DescargarCondicionesLimitacionesModificacion` conserva el mismo patron de cierre por descarga.

### Decision institucional final

- `DireccionAprobar` sigue sincronizando la solicitud AOCR mediante `SincronizarSolicitudAocrTrasFirmaFinal`.
- El mensaje operativo vigente sigue declarando que la aprobacion institucional habilita la generacion AOCR.

## Contratos de autorizacion congelados

- `SolicitudAOCR/FinalizarRevisionDocumental`: `Inspector,Coordinador,CoordinadorInspecciones,Administrador`
- `SolicitudAOCR/FirmarAceptacionDocumental`: `Coordinador,CoordinadorInspecciones,Administrador`
- `SolicitudAOCR/AprobarPorJefatura`: `DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador`
- `SolicitudAOCR/ObservarPorJefatura`: `CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador`
- `SolicitudAOCR/Legalizar`: `CoordinacionLegal,CoordinadorLegal,DirectorGeneral,Administrador`
- `CoordinacionJefatura/ValidarAocr`: contrato institucional actual por roles
- `CoordinacionJefatura/DocumentoValidacionAocr`: contrato institucional actual por roles
- `Inspeccion/DireccionAprobar` y `Inspeccion/DireccionDevolver`: `AocrAuthorize(Roles = ROLES_ACCESO_DECISION_INSTITUCIONAL_FINAL)`

## Cobertura de pruebas de caracterizacion

Base preexistente:

- `AOCR.Tests/Unit/EstadoSolicitudTransitionMatrixTests.cs`
- `AOCR.Tests/Unit/SolicitudEstadoTransitionBLTests.cs`
- `AOCR.Tests/Unit/AocrModificationAuthorizationTests.cs`

Cobertura agregada en esta fase:

- `AOCR.Tests/Unit/OperationalFlowCharacterizationTests.cs`

La nueva suite fija cuatro zonas de riesgo:

1. Routing actual de revision documental.
2. Subsanacion del RT y retorno a `Subsanada`.
3. Cierre por descarga final del RT.
4. Contratos de autorizacion y sincronizacion tras decision institucional final.

## Salida esperada al cerrar Fase 1

- Documento operativo unico para arrancar refactor sin ambiguedad.
- Pruebas de caracterizacion que fallen si cambia sin querer el comportamiento actual.
- Base minima para iniciar Fase 2 sin perder la fotografia real del sistema.