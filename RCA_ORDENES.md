# RCA_ORDENES

## Resumen ejecutivo
- El 500 en /OrdenRecaudacion/Nueva se originaba por fallas al cargar conceptos/solicitudes sin manejo defensivo (DAO/DB), causando excepciones no controladas.
- El error 500 al entrar a /OrdenRecaudacion/Obligatoria se disparaba al leer registros desde PostgreSQL porque el DAO intentaba leer campos integer como string.
- Las ordenes creadas no aparecian en "Mis Ordenes" porque la creacion usaba Session["UserId"] y el listado usaba Session["IdUsuario"], generando un filtro con un usuario distinto (o 0).
- El sidebar consultaba la BD sin manejo defensivo; ante una falla de BD podia romper cualquier vista que carga el layout.
- El listado de Mis Ordenes fallaba si alguna columna venia NULL (Convert.ToDecimal/DateTime sin validacion).

## Como se reproduce
1) Iniciar sesion con un usuario que tiene Session["UserId"] pero no Session["IdUsuario"].
2) Ir a /OrdenRecaudacion/Obligatoria o hacer clic en "Nueva Orden" desde el sidebar.
3) Se dispara 500 durante la lectura de ordenes (InvalidCastException por codigo_usuario integer leido como string).
4) Crear una orden y luego ir a /Orden/MisOrdenes: no aparece porque el filtro usa otro id de usuario.

## Causa raiz (Root Cause)
5) CapaPresentacion/Controllers/OrdenRecaudacionController.cs: CargarConceptosNueva y el POST estaban desalineados con la vista (VM) y sin manejo defensivo ante errores de BD, provocando 500 en /Nueva.
1) CapaDatos/DAOs/OrdenRecaudacionDAO.cs: MapearOrden usaba reader.GetString para columnas que en DB son integer (codigo_usuario / codigo_solicitud). Npgsql lanza InvalidCastException.
2) CapaPresentacion/Controllers/OrdenController.cs: MisOrdenes y Detalle usan Session["IdUsuario"] solamente, mientras la creacion usa Session["UserId"]. Esto provoca mismatch de ids.
3) CapaPresentacion/Views/Shared/_Sidebar.cshtml: llamadas directas a DAO sin try/catch ni verificacion de id; cualquier exception de BD rompe el layout.
4) CapaPresentacion/Controllers/OrdenController.cs: mapeo directo de columnas con DBNull genera errores al listar.

## Solucion aplicada
- Nueva Orden: el POST ahora recibe OrdenRecaudacionNuevaVM (igual que la vista) y se agrego manejo defensivo al cargar conceptos/solicitudes.
- DAO: usar Convert.ToString(reader["codigo_usuario"]) y Convert.ToString(reader["codigo_solicitud"]) para tolerar integer y text.
- OrdenController: usar Session["IdUsuario"] o Session["UserId"] como fallback; y mapear columnas DBNull a valores seguros.
- Sidebar: fallback de Session["UserId"], y manejo defensivo con try/catch para que el layout no reviente si la BD falla.
- Nueva Orden: validar id de usuario antes de insertar.

## Evidencia tecnica
- Error 500 previo: InvalidCastException "Reading as 'System.String' is not supported for fields having DataTypeName 'integer'" al ejecutar MapearOrden.
- Ruta principal afectada: /OrdenRecaudacion/Obligatoria
- Flujo: _Sidebar -> Layout -> OrdenRecaudacionController.Obligatoria -> OrdenRecaudacionDAO.ListarPorUsuario -> MapearOrden


## DB binding
- OrdenRecaudacionDAO usa Npgsql (PostgreSQL) y toma connectionString de AOCRConnection (o DefaultConnection si existiera).
- No se encontro uso de DB2/AS400 en el flujo de Ordenes de Recaudacion.
## Verificacion
- /OrdenRecaudacion/Obligatoria abre sin 500 con datos cargados.
- Crear Nueva Orden inserta en aocr_or_orden y aparece en /Orden/MisOrdenes.
- Si la BD no responde, el layout carga sin romper (sidebar degradado).




