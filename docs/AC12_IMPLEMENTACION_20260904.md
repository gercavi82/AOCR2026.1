# AC-12 — Entrega final al RT y al Inspector

## Línea base

- Repositorio: `C:\proyectos\AOCR`
- Rama: `feat/flujo-institucional_v2`
- HEAD al iniciar AC-12: `a161745a8c7d1052a9e045a773a57470ed5e94a8`
- HEAD actual: `3beecb97d73cbf747b47f1ff1d62bed1ec5f8f8d`. Durante la ejecución, un proceso externo confirmó AC-10/AC-11 en el commit `3beecb97`; los cambios de AC-12 permanecen sin commit encima de ese HEAD.
- Fecha: 2026-09-04, zona `America/Guayaquil`
- Worktree inicial: contenía AC-10 y AC-11 sin confirmar; se preservó íntegramente.
- Compilación inicial: correcta.
- Suite no integrada inicial: 641/641.

## Diagnóstico

El código heredado conservaba un método de compatibilidad que finalizaba el expediente y creaba un único correo para el RT. No modelaba al Inspector como destinatario lógico, mezclaba disponibilidad documental con confirmación SMTP, no persistía `MessageId` y no verificaba SHA-256 de los adjuntos al procesarlos. AC-11 ya había retirado la invocación de ese método y terminaba correctamente en `FIRMAS_COMPLETAS`.

La cola existente sí era reutilizable:

- `PENDIENTE`: mensaje elegible cuando llega `proximo_intento`.
- `ENVIANDO`: fila reclamada mediante `FOR UPDATE SKIP LOCKED`.
- `ENVIADO`: SMTP confirmó éxito.
- `ERROR`: agotó reintentos.
- `ERROR_CONFIG_SMTP` y `ERROR_NO_REINTENTABLE`: fallos terminales.
- Backoff existente: 5, 15 y 60 minutos.
- Un reinicio recupera filas `ENVIANDO` abandonadas.

En el diagnóstico real había 73 mensajes `ENVIADO` y 13 `ERROR`, estos últimos con tres intentos. El esquema no tenía `message_id`, `sent_at`, hash de adjunto ni tablas de entrega por destinatario.

El expediente controlado disponible era la solicitud 7, RT usuario 88 e Inspector usuario 79, con AOCR y CL versión 2 todavía sin firma y estado `DOCUMENTOS_FINALES_POR_GENERAR`. No existía un expediente productivo apto en `FIRMAS_COMPLETAS`; no se alteraron datos para fabricarlo.

## Arquitectura implementada

```text
AC-11 confirma firmas
  -> evento único ENTREGA_FINAL_SOLICITADA
  -> EntregaFinalService
  -> EntregaFinalDAO (transacción PostgreSQL)
       verifica expediente, firmas, versiones, archivos, RT e Inspector
       crea entrega + snapshot documental + 2 destinatarios lógicos
       crea email_queue idempotente + adjuntos con SHA-256
       publica documentos en bandeja
       FIRMAS_COMPLETAS -> LISTO_PARA_ENTREGA
  -> commit
  -> EmailQueueProcessor
       valida ruta/tamaño/MIME/cabecera PDF/SHA-256
       envía o reintenta el mismo mensaje
       registra intento y MessageId
       recalcula parcial/completa/fallida
       ENTREGA_COMPLETA -> ENTREGADO
```

`CERRADO` nunca se aplica automáticamente.

## Matriz de destinatarios y autorización

| Actor | Bandeja | Descarga | Correo operativo |
|---|---|---|---|
| RT asignado | Solo entregas cuyo `usuario_id` coincide y pertenecen al contexto de compañía activo | AOCR y CL del snapshot autorizado | Sí |
| Inspector asignado | Solo entregas cuyo Inspector coincide con la inspección | AOCR y CL del snapshot autorizado | Sí |
| Otro RT | Sin acceso | 404 para evitar IDOR | No |
| Otro Inspector | Sin acceso | 404 para evitar IDOR | No |
| Coordinador | Consulta institucional con permiso | Con permiso `ENTREGA_FINAL_CONSULTAR` | No |
| DIRCAV/DIRDAC | Consulta institucional con permiso | Con permiso `ENTREGA_FINAL_CONSULTAR` | No |
| Financiero | Sin acceso técnico | 403 | No |
| Administrador | Solo vista de soporte/auditoría | No es destinatario operativo | No |

## Modelo de idempotencia

La entrega tiene unicidad por:

```text
SolicitudId + VersionAocr + VersionCl
```

Cada destinatario lógico tiene unicidad por:

```text
EntregaId + TipoDestinatario + UsuarioId
```

Política cuando RT e Inspector comparten la misma dirección: se conservan dos destinatarios lógicos, pero ambos apuntan a una sola fila física de `email_queue`. La clave física incorpora solicitud, versiones y hash del correo normalizado. El worker actualiza esa misma fila al reintentar; no crea eventos ni mensajes nuevos.

## Estados

- `ENTREGA_NO_SOLICITADA`
- `ENTREGA_ENCOLADA`
- `ENTREGA_EN_PROCESO`
- `ENTREGA_PARCIAL`
- `ENTREGA_COMPLETA`
- `ENTREGA_FALLIDA_REINTENTABLE`
- `ENTREGA_FALLIDA_DEFINITIVA`

La disponibilidad en bandeja se registra como `DISPONIBLE` desde el commit de AC-12. La confirmación SMTP se mantiene separada en `estado_correo`.

## Seguridad documental

- La relación RT/solicitud e Inspector/inspección se resuelve desde PostgreSQL.
- No se aceptan IDs de usuario o rol enviados por JavaScript.
- La descarga vuelve a comprobar destinatario, compañía activa, estado de bandeja, ruta controlada, existencia, tamaño, extensión, cabecera `%PDF-` y SHA-256.
- Las rutas físicas nunca se envían al navegador ni se insertan en el cuerpo del correo.
- Los enlaces se generan a partir de la raíz virtual MVC y requieren sesión.
- Cada descarga autorizada, fallida o denegada registra auditoría.
- Los PDF mayores al límite configurado no se adjuntan; el correo conserva solamente el enlace autenticado. Límite por defecto: 20 MiB combinados.

## Endpoints

- `POST Flujo/SolicitarEntregaFinal`
- `GET RT/DocumentosFinales`
- `GET Inspector/DocumentosFinales`
- `GET Documento/DescargarFinal`
- `GET Administrador/EstadoEntrega`

El POST usa antiforgery, rol DIRDAC y permiso `ENTREGA_FINAL_SOLICITAR`. La consulta administrativa exige `ENTREGA_FINAL_AUDITAR`.

## Persistencia y worker

Tablas aditivas:

- `aocr_entrega_final`
- `aocr_entrega_documento`
- `aocr_entrega_destinatario`
- `aocr_entrega_intento`

Extensiones:

- `email_queue.message_id`
- `email_queue.sent_at`
- `email_attachment.sha256`

La transacción web bloquea la solicitud, valida la versión del proceso, persiste entrega, documentos, destinatarios, historial, auditoría y cola, y recién entonces hace commit. SMTP ocurre exclusivamente en el worker. Un fallo SMTP no revierte firmas ni legalización.

## Archivos creados

- `AOCR.Tests/Unit/Ac12EntregaFinalTests.cs`
- `CapaModelo/EntregaFinalModels.cs`
- `CapaDatos/Interfaces/IEntregaFinalRepository.cs`
- `CapaDatos/DAOs/EntregaFinalDAO.cs`
- `CapaNegocio/Interfaces/IEntregaFinalService.cs`
- `CapaNegocio/Services/EntregaFinalService.cs`
- `CapaPresentacion/Controllers/FlujoController.cs`
- `CapaPresentacion/Views/RT/DocumentosFinales.cshtml`
- `CapaPresentacion/Views/Inspeccion/DocumentosFinales.cshtml`
- `CapaPresentacion/Views/Administrador/EstadoEntrega.cshtml`
- `scripts/sql/20260904_ac12_entrega_final.sql`
- `scripts/sql/20260904_ac12_entrega_final_validate.sql`
- `scripts/sql/20260904_ac12_entrega_final_rollback.sql`
- `docs/AC12_IMPLEMENTACION_20260904.md`

## Archivos modificados por AC-12

- `AOCR.Tests/AOCR.Tests.csproj`
- `CapaModelo/CapaModelo.csproj`
- `CapaModelo/AocrFinalWorkflowModels.cs`
- `CapaDatos/CapaDatos.csproj`
- `CapaDatos/DAOs/AocrFinalWorkflowDAO.cs`
- `CapaDatos/Services/EmailQueueService.cs`
- `CapaNegocio/CapaNegocio.csproj`
- `CapaNegocio/SeguridadBL.cs`
- `CapaNegocio/Services/AocrFinalWorkflowService.cs`
- `CapaPresentacion/CapaPresentacion.csproj`
- `CapaPresentacion/App_Start/UnityConfig.cs`
- `CapaPresentacion/Controllers/AdministradorController.cs`
- `CapaPresentacion/Controllers/DocumentoController.cs`
- `CapaPresentacion/Controllers/DirdacController.cs`
- `CapaPresentacion/Controllers/FirmaAocrController.cs`
- `CapaPresentacion/Controllers/InspeccionController.cs`
- `CapaPresentacion/Controllers/RTController.cs`
- `CapaPresentacion/Helpers/SidebarMenuBuilder.cs`

## Evidencia

- Compilación de solución: correcta.
- AC-12: 26/26 pruebas correctas.
- Regresión focal AC-10 + AC-11: 43/43 correcta.
- Suite no integrada: 667/667 correcta.
- DDL AC-12 ejecutado contra PostgreSQL real dentro de una transacción y revertido intencionalmente: `VALIDACION_DDL_OK_ROLLBACK`.
- Migración AC-12 aplicada posteriormente y verificada de forma no destructiva: existen las cuatro tablas de entrega, `email_queue.message_id`, `email_queue.sent_at`, `email_attachment.sha256` y los tres permisos activos.
- Al momento de la verificación, `aocr_entrega_final` no contiene registros; todavía no se ha ejecutado una entrega E2E controlada.
- Suite integrada global: 13 correctas, 8 fallidas y 2 omitidas. Los ocho fallos ya existentes corresponden a migraciones/fixtures Gate2–Gate5 y subsanación documental, no a AC-12.
- Las rutas `/aocr/RT/DocumentosFinales`, `/aocr/Inspector/DocumentosFinales` y `/aocr/Documento/DescargarFinal` respondieron y bloquearon correctamente la navegación sin sesión mediante redirección al login.

## Limitaciones de evidencia

- No se ejecutó entrega mutante ni SMTP sobre producción porque no existe un expediente controlado con ambas firmas válidas.
- No se verificó la ceremonia E2E con identidades RT/Inspector/DIRDAC ni acceso cruzado real.
- El navegador automatizable no tenía ninguna instancia conectada, por lo que no hay evidencia visual interactiva en las siete resoluciones. La compilación Razor y las pruebas estáticas sí verifican las reglas responsive y `Url.Action`.
- Permanece la advertencia heredada de `itext.commons 9.5.0.0` respecto del framework de destino.

La implementación y su esquema quedan listos para validación operativa en QA, pero no debe declararse aceptada en producción hasta ejecutar un caso E2E controlado con SMTP de prueba y las siete resoluciones.
