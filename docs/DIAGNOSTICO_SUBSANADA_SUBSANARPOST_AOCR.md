# DIAGNOSTICO_SUBSANADA_SUBSANARPOST_AOCR

## 1. Resumen

`SUBSANADA` queda clasificado como `D. duplicidad real pero sensible`.

La evidencia runtime validada en la solicitud 7 el 2026-06-02 entre 08:13:53 y 08:13:56 confirma tres correos externos para la misma subsanacion: `AOCR_CAMBIO_ESTADO`, `SOLICITUD_SUBSANADA` y `DOCUMENTACION_SUBSANADA_RT`. El inspector `luis.catota@aviacioncivil.gob.ec` recibio los tres. Coordinacion recibio solo `SOLICITUD_SUBSANADA` y el operador/RT recibio solo `AOCR_CAMBIO_ESTADO`.

El caso no debe resolverse apagando `SUBSANADA` de forma global, porque existe otra ruta legitima a `Subsanada` en `SolicitudAOCRController.MarcarSubsanadaDespuesDeGuardar(...)` que no ejecuta la notificacion manual al inspector.

## 2. Estado actual del plan

`OBSERVADA` y `ACEPTACION_DOCUMENTAL` ya quedaron remediados y validados en build y runtime.

`PENDIENTE_ASIGNACION_INSPECTOR` fue reclasificado fuera de duplicidades automaticas confirmadas y requiere definicion funcional aparte.

`PAGO_APROBADO` quedo como riesgo abierto: la supresion ya esta activa en codigo, pero sin cierre runtime.

El siguiente frente real de correo AOCR es `SUBSANADA`, pero solo desde diagnostico tecnico y sin tocar aun `SubsanarPost`, descargas, vistas, rutas ni estados sensibles.

## 3. Nota PAGO_APROBADO pendiente runtime

La consulta de candidatos sobre `aocr_tbsolicitud` no devolvio filas para estados candidatos al flujo post-pago en el ambiente auditado. Eso impide demostrar todavia, con un caso vivo, que la supresion ya codificada evita el nuevo pareo `AOCR_CAMBIO_ESTADO` + `PAGO_APROBADO`.

Conclusion operacional: `PAGO_APROBADO` no debe cerrarse como remediado aunque el codigo ya este activo.

## 4. Flujo actual de SubsanarPost

1. `SolicitudAOCRController.SubsanarPost(...)` valida que la solicitud este en `Observada`, verifica permisos y obtiene documentos pendientes de subsanacion.
2. El controlador guarda archivos corregidos, registra revisiones documentales e historial asociado.
3. Luego llama `CambiarEstadoConReglasAocr(codigoSolicitud, EstadoSolicitud.Subsanada, ...)`.
4. `SolicitudEstadoTransitionBL.CambiarEstadoConReglasAocr(...)` persiste el estado, registra historial de estado y ejecuta `NotificarCambioEstadoAocr(...)`.
5. `NotificarCambioEstadoAocr(...)` para `Subsanada` no omite el generico, por lo que dispara campanas internas y `AOCR_CAMBIO_ESTADO`, y ademas resuelve `SUBSANADA` hacia `SolicitudAocrCorreoService.NotificarEvento(...)`.
6. De regreso en el controlador, `SubsanarPost(...)` llama `NotificarInspectorDocumentacionSubsanada(...)`, que crea campana interna al inspector y encola `DOCUMENTACION_SUBSANADA_RT` con `EventKey` propio.

Observacion clave: existe otra ruta a `Subsanada` en `SolicitudAOCRController.MarcarSubsanadaDespuesDeGuardar(...)`. Esa ruta cambia estado a `Subsanada`, pero no ejecuta `NotificarInspectorDocumentacionSubsanada(...)`. Cualquier parche que apague `SUBSANADA` en el pivote global afectaria tambien esa ruta.

## 5. Correos/notificaciones generados

Evidencia persistida para solicitud 7 el 2026-06-02:

- `AOCR_CAMBIO_ESTADO` id 127 a `mancho2002@hotmail.com` con `EventKey=AOCR:CAMBIO_ESTADO:7:45:SUBSANADA` a las 08:13:53.
- `AOCR_CAMBIO_ESTADO` id 128 a `luis.catota@aviacioncivil.gob.ec` con `EventKey=AOCR:CAMBIO_ESTADO:7:43:SUBSANADA` a las 08:13:54.
- `SOLICITUD_SUBSANADA` id 129 a `luis.catota@aviacioncivil.gob.ec` a las 08:13:55.
- `SOLICITUD_SUBSANADA` id 130 a `gercavi82@gmail.com` a las 08:13:55.
- `SOLICITUD_SUBSANADA` id 131 a `german.cajas@aviacioncivil.gob.ec` a las 08:13:55.
- `DOCUMENTACION_SUBSANADA_RT` id 132 a `luis.catota@aviacioncivil.gob.ec` con `EventKey=DOCUMENTACION_SUBSANADA_RT_7_43_119V3` a las 08:13:56.
- Campana interna 40 a usuario 45 con titulo `Cambio de Estado` a las 08:13:53.
- Campana interna 41 a usuario 43 con titulo `Cambio de Estado` a las 08:13:53.
- Campana interna 42 a usuario 43 con titulo `Documentacion subsanada` a las 08:13:56.
- Historial de estado 63: `Observada -> Subsanada`, usuario 45, observacion `Subsanacion documental enviada por el operador. Comentario: Revalidacion post-fix de cambio a Subsanada.`.

## 6. Destinatarios

Inspector:
`luis.catota@aviacioncivil.gob.ec` recibe `AOCR_CAMBIO_ESTADO`, `SOLICITUD_SUBSANADA` y `DOCUMENTACION_SUBSANADA_RT`. Tambien recibe dos campanas internas diferentes: `Cambio de Estado` y `Documentacion subsanada`.

Operador o RT:
`mancho2002@hotmail.com` recibe `AOCR_CAMBIO_ESTADO` para la transicion a `Subsanada`.

Workflow `SUBSANADA`:
la evidencia de cola muestra ademas `gercavi82@gmail.com` y `german.cajas@aviacioncivil.gob.ec`, consistente con la politica de destinatarios de `GrupoInspectorAsignado` y `GrupoCoordinacionInspeccion`.

Lectura funcional: el solapamiento real ocurre en el inspector; coordinacion y operador/RT conservan superficies distintas.

## 7. Duplicidad confirmada o descartada

Duplicidad confirmada.

Clasificacion: `D. duplicidad real pero sensible`.

La duplicidad es real porque el inspector recibe mas de un correo por el mismo hecho funcional y, en la corrida validada, recibe tres. Es sensible porque no todos los destinatarios son iguales y porque la misma transicion `Subsanada` tambien se usa en otra ruta sin notificacion manual del inspector.

## 8. Riesgos

- Apagar `SUBSANADA` en `SolicitudEstadoTransitionBL` de forma global puede romper la otra ruta `MarcarSubsanadaDespuesDeGuardar(...)`, donde hoy no existe `DOCUMENTACION_SUBSANADA_RT`.
- Tocar `SubsanarPost(...)` sin aislamiento puede alterar carga de archivos, historial documental, permisos o secuencia de persistencia.
- `SOLICITUD_SUBSANADA` hoy no tiene `EventKey`; un retry o doble submit puede volver a encolarlo a coordinacion y a cualquier destinatario adicional del grupo.
- Suprimir `AOCR_CAMBIO_ESTADO` sin revisar destinatarios puede quitar la confirmacion externa al operador/RT.
- Mantener el estado actual deja triple contacto al inspector en casos como la solicitud 7 y aumenta ruido operativo.

## 9. Recomendacion

No aplicar un parche global sobre `SUBSANADA` todavia.

La decision correcta debe separar semanticas por origen del cambio de estado. Cuando `Subsanada` nace desde `SubsanarPost(...)`, el inspector ya recibe una notificacion manual rica en detalle documental. Ese camino necesita tratamiento distinto del camino general `MarcarSubsanadaDespuesDeGuardar(...)`, donde la notificacion manual no existe.

La recomendacion tecnica es preservar el correo manual del inspector, preservar la confirmacion al operador/RT que realmente se quiera mantener y mover la decision de coordinacion a una superficie explicita, no implicita en un disparo global de `SUBSANADA`.

## 10. Parche futuro sugerido, sin aplicar

1. Introducir un contexto de origen para la transicion a `Subsanada` o un wrapper explicito que permita distinguir `SubsanarPost(...)` del camino general `MarcarSubsanadaDespuesDeGuardar(...)`.
2. Cuando el origen sea `SubsanarPost(...)`, conservar `DOCUMENTACION_SUBSANADA_RT` para inspector y evitar que el inspector vuelva a entrar por `SOLICITUD_SUBSANADA`.
3. Mantener el aviso que corresponda al operador/RT de forma explicita; si se decide conservar `AOCR_CAMBIO_ESTADO`, que sea por necesidad funcional y no por arrastre del disparo global.
4. Si coordinacion debe seguir enterandose en `SubsanarPost(...)`, enviar esa notificacion por un canal especifico a coordinacion o restringir `SOLICITUD_SUBSANADA` a coordinacion en ese origen.
5. Agregar `EventKey` a `SOLICITUD_SUBSANADA` antes de cerrar la remediacion para evitar reencolados por retry.
6. No cambiar la semantica actual de `SUBSANADA` para la ruta `MarcarSubsanadaDespuesDeGuardar(...)` hasta tener prueba dedicada de esa superficie.

## 11. Pruebas necesarias

- Ejecutar `SubsanarPost(...)` con solicitud en `Observada` y un inspector asignado; verificar cantidades exactas de campanas y correos por destinatario.
- Validar especificamente que el inspector no reciba mas de un correo externo si esa es la semantica objetivo futura.
- Ejecutar la ruta `MarcarSubsanadaDespuesDeGuardar(...)` desde una solicitud `Observada`; verificar que una remediacion futura no deje esa ruta sin notificacion necesaria.
- Probar retry y doble clic en la subsanacion para medir reencolado de `SOLICITUD_SUBSANADA` mientras siga sin `EventKey`.
- Verificar UI y BD de historial de estado, historial documental y notificaciones internas antes y despues de cualquier parche futuro.
- Confirmar con negocio si coordinacion debe seguir recibiendo aviso en toda subsanacion o solo en determinados origenes.