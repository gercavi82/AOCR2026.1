# REPORTE_CORRECCION_ESTABILIDAD_AOCR

## 1. Resumen ejecutivo

Se aplico un parche acotado de idempotencia real para el correo especifico `SOLICITUD_OBSERVADA`. La llave ahora se basa en la ocurrencia real de transicion registrada en historial (`codigo_historial`) y no en `SolicitudId + Estado`.

La supresion del correo generico `AOCR_CAMBIO_ESTADO` para `OBSERVADA` y `ACEPTACION_DOCUMENTAL` ya estaba aplicada en `SolicitudEstadoTransitionBL`.

En cierre posterior del mismo dia se corrigio tambien el caso sensible `SUBSANADA` originado desde `SubsanarPost`, sin apagar globalmente el evento `SUBSANADA`. El detalle queda en `docs/REPORTE_SUBSANACION_DOCUMENTAL_AOCR.md`.

## 2. Archivos modificados

- `CapaDatos/DAOs/HistorialEstadoDAO.cs`
- `CapaNegocio/SolicitudEstadoTransitionBL.cs`
- `CapaNegocio/Services/SolicitudAocrCorreoService.cs`
- `AOCR.Tests/Unit/OperationalFlowCharacterizationTests.cs`
- `docs/REPORTE_CORRECCION_ESTABILIDAD_AOCR.md`
- `docs/REPORTE_SUBSANACION_DOCUMENTAL_AOCR.md`

## 3. Correcciones aplicadas

- Se agrego `RegistrarCambioYObtenerCodigo(...)` para insertar historial y devolver el `codigo_historial` generado.
- `RegistrarCambio(...)` conserva su firma y delega al nuevo metodo, sin romper callers existentes.
- `SolicitudEstadoTransitionBL` conserva el `codigo_historial` de la transicion y lo pasa al correo especifico del workflow.
- `SolicitudAocrCorreoService` construye `EventKey` fuerte solo para `OBSERVADA`.
- Antes de encolar con `EventKey`, se consulta `email_queue` para omitir reencolado si ya existe la misma llave.
- `SubsanarPost` ahora cambia a `Subsanada` mediante un helper especifico que omite el correo generico y el correo workflow solo en esa ruta, conservando `DOCUMENTACION_SUBSANADA_RT`.

## 4. Correcciones no aplicadas y motivo

- `PAGO_APROBADO`: no se aplico nuevo parche por falta de caso runtime real.
- `PENDIENTE_ASIGNACION_INSPECTOR`: no se aplico patron automatico por decision funcional pendiente.
- `AOCR_LEGALIZADO`: fuera de alcance por legalizacion.
- `AOCR_EMITIDO_RECIBIDO`: fuera de alcance por emision/finalizacion.
- Correos con adjuntos: fuera de alcance por sensibilidad documental.

## 5. Estado de OBSERVADA

Intervenido con EventKey fuerte:

`AOCR:SOLICITUD_OBSERVADA:{SolicitudId}:{CodigoHistorial}:{DestinatarioNormalizado}`

Ejemplo:

`AOCR:SOLICITUD_OBSERVADA:7:65:correo@dominio.com`

## 6. Estado de ACEPTACION_DOCUMENTAL

La supresion del generico `AOCR_CAMBIO_ESTADO` esta aplicada y preserva campanas internas. No se agrego EventKey fuerte en este parche para mantener el slice limitado a `OBSERVADA`.

## 7. Estado de SUBSANADA + SubsanarPost

Corregido con alcance especifico de `SubsanarPost`. Esa ruta conserva la notificacion documental `DOCUMENTACION_SUBSANADA_RT` y evita duplicar con `AOCR_CAMBIO_ESTADO` / `SOLICITUD_SUBSANADA`.

No se apago `SUBSANADA` globalmente porque existe otra ruta a `Subsanada` sin la notificacion manual del controlador.

## 8. Estado de PAGO_APROBADO

Sin cambios nuevos. Permanece como riesgo abierto con protocolo runtime pendiente.

## 9. Estado de idempotencia EventKey

`SOLICITUD_OBSERVADA` ahora usa ocurrencia real (`codigo_historial`). Si no existe `codigo_historial` ni `correlation_id`, el helper devuelve `null` y no inventa una llave debil.

## 10. Estado de historial / trigger

No se toco `trg_cambio_estado`. El cambio solo captura el identificador del historial ya registrado por aplicacion.

## 11. Validacion de bandejas y badges

No se modificaron bandejas, vistas, rutas, roles, autorizaciones ni badges. Validacion runtime UI queda pendiente para ambiente con datos.

## 12. Validacion email_queue

Validacion BD pendiente de corrida runtime. Consulta sugerida:

```sql
SELECT
    id,
    tipo_notificacion,
    para,
    solicitud_id,
    event_key,
    correlation_id,
    created_at
FROM email_queue
WHERE solicitud_id = @SolicitudId
  AND created_at >= @FechaInicioPrueba
ORDER BY created_at DESC;
```

## 13. Guardrails respetados

No se tocaron descargas, `Finalizado`, roles, autorizaciones, vistas, rutas, actions, nombres de estados, legalizacion, `AOCR_EMITIDO_RECIBIDO`, adjuntos institucionales ni textos institucionales.

El unico cambio sobre `SubsanarPost` fue aislar el origen del cambio a `Subsanada` para controlar correos duplicados; no se altero carga, versionado ni revision documental.

## 14. Pruebas ejecutadas

- Build completo: `MSBuild.exe AOCR.sln /t:Build /p:Configuration=Debug /v:m /nr:false` OK.
- Tests: `vstest.console.exe AOCR.Tests\bin\Debug\AOCR.Tests.dll /Logger:Console` OK.
- Resultado: 158 pruebas totales, 157 correctas, 1 omitida.

## 15. Riesgos pendientes

- Validacion runtime de doble POST/doble clic/refresh para `OBSERVADA`.
- Idempotencia fuerte pendiente para `ACEPTACION_DOCUMENTAL`.
- `PAGO_APROBADO` requiere solicitud candidata real.
- El frente historial/trigger sigue separado.

## 16. Siguiente recomendacion

Validar en BD una transicion real a `OBSERVADA` y una subsanacion real desde `SubsanarPost`. Para la subsanacion, confirmar que el inspector reciba solo el correo externo `DOCUMENTACION_SUBSANADA_RT` asociado a esa correccion documental.
