# Implementación de las comunicaciones 13.1 a 13.4

La comunicación 13.4 reemplaza el ingreso de fechas por el RT. El RT solicita lugares; el Inspector acuerda por email las fechas de cada estación y las registra antes de aceptar la solicitud. Para las órdenes ya pagadas con valores faltantes, se aplicó el criterio confirmado: devolución a Financiero, corrección y nueva validación.

## Cambios

- El formulario del RT conserva estaciones/localidades, sin controles ni obligatoriedad de fechas. El backend ignora las fechas enviadas por el RT y conserva las ya acordadas por el Inspector.
- Las estaciones nuevas quedan con fechas nulas. No se inventa la fecha actual. Las fechas históricas permanecen intactas.
- El Inspector asignado accede a Planificación. Se validan todas las estaciones, fechas, duplicados y versiones; la inspección y sus fechas se confirman dentro de una transacción.
- El oficio de aceptación/designación incorpora las fechas acordadas. El cierre y la aceptación documental requieren planificación completa.
- El formulario identifica emisión, renovación o modificación; el correo de habilitación también identifica el tipo. Se actualizaron “Guardar y enviar solicitud” y “Enviar actualización de documentación devuelta”.
- La designación RT permite ingresar nombre/cargo del Gerente de Operaciones y lugar de emisión para descargar la solicitud de aceptación, firmarla y cargarla.
- Los documentos devueltos de inspección pueden regenerarse desde subsanación. La nueva versión queda registrada mediante la carga y reenvío existentes; no se sobrescribe el archivo firmado anterior.
- Se agregó aviso interno al Coordinador para documentación inicial y revisión finalizada, independiente del canal de correo. El RT recibe copia en eventos adicionales de revisión y remisión.
- El Inspector puede devolver la orden pagada a Financiero desde el detalle de inspección. La bandeja financiera muestra las correcciones pendientes.
- Financiero conserva las líneas originales, corrige cantidades/importes y agrega conceptos faltantes. Los totales se recalculan en servidor; se conservan instantáneas de la orden y sus detalles anteriores.
- La validación exige referencia, factura rectificada, comprobante verificado y un monto que cubra el total corregido. Se rechazan formularios de ciclos anteriores y totales desactualizados. La aprobación habitual no puede omitir esta validación.
- El expediente tiene bloqueos de avance durante la corrección pendiente; se avisa al RT/Inspector tras la validación. La descarga de factura utiliza la versión rectificada validada.

## Publicación

1. Aplicar `scripts/db/20261012_fechas_inspector.sql` sobre la base AOCR de destino **antes** de publicar los binarios. El script admite repetir su ejecución y no borra expedientes ni modifica usuarios. Permite fechas pendientes y crea la auditoría de correcciones financieras.
2. Publicar las capas, vistas y scripts actualizados. Incluir `Views/Financiero/CorregirOrden.cshtml`.
3. Probar con los usuarios reales: aceptación RT, envío a Coordinador, devolución y reenvío documental, fechas del Inspector/oficio, devolución financiera y revalidación.
4. Verificar destinatarios activos, procesamiento de `email_queue` y recepción en Carbonio. Confirmar acceso de la cuenta DCAV/DIRCAV. No se crearon cuentas ni se cambiaron contraseñas.
5. Financiero debe efectuar la regularización institucional de la factura y adjuntar sus respaldos antes de validar. Este flujo no corrige automáticamente un FR3 ya emitido en AS400; registra la evidencia de regularización y conserva el historial anterior.

La migración se ejecutó únicamente en una base temporal aislada de pruebas; no se aplicó a la base del sistema ni se publicó el proyecto.

## Validación realizada

- Compilación Debug de `AOCR.sln`, incluyendo compilación de vistas Razor: correcta. Persisten advertencias anteriores del proyecto.
- 62 pruebas seleccionadas de fechas, autorizaciones, notificación RT, revisión documental, envío a Coordinación y estados financieros: correctas.
- `node scripts/test_inspection_dates.cjs`: correcto; selección de lugares sin fechas, localidad obligatoria, recuperación histórica y desmarcado.
- PostgreSQL 18 aislado: 25 verificaciones de persistencia, vínculos, auditoría, montos insuficientes, formularios desactualizados, conservación de IDs, fechas pendientes y rollback. Migración ejecutada dos veces sin alterar la orden corregida.
- `git diff --check`: correcto.

Para repetir las pruebas de persistencia después de compilar Debug:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test_comunicaciones_persistencia.ps1
```

El runner usa solo `127.0.0.1:55439`, crea una base desechable bajo `.tmp`, no reutiliza las conexiones del sistema y detiene PostgreSQL al finalizar. Se pueden ajustar las rutas de PostgreSQL y del compilador mediante los parámetros del script.
