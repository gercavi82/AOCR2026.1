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

### Vista previa del oficio DCAV - 20/10/2026

Se corrigio un inicio duplicado de funcion JavaScript sin cierre en `Views/Dircav/Bandeja.cshtml`, responsable de Unexpected end of input y del bloqueo de los modales. Se valido la sintaxis del bloque completo con Node.

AOCR003 tiene inspector propuesto y estado PENDIENTE_DIRCAV, pero cero designaciones formales vigentes. Para ese caso, la vista previa ahora arma un borrador con los datos del inspector y las estaciones planificadas, sin crear designaciones ni firmas. El PDF muestra BORRADOR - SIN DESIGNACION FORMAL y la marca VISTA PREVIA NO OFICIAL. La construccion oficial y la firma siguen requiriendo designacion formal. Se muestra tambien TempData Error en el detalle para que los fallos del PDF sean visibles.

Verificacion: compilaciones Debug/Release correctas y 26 comprobaciones de integracion local, incluidas generacion de PDF para el caso sin designacion, ausencia de escrituras de designacion, precondicion oficial conservada y acceso DIRDAC rechazado. No se genero ni firmo un documento real en el servidor.

Publicar conjuntamente los DLL actualizados de `public/public1/bin` (Datos, Modelo, Negocio y Presentacion) y `public/public1/Views/Dircav/Bandeja.cshtml` y `Detalle.cshtml`. Esta carpeta local ya esta actualizada. La copia al servidor sigue pendiente; el paquete ZIP del 19/10 no contiene esta correccion.

### Texto del oficio y pais del explotador - 20/10/2026

Se reemplazo EQUIPO INSPECTOR por INSPECTOR DESIGNADO POR LA DIRECCION DE CERTIFICACION Y VIGILANCIA CONTINUA y se retiro Aeronavegabilidad del cargo principal. Alcance y Mandato Tecnico contiene, en orden: aceptacion documental, facultades RDAC 129 y transporte proporcionado por el operador. Se corrigieron acentos y separacion de parrafos.

La consulta de AOCR003 confirma `pais` vacio; no es un defecto de color o de renderizado. El catalogo local `operadores_operador` tampoco tiene una fila QATAR de la cual recuperar ese dato. Se solicito confirmacion del pais al usuario; no se deduce ni actualiza la nacionalidad a partir del nombre comercial. Cuando falta el valor, el PDF indica Pendiente de registrar. Cuando existe, lo muestra. La correccion de ese dato real queda pendiente de confirmacion.

Validacion: prueba de PDF verifica pais de prueba, ausencia de EQUIPO INSPECTOR y Aeronavegabilidad, orden de los tres parrafos y fecha de estacion. PDF de una pagina renderizado y revisado sin superposiciones ni cortes. Compilaciones Debug/Release correctas. Se actualiza la carpeta local public/public1; falta publicarla en servidor. Los PDF firmados historicos no se modifican.

### Acceso denegado al designar o firmar en DIRCAV

El log del 20/10 a las 09:40 y 10:28-10:30 muestra al usuario GEN_DIRCAV autenticado, pero DesignarInspector/FirmarDesignacion devuelve sesion invalida. El controlador leia exclusivamente Session UsuarioId; la sesion canonica usa UserId, IdUsuario y CodigoUsuario. El 401 era convertido por Forms Authentication en redireccion a Login y luego por el filtro global en NoAutorizado.

El controlador ahora lee los identificadores canonicos, mantiene compatibilidad con UsuarioId y recupera el contexto autenticado si faltan. No cambia la autorizacion del rol ni las precondiciones de designacion/firma. Tres pruebas correctas: resolucion de las cuatro claves con DIRCAV autorizado y DIRDAC rechazado, recursos publicados y ciclo de vida del controlador. Compilaciones Debug/Release correctas. Publicar `public/public1/bin/CapaPresentacion.dll`, actualizado localmente y verificado por hash, y reintentar la operacion. No se ejecuto ninguna firma real ni se modificaron roles en servidor.

### Conflicto 409 al firmar el borrador

Se confirmo por consulta que AOCR003 sigue PENDIENTE_DIRCAV y tiene cero designaciones vigentes. La vista mostraba firma por existir inspector propuesto; la firma oficial exige una designacion formal. Ahora el detalle consulta esa existencia, muestra Pendiente de designacion formal y explica aceptar documentacion y Designar / Reasignar Inspector antes de habilitar la firma.

Un conflicto de firma en formulario normal vuelve al detalle con el motivo visible, en lugar de HTTP 409 sin pantalla. Las solicitudes AJAX conservan 409 y reciben JSON explicativo. Se registra `[DIRCAV][FIRMAR_DESIGNACION_RESULTADO]` sin archivo ni contrasena del certificado. Tres pruebas correctas, incluida redireccion al expediente con mensaje ante conflicto; compilacion Debug/Release correcta. La carpeta local public/public1 contiene DLL actualizados y Views/Dircav/Detalle.cshtml; falta publicar en servidor. No se creo designacion ni se firmo ningun documento real.

### Aceptacion documental transaccional y devolucion a Coordinacion

Se corrigieron incompatibilidades de AocrDesignacionDAO con la base publicada: solicitud sin version, documentos en aocr_tbdocumento (singular, sin activo) y auditoria con tabla_afectada obligatoria y datos_anteriores/datos_nuevos JSONB. AuditoriaDAO tiene una insercion compatible que participa de la misma transaccion y se reutiliza en aceptacion, designacion y registro de firma. Se mantienen los bloqueos FOR UPDATE, las restricciones de estado y la version cuando existe. Los errores del DAO ahora se registran por el logger del sistema. Los correos de aceptacion/designacion se resuelven por el coordinador del expediente, eliminando el destinatario fijo ficticio anterior.

El log de 11:54 confirma que DevolverCoordinador modifico la solicitud antes de fallar en datos_previos. Ahora estado y auditoria de devolucion se confirman o revierten juntos. Para el expediente ya devuelto se habilita una nueva remision desde Coordinacion, sin saltar directamente a la aceptacion DCAV ni modificarlo manualmente. La aceptacion fuera de estado permitido vuelve al detalle con mensaje visible.

Verificacion: 36 comprobaciones de integracion local correctas, incluidas aceptacion con el esquema publicado, auditoria, rechazo de repeticion/observaciones, rollback sin historial parcial, devolucion atomica y nueva remision del expediente devuelto. Tres pruebas del controlador correctas y compilacion Debug/Release correcta. DLL actualizados en public/public1/bin y vista SolicitudAOCR/Detalle.cshtml actualizada localmente. Falta publicar estos archivos. Luego Coordinacion debe remitir nuevamente AOCR003 y DCAV podra aceptar su documentacion y continuar con designacion/firma. No se ejecutaron cambios sobre el expediente real.
