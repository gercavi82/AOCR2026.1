# Reporte de estabilidad AOCR - fase correctiva 2026-06-02

## Estado general

- `AOCR.sln` habia quedado validado previamente en esta sesion con compilacion limpia.
- `AOCR.Tests` habia quedado validado previamente con `155` pruebas totales, `154` correctas y `1` omitida.
- En esta fase correctiva no se aplicaron cambios de comportamiento sobre flujos sensibles (`SUBSANADA`, `PENDIENTE_ASIGNACION_INSPECTOR`, `PAGO_APROBADO`, `AOCR_LEGALIZADO`, `AOCR_EMITIDO_RECIBIDO`, triggers/historial).

## Correccion segura aplicada

- Se agregaron caracterizaciones en `AOCR.Tests/Unit/OperationalFlowCharacterizationTests.cs` para congelar el alcance actual de la supresion segura del correo generico AOCR.
- Las nuevas pruebas verifican que `SolicitudEstadoTransitionBL` mantenga dentro de la supresion solo los eventos actualmente confirmados en codigo (`OBSERVADA`, `ACEPTACION_DOCUMENTAL`, `PAGO_APROBADO`).
- Las nuevas pruebas verifican que eventos todavia sensibles o funcionalmente abiertos no entren por accidente a ese mismo patron (`PENDIENTE_ASIGNACION_INSPECTOR`, `SUBSANADA`, `AOCR_LEGALIZADO`, `AOCR_EMITIDO_RECIBIDO`).
- Tambien se congelo que, cuando se omite el correo generico, se sigan preservando tanto la campana interna como el despacho del correo funcional especifico.

## Hipotesis descartada en esta fase

- Se evaluo agregar `EventKey` deterministico en `CapaNegocio/Services/SolicitudAocrCorreoService.cs` para los correos especificos de `OBSERVADA` y `ACEPTACION_DOCUMENTAL`.
- Ese parche no se aplico porque el key natural `solicitud + evento + destinatario` no distingue ciclos legitimos posteriores del mismo estado sobre la misma solicitud.
- En estados repetibles como `OBSERVADA`, un `EventKey` fijo a ese nivel podria bloquear correos validos de una nueva observacion futura y romper el contrato funcional sin evidencia runtime suficiente.
- La deduplicacion de esos correos solo es segura si el `EventKey` nace de un identificador de ocurrencia de transicion real (por ejemplo, historial/audit/transition token), no de la combinacion estatica de solicitud y estado.

## Validacion ejecutada en esta fase

- Compilacion: `MSBuild.exe AOCR.Tests/AOCR.Tests.csproj /t:Build /p:Configuration=Debug /m`
- Resultado: correcta, `0` advertencias, `0` errores.
- Pruebas enfocadas: `SolicitudEstadoTransition_ShouldKeepSafeGenericCorreoSuppressionScopeRestricted`
- Pruebas enfocadas: `SolicitudEstadoTransition_ShouldPreserveInternalNotificationsAndSpecificWorkflowDispatch`
- Resultado: `2/2` correctas.

## Conclusiones operativas

- El siguiente parche de comportamiento sobre correos especificos AOCR no debe hacerse en `SolicitudAocrCorreoService` con un `EventKey` fijo por solicitud/evento/destinatario.
- El frente seguro queda acotado a endurecer caracterizaciones y mantener la separacion entre supresion generica segura y casos sensibles pendientes de validacion runtime.