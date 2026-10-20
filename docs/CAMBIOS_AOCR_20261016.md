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

Publicar las DLL de Datos, Modelo, Negocio y Presentacion junto con la vista de detalle y los recursos. Antes de volver a remitir, ejecutar `scripts/sql/20261019_estados_revision_dcav.sql` en la base usada por el sitio publicado. Luego, el coordinador debe volver a remitir AOCR003 desde la pantalla. No se ha avanzado manualmente el estado del expediente en la base real.

### Restriccion de estados pendiente en servidor

El log del 19/10 a las 16:05:58 confirma que la correccion publicada se ejecuta, pero devuelve PostgreSQL 23514: `chk_aocr_revdoc_coord_estado` rechaza `PENDIENTE_ACEPTACION_DCAV`. La transaccion se revierte y AOCR003 sigue en Aceptacion Documental; por eso no llega a la bandeja DCAV.

La migracion `20261019_estados_revision_dcav.sql` conserva la condicion existente y admite adicionalmente `PENDIENTE_ACEPTACION_DCAV` y `ACEPTADA_POR_DCAV`. Es repetible y no modifica estados de expedientes ni elimina la validacion. Se actualiza tambien el script base de septiembre para instalaciones nuevas.

Validacion ampliada: 13 comprobaciones correctas en PostgreSQL temporal, incluida reproduccion del rechazo con el CHECK antiguo, doble ejecucion de la migracion, remision y estado posterior de firma DCAV. El script aun debe aplicarse en la base del servidor; estas pruebas no la modifican.

### Auditoria de remision - intento 16:49

El ultimo intento supera el CHECK, pero PostgreSQL rechaza el INSERT de auditoria por `tabla_afectada` nula (23502). Se revisaron todas las columnas de la tabla mediante consulta de metadatos: ademas de la estructura anterior, tiene `tabla_afectada` obligatoria, `registro_id`, `usuario_id`, `datos_anteriores` JSONB y fechas de auditoria.

El DAO ahora completa tabla afectada, identificador de solicitud, usuario y fechas disponibles, y utiliza `datos_anteriores` o `datos_previos` segun el esquema. No se relajan restricciones ni se omite la auditoria. Se compilaron Datos y Presentacion y pasaron 14 comprobaciones con una tabla temporal que reproduce las columnas y nulabilidad del servidor, mas compatibilidad con el esquema anterior y rollback.

Publicar el nuevo `CapaDatos.dll` compilado en esta correccion y volver a remitir desde Coordinacion. La prueba no cambia AOCR003 ni publica archivos en el servidor. No requiere otro script SQL para este fallo.

### Remision, bandeja y avisos DCAV completos

El log a las 19:22 aun muestra el error de auditoria `tabla_afectada`. La carpeta local de publicacion contenia DLL distintas de las corregidas. Se compilo Release y se actualizaron `public/public1/bin/CapaDatos.dll`, `CapaModelo.dll`, `CapaNegocio.dll` y `CapaPresentacion.dll`, mas la vista de detalle que muestra el resultado. Se verifico coincidencia SHA256 con la compilacion.

La remision ahora resuelve expresamente usuarios activos con rol DIRCAV o DCAV; no utiliza el grupo generico de Direccion. Persiste el estado `PENDIENTE_DIRCAV`, el control documental, auditoria, aviso interno para cada usuario y correo en cola para cada direccion distinta en una sola transaccion. Si falla el aviso o el correo, revierte todo; un segundo clic no duplica envios. Se conserva la comunicacion transaccional al correo de la solicitud. Sin usuarios DCAV activos con correo, muestra un error de configuracion en lugar de aparentar un envio exitoso.

El aviso indica documentos pendientes de aprobacion y abre `/Dircav/Bandeja?tab=documentacion`, respetando el directorio virtual del sitio. DCAV debe revisar la pestana Documentacion, no la pestana Remision a DIRDAC. Los correos se procesan por la cola existente; encolado no equivale a entrega SMTP confirmada.

Validacion: compilacion Debug y Release correctas; 22 comprobaciones de integracion correctas con PostgreSQL temporal local. Se verificaron resolucion del destinatario activo, exclusion de DIRDAC/cuentas inactivas, aparicion de la solicitud en `DircavBandejaService`, aviso interno, correo pendiente, ausencia de duplicados, auditoria y rollback ante fallos de auditoria/correo, ambos esquemas compatibles y estados posteriores de firma.

Despliegue: copiar al servidor los cuatro DLL actualizados y la vista de detalle de la carpeta local `public/public1`. Alternativamente, usar `.tmp/entrega-remision-dcav-20261019.zip` (contiene solo esos archivos, SQL y manifiesto SHA256, sin configuraciones). Si la restriccion de estados todavia no se actualizo, ejecutar primero `scripts/sql/20261019_estados_revision_dcav.sql`. Reintentar desde Coordinacion y comprobar `Ok=True` en `[COORD][REMITIR_DCAV_RESULTADO]`; luego acceder como DCAV a Documentacion y revisar su campana y correo. No se publico en el servidor ni se cambio manualmente AOCR003.

### Menu de pendientes DCAV - 20/10/2026

Se agregaron a Mi trabajo los accesos Documentacion por aprobar, Designaciones por firmar e Informes por revisar. Todos muestran el numero de pendientes de su bandeja correspondiente, al igual que Firma de Condiciones y Remision a DIRDAC. Se habilito expresamente la carga de contadores para el rol activo DCAV/DIRCAV, que antes se omitia en el filtro de direccion. Los contadores DCAV usan `DircavBandejaService`, igual que la pantalla de bandejas, sin depender de consultas de DIRDAC.

Compilacion Debug/Release y 16 pruebas SidebarRoleMatrixTests correctas. `public/public1/bin/CapaPresentacion.dll` actualizado y verificado por SHA256; falta copiar esa DLL al servidor. El paquete del 19/10 no incluye este cambio posterior. No cambia estados ni permisos de aprobacion. El log del 20/10 confirma envio SMTP exitoso del aviso de AOCR003 a la cuenta de vigilancia continua; esto no acredita lectura ni entrega en la carpeta de entrada del destinatario.
