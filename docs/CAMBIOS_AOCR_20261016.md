# Cambios solicitados el 16 de octubre de 2026

Implementados a partir de `cambios AOCR.pdf`. No se publicaron archivos ni se modificaron datos de produccion.

- Orden nueva: INSPECCION_EXT toma la cantidad de estaciones seleccionadas. El servidor recalcula la cantidad y el subtotal antes de calcular el total; el formulario muestra el cambio y bloquea la edicion manual de esa cantidad.
- Comprobante: confirmacion adicional al RT registrado en la solicitud y al correo de la orden; destinatarios deduplicados y clave de evento por comprobante y destinatario. No implica aprobacion del pago. Se conserva el aviso a Financiero.
- Correos: asignacion muestra En revision; cierre documental muestra Revision documental completada y Para revision de la Coordinacion. Se retira del aviso de asignacion la instruccion informal agregada, conservando datos estructurados e historial.
- Planificacion: conserva rangos independientes por estacion, contacto y observaciones. Retira campos generales desde fecha hasta equipos. Los campos retirados no sobrescriben los valores historicos cuando no se reciben. La fecha general se deriva del primer rango registrado. Confirmacion antes de guardar y de finalizar la revision documental.
- Coordinacion: la accion existente de revision final remite a DCAV; no firma ni confirma/reasigna al inspector ni genera el oficio de aceptacion. Conserva la devolucion con observaciones.
- DCAV: se reutiliza su aceptacion y firma digital institucional existentes. La remision queda PENDIENTE_ACEPTACION_DCAV y la firma de designacion actualiza ACEPTADA_POR_DCAV, inspector confirmado y habilitacion LV/Informe dentro de la misma transaccion de firma. No se reescriben PDFs firmados historicos.
- Oficio DCAV: titulo de aceptacion de documentacion y designacion solicitado; texto de aceptacion y transporte proporcionado por el operador. La constancia preliminar conserva su naturaleza de revision, sin atribuirle una firma de DCAV.
- Generacion AOCR: solo Inspector; bloqueo adicional en la ruta antigua y servicio, controles de Coordinacion deshabilitados. Se conservan los permisos de firma final de DCAV/DIRDAC y las validaciones de asignacion existentes de FirmaAocr.
- Se elimina el destinatario ficticio de la designacion firmada y se resuelve el correo del inspector por su usuario.

## Verificacion

- Compilacion con vistas Razor.
- 62 pruebas seleccionadas correctas de flujo documental, firma DCAV, fechas y nuevos casos de correo/permisos/PDF.
- Prueba JavaScript `node scripts/test_cambios_aocr_20261016.cjs`: cantidades, importes, cero estaciones y ausencia de duplicados.
- Revision visual del PDF oficial generado con datos de prueba: una pagina, texto y firma sin superposiciones.
- Ampliacion a 78 pruebas: 72 correctas y 6 fallos de caracterizacion preexistentes en Ac02MatrizPruebasObligatoriasTests que buscan cadenas de acciones de estaciones ausentes tambien en HEAD. No se alteran esas pruebas ni las rutas ajenas al cambio.

## Antes de publicar

1. Publicar conjuntamente las DLL de datos, negocio y presentacion y las vistas modificadas. Este cambio reutiliza las columnas existentes de revision documental; no altera usuarios, roles ni registros ya firmados.
2. Verificar que estan aplicadas las migraciones previas de estaciones, correccion financiera y revision documental/DCAV del proyecto.
3. En un entorno de pruebas, ejecutar un tramite completo: seleccionar dos estaciones, comprobar cantidad e importe, cargar comprobante, planificar fechas, finalizar revision, remitir desde Coordinacion, aceptar/designar y firmar con un certificado DCAV valido, comprobar desbloqueo del inspector y generacion AOCR.
4. Confirmar entrega real de los correos en RT e Inspector. La aceptacion SMTP no acredita entrega en bandeja.
5. Los expedientes aceptados antes de este cambio conservan su historial. Cualquier regularizacion retroactiva requiere una decision especifica, sin reemplazar silenciosamente documentos firmados.


## Correccion de remision DCAV - 19/10/2026

Se detecto que la base publicada no tiene `aocr_tbsolicitud.version` ni `aocr_tbauditoria.datos_previos`, y almacena `datos_nuevos` como JSONB. La transaccion anterior suponia otra estructura y se revertia; ademas, AOCR003 no tenia fila de control de Coordinacion.

La remision ahora detecta las columnas disponibles, tipa los valores de auditoria como JSON/JSONB o texto segun corresponda y crea el control faltante dentro de la misma transaccion. Conserva el bloqueo de solicitud y rechaza un segundo envio. La pantalla muestra el resultado y el log registra `[COORD][REMITIR_DCAV_RESULTADO]`. Se incluye tambien el CSS de detalle que faltaba en la publicacion.

Validacion: `scripts/test_remision_dcav.ps1` ejecuta 11 comprobaciones sobre PostgreSQL temporal local (puerto 55440), sin correos ni cambios en expedientes reales. Cubre esquema sin version y con auditoria JSONB, control faltante, envio repetido, rollback y esquema con version/auditoria de texto.

Publicar las DLL de Datos, Modelo, Negocio y Presentacion junto con la vista de detalle y los recursos. Luego, el coordinador debe volver a remitir AOCR003 desde la pantalla. No se ha avanzado manualmente el estado del expediente en la base real.
