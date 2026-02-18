# RCA - Ordenes Financiero

Fecha: 2026-02-13
Modulo: Ordenes de Recaudacion (Financiero)

## Resumen
Se identificaron fallos por mapeo de columnas inexistentes en DAO, inserciones con columnas fuera de esquema, vistas faltantes y flujo de correo no confiable. Se corrigieron los puntos con mapeo seguro por columnas, SQL minimo compatible, nuevas vistas financieras y encolado de correos con plantillas HTML.

## Hallazgos (causa raiz, reproduccion, fix, verificacion)

1) Error 500 al cargar bandeja/detalle financiero
Causa raiz:
- `CapaDatos/DAOs/OrdenRecaudacionDAO.cs` en `MapearOrden` y `MapearDetalle` accedia columnas que no existen en algunos queries (por ejemplo `observacion`, `codigo_usuario`, `nombre_contribuyente`, `email_contribuyente`), generando `IndexOutOfRangeException` o `InvalidCastException` al leer `NpgsqlDataReader`.
Como reproducir:
- Abrir la bandeja financiera o detalle de orden con consultas que no incluyen esas columnas.
Fix aplicado:
- Se incorporo mapeo por conjunto de columnas (`GetColumnSet`, `HasColumn`, `GetSafeString`, `GetSafeDecimal`, `GetSafeInt32`) y se ajusto `MapearOrden` y `MapearDetalle` para no romper cuando la columna no existe.
Verificacion:
- Revisar consulta y navegar bandeja/detalle sin excepciones de mapeo.

2) Error SQL al registrar detalle de orden
Causa raiz:
- `InsertarDetalle` insertaba columnas no existentes en `aocr_or_detalle` (subtotal, iva, total, etc.), provocando error SQL en Postgres.
Como reproducir:
- Crear orden y registrar detalle con flujo que llame `InsertarDetalle`.
Fix aplicado:
- SQL reducido a columnas reales: `orden_id`, `concepto_id`, `concepto_nombre`, `cantidad`, `valor_unitario`, `total_linea`.
Verificacion:
- Insercion correcta de detalle en BD y orden creada sin error.

3) Correos financieros no confiables
Causa raiz:
- Envio directo con HTML en controlador y sin reintentos; fallos SMTP no quedaban trazados y el flujo quedaba inconsistente.
Como reproducir:
- Aprobar/rechazar orden con SMTP intermitente.
Fix aplicado:
- Encolado con `EmailQueueService`, plantillas Razor HTML y trazabilidad por `CorrelationId`. Se guardan adjuntos por ruta fisica.
Verificacion:
- Registro en `email_queue` con estado `PENDIENTE` y envio por worker.

4) Vista faltante en detalle financiero
Causa raiz:
- No existia la vista `Financiero/DetalleOrden`, generando 404/500 en navegacion.
Como reproducir:
- Navegar a detalle desde bandeja o URL directa.
Fix aplicado:
- Se creo `CapaPresentacion/Views/Financiero/DetalleOrden.cshtml` y se actualizo bandeja para usarla.
Verificacion:
- Vista renderiza orden, pago, acciones y historial.

5) Parametros y tipos inconsistentes al actualizar solicitud
Causa raiz:
- `ActualizarCodigoSolicitudOrden` recibia string para `codigo_solicitud` cuando el tipo real es entero.
Como reproducir:
- Actualizar codigo de solicitud con `ActualizarCodigoSolicitudOrden`.
Fix aplicado:
- Se normalizo parametro a entero y se mantuvo parametrizacion.
Verificacion:
- Update ejecuta sin error de cast.

## Notas
- No se ejecuto build ni pruebas automaticas en esta intervencion.
