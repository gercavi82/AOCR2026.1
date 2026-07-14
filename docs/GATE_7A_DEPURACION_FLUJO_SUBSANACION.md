# GATE 7A — Depuración del flujo de subsanación

## Línea base

- Rama: `firma-dirdac-tec`.
- Commit de referencia y `HEAD` verificado: `da964be92ca99370c92e4aa3e0c29f283fcd2cb0`.
- Línea base recibida: 281 totales, 261 aprobadas, 19 fallidas y 1 omitida.

## Matriz de endpoints

| Endpoint | Controlador / vista | Rol | Estado admitido | Persistencia | Servicio/DAO | Destino | Clasificación |
|---|---|---|---|---|---|---|---|
| `GET SolicitudAOCR/Subsanar` | `SolicitudAOCRController` / `SolicitudAOCR/Subsanar.cshtml` | RT, propietario o administrador | Solicitud `OBSERVADA`, o NC `FIRMADA_COORDINADOR`, `EN_SUBSANACION`, `SUBSANACION_DEVUELTA` | Solo lectura | `DocumentoSubsanacionService`, `DocumentoDAO`, `NoConformidadDAO` | Formulario individual | Canónico |
| `POST SolicitudAOCR/EnviarSubsanacionAlInspector` | `SolicitudAOCRController` / formulario anterior | RT, propietario o administrador | Los mismos estados de carga | `aocr_tbdocumento`, `aocr_tbdocumento_subsanacion`, revisión e historial | `CrearVersionSubsanadaNc`, `RevisionDocumentalService` | `SolicitudAOCR/Detalle` | Canónico |
| `POST Inspeccion/RevisarDocumentoSubsanado` | `InspeccionController` / `Inspeccion/Detalle.cshtml` | Inspector asignado o administrador | `SUBSANADA_RT`, `EN_REVISION_INSPECTOR`, `SUBSANACION_DEVUELTA` | Decisión individual, auditoría y estado documental | `RegistrarDecisionDocumentoSubsanado` | `Inspeccion/Detalle` | Canónico |
| `POST Inspeccion/AceptarSubsanacionNc` | `InspeccionController` / `Inspeccion/Detalle.cshtml` | Inspector asignado o administrador | Solo cuando todas las versiones están aceptadas | NC → `SUBSANACION_ACEPTADA`; inspección → `EN_INSPECCION`; historial | `AceptarSubsanacionDocumentalCompleta`, `ReevaluacionInspeccionService` | Reevaluación técnica | Canónico |
| `GET RT/SubsanarNc` | `RTController`; la vista legacy solo muestra aviso | RT, propietario o administrador | No habilita carga | Ninguna | Ninguno | Redirección controlada a `SolicitudAOCR/Subsanar` | Legacy obsoleto |
| `POST RT/SubsanarNcPost` | `RTController` | RT, propietario o administrador | Ninguno | Ninguna; archivo ignorado | DAO legacy también bloqueado | Redirección controlada a `SolicitudAOCR/Subsanar` | Legacy bloqueado |
| `GET RT/DescargarSubsanacionNc` | `RTController` | RT propietario o administrador | Expediente histórico con ruta | Solo lectura | `NoConformidadDAO` | PDF histórico | Compatibilidad histórica |
| `GET Inspeccion/DescargarSubsanacionNc` | `InspeccionController` | Inspector asignado o administrador | Expediente histórico con ruta | Solo lectura | `NoConformidadDAO` | PDF histórico | Compatibilidad histórica |

## Flujo oficial

`SolicitudAOCR/Subsanar` → carga de todos los documentos observados → versión N+1 por documento → `aocr_tbdocumento_subsanacion` → envío al Inspector → decisión individual → `SUBSANACION_ACEPTADA` → reevaluación.

Los documentos aceptados (`ACEPTADO`, `APROBADO`, `ACEPTADO_SUBSANACION`) no pueden sustituirse. Los observados, rechazados y `RECHAZADO_SUBSANACION` sí pueden producir la siguiente versión.

## Depuración legacy

- El botón vigente apunta únicamente a `SolicitudAOCR/Subsanar`.
- La vista `RT/SubsanarNc.cshtml` ya no contiene formulario ni campo de archivo.
- Los endpoints legacy están marcados `Obsolete`, registran `LEGACY_REDIRECT`/`LEGACY_WRITE_BLOCKED` y redirigen.
- `RegistrarSubsanacionRt`, `ReabrirSubsanacionRt` y `CerrarSubsanacion` lanzan `NotSupportedException`; no existe una escritura operativa de PDF general.
- Se conserva el código histórico inalcanzable dentro del controlador temporalmente para facilitar comparación/migración, pero está detrás de una redirección incondicional y de la defensa adicional del DAO.
- Las descargas históricas exigen autorización, archivo `.pdf` y ruta confinada bajo `App_Data/SubsanacionesNC`.

## Máquina de estados

- Carga RT: `FIRMADA_COORDINADOR`, `EN_SUBSANACION`, `SUBSANACION_DEVUELTA`.
- Revisión Inspector: `SUBSANADA_RT`, `EN_REVISION_INSPECTOR`.
- Cierre documental: `SUBSANACION_ACEPTADA`.
- Aceptar documentos no modifica `resultado` ni `resultado_evaluacion`; crea el contexto para un Informe Técnico nuevo.

## Archivos modificados

- `CapaPresentacion/Controllers/RTController.cs`
- `CapaPresentacion/Controllers/SolicitudAOCRController.cs`
- `CapaPresentacion/Controllers/InspeccionController.cs`
- `CapaPresentacion/Views/RT/SubsanarNc.cshtml`
- `CapaPresentacion/Views/SolicitudAOCR/Detalle.cshtml`
- `CapaDatos/DAOs/NoConformidadDAO.cs`
- `CapaDatos/DAOs/DocumentoDAO.cs`
- `AOCR.Tests/Unit/Gate7ADepuracionSubsanacionTests.cs`
- `AOCR.Tests/AOCR.Tests.csproj`

## Pruebas y validación

- Build Debug: aprobado.
- Build Release y precompilación Razor: aprobados; permanece la advertencia conocida de `itext.commons`.
- Focales: 20/20 aprobadas (`TestResults/gate7a-focal-final.trx`).
- Suite global: 291 ejecutadas, 271 aprobadas, 19 fallidas preexistentes y 1 omitida (`TestResults/gate7a-global-final.trx`).
- No aparecieron fallos nuevos.

## Pendientes reales

- Retirar físicamente, en una limpieza posterior, los bloques inalcanzables de implementación legacy y los tres métodos DAO obsoletos cuando haya concluido la ventana de compatibilidad. Actualmente no son invocables para escribir.
- Resolver por infraestructura los 17 fallos dependientes de AS400 y el dato financiero faltante; los dos contratos históricos restantes se mantienen como línea base. No se modificaron para reducir el conteo.
