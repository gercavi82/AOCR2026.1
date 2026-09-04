# AC-11 — Informe técnico de implementación

## Identificación y línea base

- Repositorio: `C:\proyectos\AOCR`
- Rama: `feat/flujo-institucional_v2`
- Commit base: `a161745a8c7d1052a9e045a773a57470ed5e94a8`
- Fecha de ejecución: 2026-09-04, zona `America/Guayaquil`
- Worktree inicial: contenía cambios de AC-10 y otros cambios locales sin confirmar. Se preservaron y no se revirtieron.
- Compilación inicial: correcta, con la advertencia heredada de compatibilidad de `itext.commons 9.5.0.0`.
- Regresión inicial AC-10: 18/18 pruebas correctas.

## Diagnóstico anterior

El flujo heredado permitía que la finalización documental enviara ramas directamente a las direcciones y que el registro de las dos firmas llevara el expediente a `FINALIZADO`, incluyendo el correo al RT. Esto mezclaba AC-11 con la entrega de AC-12. `DirdacController` también contenía decisiones de estado, usaba datos dinámicos y admitía `ADMINISTRADOR` en operaciones funcionales. Había estados históricos ambiguos y varias reglas duplicadas entre controller, DAO y el flujo de firma.

La consulta de solo lectura al esquema real confirmó las estructuras existentes `aocr_proceso_estado`, `aocr_tbdocumento_generado`, `aocr_evento_workflow`, `aocr_tbhistorial_estado`, `aocr_tbnotificacion`, `email_queue` y `aocr_tbfirma_documento`. Se inspeccionaron expedientes controlados, incluido el expediente 7 con proceso `DOCUMENTOS_FINALES_POR_GENERAR` y documentos versionados, sin modificar datos productivos.

## Diseño implementado

`IAocrFinalWorkflowService` es la autoridad central de estas operaciones:

- `RemitirAocrDirdac`
- `DevolverAocrDircav`
- `FirmarLegalizarAocr`
- `EvaluarFirmasCompletas`
- consulta tipada de bandeja y detalle DIRDAC

Los controllers obtienen la identidad del servidor y delegan al servicio. El servicio valida request, rol canónico exacto, permiso granular y usuario distinto de cero. El repositorio ejecuta cada transición en transacción PostgreSQL con bloqueo, control de versión, estado de origen, documentos vigentes, idempotencia, historial, auditoría y notificación/outbox.

La firma solo acepta evidencia almacenada bajo el directorio controlado de firmados AOCR, vuelve a calcular SHA-256 antes de persistir y actualiza únicamente una versión aún no firmada. La evidencia anterior se conserva; cualquier corrección exige una versión nueva.

## Máquina de estados cubierta

```text
INSPECTOR
  PAQUETE_BORRADOR_INSPECTOR
    -> PENDIENTE_REVISION_FINAL_COORDINADOR

COORDINADOR
  PENDIENTE_REVISION_FINAL_COORDINADOR
    -> DEVUELTO_INSPECTOR_FINAL
    -> PENDIENTE_REVISION_FINAL_DIRCAV

DIRCAV
  PENDIENTE_REVISION_FINAL_DIRCAV
    -> DEVUELTO_COORDINADOR_FINAL
    -> CL_PENDIENTE_FIRMA_DIRCAV
    -> CL_FIRMADA_DIRCAV
  CL_FIRMADA_DIRCAV + AOCR vigente
    -> AOCR_PENDIENTE_DIRDAC
  DEVUELTO_DIRCAV + corrección/versionado
    -> AOCR_PENDIENTE_DIRDAC

DIRDAC
  AOCR_PENDIENTE_DIRDAC
    -> DEVUELTO_DIRCAV
    -> AOCR_FIRMADA_DIRDAC

SISTEMA
  CL_FIRMADA_DIRCAV + AOCR_FIRMADA_DIRDAC
  + mismo expediente + versiones vigentes compatibles
    -> FIRMAS_COMPLETAS
```

AC-11 no produce `LISTO_PARA_ENTREGA`, `ENTREGADO`, `FINALIZADO` ni `CERRADO`, y no encola el correo final al RT.

## Matriz de autorización AC-11

| Rol | Acción | Estado/precondición | Permiso |
|---|---|---|---|
| DIRCAV | Remitir AOCR a DIRDAC | CL firmada, AOCR vigente; revisión final o devolución DIRDAC | `DIRCAV_REMITIR_DIRDAC` |
| DIRDAC | Ver bandeja/detalle | AOCR pendiente DIRDAC visible | `DIRDAC_VER_BANDEJA` |
| DIRDAC | Devolver a DIRCAV | `AOCR_PENDIENTE_DIRDAC`, versión esperada y observación válida | `DIRDAC_DEVOLVER_DIRCAV` |
| DIRDAC | Firmar/legalizar AOCR | `AOCR_PENDIENTE_DIRDAC`, evidencia íntegra y versión esperada | `DIRDAC_FIRMAR_AOCR` |
| ADMINISTRADOR | Operaciones anteriores | No aplica | Denegado expresamente |
| COORDINADOR/INSPECTOR/RT/FINANCIERO | Operaciones DIRDAC/DIRCAV anteriores | No aplica | Denegado |

## Endpoints y UI

- `POST Dircav/RemitirAocrDirdac`
- `GET Dirdac/BandejaAocr`
- `GET Dirdac/Detalle`
- `POST Dirdac/DevolverAocrDircav`
- `POST Dirdac/FirmarLegalizarAocr`

Los POST tienen antiforgery y permiso granular. La bandeja y su contador consumen la misma consulta. Las vistas son tipadas, usan `Url.Action`, tabla responsive, foco visible, controles táctiles y prevención de doble envío. No se añadieron rutas absolutas dependientes de la raíz del sitio.

## Persistencia, concurrencia e idempotencia

- Bloqueo transaccional del proceso y documentos (`FOR UPDATE` y advisory lock por solicitud).
- Versión esperada verificada antes de cada escritura; incompatibilidad retorna 409.
- Clave de evento basada en solicitud, operación, versión y actor.
- Índices únicos para eventos, notificaciones, correo y firma por versión.
- Historial, auditoría, firma y outbox se confirman en la misma transacción.
- No se invoca SMTP dentro de la transición.
- La escritura de firma exige que el hash firmado todavía sea nulo.
- Los errores provocan rollback y se convierten en respuestas controladas 400/401/403/404/409/500.

## Migración

- UP: `scripts/sql/20260904_ac11_flujo_legalizacion.sql`
- Validación: `scripts/sql/20260904_ac11_flujo_legalizacion_validate.sql`
- Rollback no destructivo: `scripts/sql/20260904_ac11_flujo_legalizacion_rollback.sql`

La migración amplía restricciones para los estados exactos AC-11, agrega índices necesarios, activa permisos granulares y retira permisos operativos AC-11 de ADMINISTRADOR sin reasignar usuarios históricos. Se ejecutó contra el PostgreSQL real dentro de una transacción y se hizo rollback intencional: `VALIDACION_DDL_OK_ROLLBACK`.

## Archivos AC-11 creados

- `AOCR.Tests/Unit/Ac11LegalizacionWorkflowTests.cs`
- `CapaDatos/DAOs/AocrFinalWorkflowDAO.cs`
- `CapaDatos/Interfaces/IAocrFinalWorkflowRepository.cs`
- `CapaModelo/AocrFinalWorkflowModels.cs`
- `CapaNegocio/Interfaces/IAocrFinalWorkflowService.cs`
- `scripts/sql/20260904_ac11_flujo_legalizacion.sql`
- `scripts/sql/20260904_ac11_flujo_legalizacion_validate.sql`
- `scripts/sql/20260904_ac11_flujo_legalizacion_rollback.sql`
- `docs/AC11_IMPLEMENTACION_20260904.md`

## Archivos existentes modificados por AC-11

- `AOCR.Tests/AOCR.Tests.csproj`
- `CapaDatos/CapaDatos.csproj`
- `CapaDatos/Constants/AocrEstadosProceso.cs`
- `CapaDatos/DAOs/DocumentosFinalesWorkflowDAO.cs`
- `CapaModelo/CapaModelo.csproj`
- `CapaNegocio/CapaNegocio.csproj`
- `CapaNegocio/SeguridadBL.cs`
- `CapaNegocio/Services/AocrFinalWorkflowService.cs`
- `CapaPresentacion/App_Start/UnityConfig.cs`
- `CapaPresentacion/CapaPresentacion.csproj`
- `CapaPresentacion/Controllers/DircavController.cs`
- `CapaPresentacion/Controllers/DirdacController.cs`
- `CapaPresentacion/Controllers/FirmaAocrController.cs`
- `CapaPresentacion/Helpers/SidebarMenuBuilder.cs`
- `CapaPresentacion/Views/Dircav/Detalle.cshtml`
- `CapaPresentacion/Views/Dirdac/Bandeja.cshtml`
- `CapaPresentacion/Views/Dirdac/Detalle.cshtml`
- `scripts/dev/SchemaProbeNet/Program.cs`

Los demás archivos que aparecen modificados o sin seguimiento pertenecían al trabajo local previo, principalmente AC-10, y se preservaron.

## Evidencia automatizada final

- Solución completa: compila correctamente.
- Regresión AC-10: 18/18 correcta.
- Suite no integrada: 641/641 correcta.
- Casos focales AC-11 no integrados: 25/25 correctos.
- Prueba AC-11 de integración de bandeja contra el esquema real: 1/1 correcta y de solo lectura.
- Validación DDL real: correcta con rollback deliberado.
- La suite integrada global del repositorio conserva 8 fallos previos ajenos a AC-11 por diferencias de esquemas/fixtures en Gate2–Gate5 y subsanación documental; 2 pruebas están omitidas. Estos fallos no se ocultaron ni modificaron.

Las pruebas AC-11 cubren roles, permiso, usuario cero, 400/403/404/409/500, remisión, devolución, observación, hash, firma, idempotencia, consulta compartida bandeja/contador, antiforgery, transacción, locks, auditoría, historial, outbox, migraciones, rutas MVC, responsive estático y prohibición de entregar/cerrar en AC-11.

## Limitaciones de verificación

- La migración fue validada con rollback, pero no quedó aplicada al entorno real; aplicarla requiere una ventana autorizada de despliegue.
- No se ejecutó un happy path mutante sobre datos productivos para evitar alterar expedientes reales.
- IIS Express inició por HTTPS, pero el navegador de automatización disponible no tenía una instancia conectada. Por eso no existe evidencia visual interactiva de las siete resoluciones ni navegación autenticada bajo `/aocr`; sí existe verificación estática automatizada de responsive y URLs generadas por MVC.
- La firma criptográfica completa requiere certificado/identidad DIRDAC y sesión autenticada de prueba. La integración desde el resultado criptográfico al servicio central está implementada, pero esa ceremonia no se ejecutó sobre producción.

En consecuencia, la implementación de código está cerrada y validada automatizadamente, pero la aceptación operativa total debe esperar: aplicación de migración en un ambiente de prueba, ejecución E2E autenticada con identidades DIRCAV/DIRDAC y evidencia visual en las siete resoluciones.

## Recomendación para AC-12

Iniciar AC-12 únicamente desde `FIRMAS_COMPLETAS`. AC-12 debe verificar nuevamente vigencia, compatibilidad e integridad de ambos PDF; transicionar explícitamente a `LISTO_PARA_ENTREGA`, luego `ENTREGADO` y finalmente `CERRADO`; y recién entonces crear la entrega y el correo idempotente al RT/Inspector. No debe reutilizar una firma como evidencia de entrega.
