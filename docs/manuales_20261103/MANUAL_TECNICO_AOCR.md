# Alcance evidencia y versión de corte

Este manual permite a un técnico identificar la arquitectura real, preparar un entorno, mantener el servicio, diagnosticar fallos y relacionar cada procedimiento con sus acciones, servicios, persistencia y pruebas. Debe utilizarse junto con los catálogos CSV, el esquema de referencia y la matriz de trazabilidad. No se considera que una función esté terminada por tener un nombre de método o una captura.

La rama confirmada es **fix/cierre-flujo-aocr** y el commit es **6d885a984bb875507366a824a290681450376d87**. El 03/11/2026 se ejecutó fetch del remoto y se comprobó que HEAD y origin de esa rama no tenían diferencia de commits. El árbol estaba limpio antes de crear la documentación. No se cambió la lógica de aplicación, no se publicó al servidor y no se enviaron correos ni se ejecutaron decisiones de expedientes reales.

El esquema PostgreSQL se obtuvo por metadatos de solo lectura y por pg_dump de esquema sin datos de la base configurada, cuyo uso confirmó el usuario. Su captura es del 03/11/2026; no equivale a afirmar que el esquema fue el mismo el día del commit. Se documentan por separado las migraciones del repositorio y las diferencias observadas. Los valores de contraseñas, certificados privados, cadenas de conexión y registros personales no se incorporan al paquete.

## Qué se revisó

Se analizaron con Roslyn 645 archivos incluidos por Compile en los siete proyectos de AOCR.sln. El inventario encontró 1.109 tipos, 6.349 métodos, 508 acciones candidatas MVC y 782 métodos de prueba. Se inventariaron 212 vistas Razor. El esquema consultado contiene 263 tablas y 9 vistas, 3.628 columnas y 638 restricciones. Los .csproj históricos fuera de la solución se clasifican aparte; no forman nuevas capas activas.

Los catálogos indican si una relación tabla-método o prueba-método deriva de coincidencia textual. Ese dato sirve para navegar al código, no para acreditar cobertura, autorización efectiva o un grafo de llamadas semántico completo. Los procedimientos troncales se contrastaron además con vistas, servicios y validaciones. Las pruebas ejecutadas figuran en una evidencia independiente de los casos solo inventariados.

## Contradicciones que afectan la operación

| Situación observada | Consecuencia técnica |
| --- | --- |
| Aplicación en .NET Framework 4.6.2; pruebas en 4.8; paquetes con otros destinos | No reutilizar la descripción antigua de que toda la solución es 4.8. Revisar la advertencia iText y compatibilidad antes de instalar. |
| Tipo 3 en lógica y CHECK 0,1,2 en la base consultada | La modificación no puede declararse validada de extremo a extremo. Corregir el esquema mediante cambio aprobado y pruebas. |
| Dos circuitos y formatos de CL | Una vista previa heredada no representa el PDF oficial del paquete ni demuestra revisión del Coordinador. |
| Una propiedad Codigo por estación | R/NR/RC/ET/A se almacena como selección única; la selección múltiple requerida está pendiente. |
| Bandeja documental excluye fases cerradas | No confundir filtro operativo con eliminación; el acceso de auditoría del Inspector requiere completar navegación y validación. |
| Solo una captura disponible frente a 42 anunciadas | No afirmar cobertura visual de siete roles ni presentar capturas ficticias. |
| Conexiones AOCRConnection y PostgreSQL con resolución diferente | Verificar que persistencia de dominio y trabajador de correo operen en el entorno previsto. Una variable de entorno no reemplaza todas las lecturas directas. |
| Excepción de activación de una cuenta por correo específico en UsuarioBL | Requiere revisión de seguridad institucional. La dirección no se reproduce ni se promueve como mecanismo de soporte. |
| Atributos Route sin MapMvcAttributeRoutes en RouteConfig | Catalogar la declaración sin garantizar que esa URL de atributo esté registrada. Las rutas convencionales y aliases explícitos requieren lectura separada. |

La lista completa y su tratamiento preceden a los manuales en LEVANTAMIENTO_Y_CONTRADICCIONES.md. Estas observaciones son parte de la definición de alcance, no una autorización para modificar producción durante la documentación.

# Arquitectura y proyectos

## Capas de la solución

La solución utiliza ASP.NET MVC5 sobre .NET Framework y organiza presentación, negocio, datos y modelos en proyectos separados. Los controladores reciben solicitudes web y construyen vistas o respuestas; los servicios coordinan validaciones y transiciones; los DAO ejecutan consultas y escrituras; CapaModelo contiene entidades, solicitudes y contratos compartidos. La separación existe, aunque algunos controladores construyen DAO y servicios directamente, por lo que la inyección de dependencias no cubre todos los caminos.

| Proyecto activo | Destino y responsabilidad |
| --- | --- |
| AOCR/AOCR.csproj | v4.6.2; Proyecto adicional incluido en la solución; no sustituye el sitio MVC |
| AOCR.Tests/AOCR.Tests.csproj | v4.8; MSTest y pruebas de regresión |
| CapaDatos/CapaDatos.csproj | v4.6.2; DAO e integración |
| CapaModelo/CapaModelo.csproj | v4.6.2; Entidades y contratos |
| CapaNegocio/CapaNegocio.csproj | v4.6.2; Servicios y reglas |
| CapaPresentacion/CapaPresentacion.csproj | v4.6.2; Aplicación web MVC y Razor |
| ClassLibrary1/CapaUtilidades.csproj | v4.6.2; Utilidad compartida referenciada por las capas |

El sitio publicable es CapaPresentacion. El proyecto AOCR adicional se conserva en la solución y debe inventariarse, pero no debe elegirse como sitio IIS por semejanza del nombre. ClassLibrary1 contiene la utilidad compartida; otros proyectos de utilidades encontrados fuera de AOCR.sln no se consideran dependencias activas sin referencia.

## Dependencias y versiones

packages.config del sitio declara Microsoft.AspNet.Mvc 5.2.9, Microsoft.AspNet.Razor 3.2.9, Npgsql y otras dependencias de acceso, Rotativa para renderización y bibliotecas iText. Deben restaurarse las versiones del repositorio, sin actualizar paquetes como parte de publicar estos manuales. El inventario de paquetes adjunto identifica las versiones por proyecto; su presencia no acredita que todas las bibliotecas estén en la ruta principal de ejecución.

El proyecto declara 4.6.2 pero diversos paquetes conservan destinos net48 o net472 en su inventario. La compilación de vistas produce advertencia de dependencias de itext.commons para un framework posterior. Esa advertencia exige validación de carga y firma en el host real; un build sin errores no demuestra que la combinación sea compatible en cualquier servidor. La biblioteca de contraseñas efectiva revisada no se deduce de que BCrypt aparezca entre los paquetes: UsuarioBL utiliza PasswordHelper con PBKDF2 y compatibilidad SHA256.

## JavaScript CSS Bootstrap y AdminLTE

El sitio combina Razor, estilos propios y recursos de terceros de su árbol Content y Scripts. Hay formularios tradicionales y solicitudes AJAX con FormData, tokens antifalsificación y respuestas JSON. Las clases y recursos de Bootstrap o la apariencia AdminLTE no deben utilizarse para inferir una única versión global de interfaz; el catálogo 13 conserva cada archivo y huella. Los recursos minificados y heredados se distinguen de los scripts propios para no editar una dependencia cuando el comportamiento está en la aplicación.

firma-aocr.js gestiona guardado, generación, firma y remisión final. La pantalla y el servicio deben confirmar el mismo estado; el mensaje provisional de carga no acredita que la transacción haya terminado. El versionado de URL del script es importante para no diagnosticar como código actual una copia en caché del navegador.

## Inicio Unity y ciclo de vida

UnityConfig.RegisterComponents registra servicios de configuración, acceso a contexto, repositorios y servicios del flujo final con sus contratos. Se observan registros para IAocrFinalWorkflowRepository/Service e IEntregaFinalRepository/Service, además de dependencias del acceso AS400. Los registros deben revisarse junto con los constructores: un new dentro del controlador o servicio no queda sustituido por el contenedor por el solo hecho de registrar el tipo.

Global.asax inicia el aplicativo, registra componentes y filtros e inicia el procesador de correos sin bloquear toda la respuesta de arranque. Application_End dispone el procesador. Por estar dentro del proceso web, un reciclaje del pool o una aplicación que no arranca afecta el consumo de la cola. No se localizó en el repositorio una acreditación de una tarea externa siempre activa que sustituya ese ciclo de vida.

Fuentes: [CapaPresentacion/App_Start/UnityConfig.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/App_Start/UnityConfig.cs), [CapaPresentacion/Global.asax.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Global.asax.cs), [CapaPresentacion/CapaPresentacion.csproj](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/CapaPresentacion.csproj).

# Rutas contratos y autorización

## Registro de rutas

RouteConfig registra rutas convencionales y aliases explícitos antes de la ruta por defecto controller/action/id. Entre los aliases están el detalle amigable de inspección y rutas históricas de LV e informes. La acción real puede diferir de la palabra que aparece en una URL antigua; para diagnóstico deben registrarse tanto la ruta solicitada como controller/action resueltos.

No se encontró MapMvcAttributeRoutes en ese archivo de registro. Un atributo Route localizado en un método se conserva en el catálogo como declaración, sin considerarlo una garantía de que esa ruta se habilite. Tampoco debe interpretarse que una acción sin HttpGet es necesariamente GET: MVC admite los verbos no restringidos por atributo. Los catálogos separan el verbo declarado de la ruta convencional candidata.

## Filtros y permisos

FilterConfig registra GlobalExceptionFilter, GlobalSecurityFilter, RestoreAuthenticatedSessionAttribute y AuditActionFilter. Los atributos AocrAuthorize de SecurityFilters y RequirePermission añaden comprobaciones de contexto y permisos. Existen controles adicionales en el controlador y servicio: rol activo, compañía, identidad numérica y login, asignación del Inspector, estado del expediente y versión de concurrencia.

El menú se construye con SidebarMenuBuilder y no es una frontera de seguridad suficiente. Algunas vistas o controladores admiten Administrador para consulta o compatibilidad, pero el servicio de la decisión puede rechazarlo. Los manuales no atribuyen al Administrador la firma o aceptación de otro actor. La comprobación institucional de todos los caminos heredados con autorización amplia sigue siendo necesaria.

## Roles canónicos y permisos finales

| Rol | Responsabilidad protegida |
| --- | --- |
| RT | Solicitud, documentos, subsanación y descarga de su compañía. |
| Financiero | Validación económica y operación financiera autorizada. |
| Coordinador | Revisión y remisión documental y de paquete; no sustituye aceptación DIRCAV. |
| DIRCAV | Aceptación, designación, CL y remisión formal del AOCR. |
| Inspector | Trabajo técnico asignado, LV e informes por estación y preparación de documentos. |
| DIRDAC | Revisión y firma del AOCR remitido. |
| Administrador | Cuentas, roles, permisos, catálogos, parámetros y soporte autorizado. |

AocrRolesInstitucionales mantiene alias históricos separados. DCAV corresponde a DIRCAV, mientras DireccionJefaturaTecnica puede normalizarse a DIRDAC en el catálogo de esa autoridad. No normalice ambas autoridades a un rol genérico Dirección. AocrFinalWorkflowService declara permisos como DIRCAV_REMITIR_DIRDAC, DIRDAC_VER_BANDEJA, DIRDAC_DEVOLVER_DIRCAV y DIRDAC_FIRMAR_AOCR. Su asignación en la base se entrega aparte de los atributos declarados en fuente.

## Entradas y salidas de acciones

El catálogo 02 contiene por acción clase, archivo, línea, parámetros, tipo de retorno, atributos de clase y acción, verbos declarados, ruta convencional candidata, rutas de atributo y excepciones controladas. No inventa una API REST donde existen acciones MVC. Los modelos del catálogo 05 permiten reconstruir los parámetros anidados, archivos, fechas y versiones que consume cada pantalla. Los parámetros enviados por el cliente no son confiables aunque provengan de campos ocultos.

Las respuestas pueden ser vistas, archivos, redirecciones o JSON; el tipo de retorno declarado no describe por sí solo todas las ramas. Para los procedimientos críticos se debe leer el resultado y el código HTTP: 400 para validación, 401/403 para autenticación o autorización, 409 para conflicto de estado o versión y 500 para fallo no controlado o transaccional cuando se utilicen en la acción concreta. No se establece una tabla universal de códigos para todas las acciones heredadas.

Fuentes: [CapaPresentacion/App_Start/RouteConfig.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/App_Start/RouteConfig.cs), [CapaPresentacion/App_Start/FilterConfig.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/App_Start/FilterConfig.cs), [CapaPresentacion/Filters/SecurityFilters.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Filters/SecurityFilters.cs), [CapaPresentacion/Filters/RequirePermissionAttribute.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Filters/RequirePermissionAttribute.cs), [CapaDatos/Constants/AocrRolesInstitucionales.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaDatos/Constants/AocrRolesInstitucionales.cs).

# PostgreSQL y diccionario de datos

## Esquema observado y forma de acceso

La base de referencia responde como PostgreSQL 16.10. El repositorio utiliza Npgsql y Dapper en los DAO, además de rutas de compatibilidad y paquetes de ORM que no sustituyen el análisis del acceso efectivo. Algunos DAO consultan information_schema o to_jsonb para soportar columnas opcionales o nombres heredados. Esa tolerancia no reemplaza una migración completa ni evita incompatibilidades de restricciones.

Los archivos esquema_postgresql.json, 08_diccionario_datos.csv, 09_relaciones_restricciones.csv y 10_indices.csv contienen nombres reales. El diccionario conserva esquema, tabla, orden y nombre de columna, tipo, UDT, nulabilidad, longitud y valor por defecto de metadatos, omitiendo cualquier valor de apariencia sensible. Los comentarios disponibles de PostgreSQL se incluyen en un anexo independiente; cuando no existe comentario no se inventa una definición de negocio.

## Núcleo transaccional

| Entidad persistente | Responsabilidad contrastada |
| --- | --- |
| aocr_tbsolicitud | Identidad del trámite, tipo, compañía y usuario solicitante; numero_aocr y datos_documentales_certificado para reconocimiento y datos independientes. |
| aocr_tbsolicitud_estacion | Estaciones y fechas; vínculo con inspección y actividad de la estación. |
| aocr_tbdocumento | Documentos cargados y decisiones documentales. |
| aocr_revision_documental_coordinador | Revisión y aceptación del cierre documental y habilitación de LV/informe según el flujo. |
| aocr_tbinspeccion | Trabajo técnico, asignación y relación con solicitud. |
| aocr_tbinforme_inspeccion | Informes versionados, con estacion_id, finalización, firmas y rutas de evidencia. |
| aocr_tbnoconformidad | No conformidades y relación con ciclos y nueva inspección. |
| aocr_tbcertificado | Certificado por solicitud y datos de generación o firma en la ruta correspondiente. |
| aocr_tbdocumento_generado | Versiones de documentos finales; vigente, bloqueado, hash, ruta y concurrencia. |
| aocr_tbfirma_documento | Evidencia institucional de firma por documento. |
| aocr_proceso_estado | Estado de proceso con versión y fila activa; no es equivalente a cada estado de informe o correo. |
| aocr_evento_workflow | Eventos y claves de idempotencia de operaciones del flujo. |
| aocr_tbnotificacion | Avisos internos dirigidos a usuarios y asociados al expediente. |
| email_queue y email_attachment | Comunicación persistente y adjuntos para procesamiento posterior. |
| seguridad_permiso y seguridad_rol_permiso | Permisos granulares y asignación por rol; separar de usuario_rol. |

Para todas las tablas no incluidas en esta tabla de orientación se utiliza el diccionario completo; una tabla de espejo AS400 no debe confundirse con la fuente transaccional de solicitud. Las relaciones físicas están en el catálogo de restricciones. Una asociación ejecutada por JOIN en un DAO no se declara como FK física si el esquema no la contiene.

## Restricciones y valores de estado

La base mantiene chk_tipo_solicitud con valores 0,1,2, mientras el servicio de cierre contempla 1 emisión, 2 renovación y 3 modificación. Es una discrepancia observada, no una interpretación del manual. El catálogo de estados conserva constantes y alias; algunas cadenas aparecen solo por compatibilidad. Las columnas estado_documental, estado_informe, estado_actual y status de correo representan dominios distintos y no deben actualizarse todas al mismo valor para hacer visible un expediente.

Las claves e índices de documento vigente protegen la unicidad del registro actual. No deben eliminarse para resolver un error de regeneración. AocrDocumentoGeneradoDAO retira la versión anterior y crea la nueva en comandos secuenciales de la misma transacción, porque un CTE de modificación e inserción puede fallar la comprobación del índice antes de retirar la fila previa. El historial de archivos y metadatos conserva el documento anterior.

## Migraciones y esquema de referencia

El esquema exportado es estructura sin filas y sin propietarios o grants del servidor. No contiene las cuentas iniciales, permisos semilla, catálogos de negocio ni archivos binarios. Una instalación útil necesita respaldos o semillas institucionales aprobadas y la configuración por entorno; no hay aquí una contraseña de bootstrap ni se inventa un usuario de administración.

El catálogo de scripts identifica nombre y hash, sin afirmar que todos deban ejecutarse por orden de fecha. Hay scripts de corrección o reinicio y respaldos históricos que no son migraciones acumulables. Antes de aplicar un script compruebe dependencia, esquema actual, restricciones y efecto sobre firmados. Las migraciones del 27/10 y 28/10 corresponden a datos documentales y numeración por trámite original; su existencia en Git no acredita su aplicación en IIS o en una base distinta.

Fuentes: [CapaDatos/DAOs/CertificadoDAO.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaDatos/DAOs/CertificadoDAO.cs), [CapaDatos/DAOs/AocrDocumentoGeneradoDAO.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaDatos/DAOs/AocrDocumentoGeneradoDAO.cs), [scripts/sql/20261027_datos_documentales_certificado.sql](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/scripts/sql/20261027_datos_documentales_certificado.sql), [scripts/sql/20261028_numero_aocr_consecutivo_original.sql](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/scripts/sql/20261028_numero_aocr_consecutivo_original.sql).

# Integración económica DB2 y AS400

## Conectividad ODBC y configuración

AS400BaseDAO utiliza System.Data.Odbc y credenciales resueltas por ISecureConfigurationService. Puede construir conexión por DSN o por propiedades de servidor y biblioteca, y mantiene alternativa de conexión, timeout y circuito de fallo. El código define timeout de comando por defecto de 30 segundos y CircuitBreakerRegistry para AS400. El driver IBM i Access, su arquitectura y el DSN deben coincidir con el proceso IIS; no se identifica un driver solo por que exista una clave en Web.config.

Las claves AOCR_AS400_SERVER, AOCR_AS400_DATABASE, AOCR_AS400_USERID, AOCR_AS400_PASSWORD y AOCR_AS400_LIBRARY se documentan por nombre. No se incluyen sus valores. Debe obtenerse una cuenta de servicio con permisos adecuados y verificar la conectividad en el entorno autorizado. Este levantamiento no ejecutó escrituras DB2 ni acreditó facturación real.

## FR3 y espejo de datos

ControlFR3DAO, los servicios de facturación AS400 y Fr3OutboxWorkerService participan en la integración financiera. El pago aprobado localmente y el envío FR3 son hechos diferentes, con posibilidades de reintento e idempotencia. El circuito de corrección financiera tiene validaciones propias y no debe sortearse volviendo a ejecutar la aprobación ordinaria. La regla de numeración AOCR por consecutivo original no toca el contador FR3.

As400MirrorSyncService, As400OdbcSourceReader, PostgresSyncStateStore y MirrorReadService implementan lectura, sincronización y estado de lotes. Un espejo PostgreSQL puede permitir lecturas o diagnóstico aun cuando no se realiza una escritura directa en DB2; no debe confundirse disponibilidad del espejo con éxito de una operación externa. SyncAdminController expone acciones de administración de sincronización y un trabajador FR3 manual según permisos.

## Diagnóstico de integración

1. Identifique la orden y el evento financiero que falló. Compruebe si el pago fue aprobado o si la transacción local fue revertida.
2. Revise el resultado de integración y el número de intento antes de volver a enviar.
3. Verifique en el host la disponibilidad del driver y la configuración efectiva del proceso de aplicación, sin escribir valores secretos en logs.
4. Consulte el health check AS400 autorizado y los registros del circuito de fallo; un resultado no saludable no justifica saltar la validación del pago.
5. Reintente únicamente mediante el mecanismo previsto para esa orden o lote, conservando claves de idempotencia.
6. Compruebe la evidencia externa por el procedimiento institucional. No dé por emitida una factura en DB2 a partir de un mensaje local de aprobación.

Los parámetros de bibliotecas, tablas externas autorizadas, ventanas de sincronización, cuenta de servicio y prueba de recuperación se mantienen pendientes de confirmación de infraestructura. Los archivos del catálogo de interfaces y DAO permiten localizar la implementación por nombre sin publicar sus credenciales.

Fuentes: [CapaDatos/Infrastructure/AS400BaseDAO.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaDatos/Infrastructure/AS400BaseDAO.cs), [CapaDatos/DAOs/ControlFR3DAO.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaDatos/DAOs/ControlFR3DAO.cs), [CapaNegocio/Services/Fr3OutboxWorkerService.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaNegocio/Services/Fr3OutboxWorkerService.cs), [CapaNegocio/Integraciones/As400Sync/As400MirrorSyncService.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaNegocio/Integraciones/As400Sync/As400MirrorSyncService.cs).

# Documentación inspección y documentos institucionales

## Expediente y subsanación

La revisión se ejecuta por documento y por etapa, con vínculos a solicitud y actor. El modo de lectura de Documento/Lista no equivale al modo de revisión. Las acciones de aceptar, devolver y reabrir requieren los controles de rol y expediente correspondientes. La subsanación conserva relación con la observación; la nueva carga no implica aceptación automática.

RevisionDocumentalBandejaService excluye entradas cuya documentación está cerrada. Por ello una bandeja vacía no demuestra que los archivos se hayan eliminado. El acceso directo del Inspector al archivo para auditoría debe validarse con asignación y fase cerrada; el requisito de navegación está pendiente en los escenarios reportados. No se recomienda resolverlo ampliando acceso a toda la carpeta de archivos del servidor.

## Aceptación y designación

DIRCAV acepta formalmente y designa al Inspector. El cierre documental y la habilitación de LV e informe están sujetos a validaciones de pago, observaciones y asignación. El Coordinador revisa y remite, y puede coordinar una asignación preliminar de revisión; eso no sustituye la designación formal ni la firma del oficio por DIRCAV.

DesignacionDocumentoService y DircavDesignacionService gestionan el oficio y sus reglas. El certificado p12/pfx y la contraseña se reciben para la operación criptográfica y no deben guardarse en la documentación, datos semilla o logs. Un archivo firmado que no pudo almacenarse correctamente no se acredita como oficio disponible al Inspector o RT.

## LV e informe por estación

El identificador de inspección por sí solo no identifica toda la evidencia: se conserva la estación de la LV e informe. En informes existe estacion_id y un índice por inspección, estación y versión. Los documentos históricos sin estación no se reasignan silenciosamente a todas las estaciones. Los controles deben impedir que un formulario de Quito guarde sobre la LV de Guayaquil.

AocrFlujoValidacionService comprueba finalización, firma del Inspector, ruta de evidencia, resultado SATISFACTORIO y ausencia de devolución, recorriendo las estaciones activas de las inspecciones. La preparación de documentos finales exige el conjunto, no una LV o informe aislados. Los datos editables del numeral 4 y las aeronaves realmente inspeccionadas deben provenir del Inspector; no se completa toda la flota por presunción.

Las no conformidades y nuevas inspecciones mantienen vínculos con solicitud, inspección o ciclo raíz. Una nueva inspección puede exigir otra orden y otra evidencia técnica. No se sobrescribe el informe firmado anterior para presentar un resultado satisfactorio nuevo. Los eventos del circuito de NC tienen su catálogo y no se confunden con el estado final de los documentos AOCR.

## Captura documental y numeración

DatosDocumentalesAocr separa permiso CNAC, sus dos fechas, vigencia AOC, país de bandera y los contactos de explotador, gerente de Operaciones, RT y representante legal Ecuador. Esos datos se guardan en JSONB de la solicitud. La validación comprueba formatos, longitudes, fechas, pertenencia de estación y códigos de operación permitidos. La persistencia de JSON no convierte automáticamente en vigente un PDF anterior.

CertificadoDAO toma el consecutivo del número de trámite original, no el id interno ni un contador nuevo de reconocimiento. Para AOCR003 de QTR produce DGAC-129-QTR-003; renovación y modificación copian exactamente el número de su emisión original. La secuencia creada en una migración anterior se conserva por compatibilidad y no es la fuente de esta regla. El disparador de inmutabilidad protege números ya asignados.

La regularización del 28/10 se limita a borradores y protege todo el grupo si una emisión o sus renovaciones/modificaciones tienen evidencia enviada o firmada. Archiva los PDF previos para regenerar; no cambia los bytes ni los números impresos de archivos históricos. Se exige revisión ante colisiones o referencias ambiguas. El script no debe utilizarse como autorización genérica para renumerar firmados.

## Generación PDF y coexistencia de formatos

La ruta institucional FirmaAocrPdfService utiliza Rotativa y wkhtmltopdf sobre AocrReconocimientoPdf.cshtml y AocrCondicionesLimitacionesPdf.cshtml. La firma conserva sus anclas y el archivo exacto que se revisó. Otras rutas utilizan iTextSharp, incluyendo el circuito heredado de Condiciones; no deben describirse como si todas renderizaran la misma plantilla.

CondicionesLimitacionesService y Documento/DescargarCondicionesLimitaciones pueden mostrar una vista previa heredada diferente. En el corte se corrigió su origen de número para consultar el reconocimiento persistido, pero no se acredita por ello la unificación de todo su formato ni la eliminación de todas las matrículas en ese circuito. La firma no debe prometer cambiar después el formato de un PDF que la autoridad revisó como otro documento.

Las plantillas institucionales actuales muestran modelos sin matrículas en la tabla final de CL y una marca de tipo de trámite. La solicitud puede conservar matrículas para su expediente: ese dato no debe borrarse de la solicitud solo para ocultarlo en un PDF. Las operaciones de estación usan una única propiedad Codigo; permitir múltiples operaciones simultáneas es un cambio pendiente de modelo, validación, interfaz y plantilla.

Fuentes: [CapaNegocio/Services/AocrFlujoValidacionService.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaNegocio/Services/AocrFlujoValidacionService.cs), [CapaNegocio/Services/DesignacionDocumentoService.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaNegocio/Services/DesignacionDocumentoService.cs), [CapaPresentacion/Services/FirmaAocrServices.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Services/FirmaAocrServices.cs), [CapaNegocio/Services/CondicionesLimitacionesService.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaNegocio/Services/CondicionesLimitacionesService.cs).

# Flujo final transacciones idempotencia y firmas

## Envío del Inspector al Coordinador

DocumentosFinalesWorkflowService obtiene el plan por tipo de solicitud y verifica los PDF antes de remitir. La evidencia incluye id, inspección, versión, tipo, ruta, SHA256 y tamaño. El servicio no debe aceptar una extensión .pdf como prueba suficiente: comprueba existencia, cabecera PDF, tamaño y hash frente a lo persistido.

DocumentosFinalesWorkflowDAO.FinalizarYEncolar abre la transacción y bloquea el expediente con una llave consultiva y las filas relevantes. Valida Inspector, informes por estación, estado permitido, versiones y ausencia de observaciones o NC pendientes. Bloquea los documentos, cambia proceso y solicitud, registra el evento y encola las ramas de AOCR y CL al Coordinador, además del avance al RT. Solo confirma éxito después del commit.

El envío repetido ya confirmado devuelve un resultado idempotente sin duplicar avisos. Un error al registrar el segundo documento o su correo debe revertir el paquete completo; no se admite que el primer documento quede remitido y el segundo no. Esto es distinto de un error posterior de SMTP, que se trata mediante la cola sin revertir una decisión institucional ya confirmada.

## Revisión del Coordinador y segregación de firmas

El Coordinador revisa el paquete en FirmaAocr/PaquetesCoordinador y ejecuta RevisarPaquete con versión esperada y observación. Si devuelve, desbloquea las versiones según el estado de devolución para una nueva preparación; no modifica LV o informes firmados. Si remite, habilita la etapa DIRCAV prevista. Una bandeja de asignaciones de Inspector no es fuente de paquetes pendientes de revisión final.

DIRCAV firma CL y remite formalmente el AOCR. AocrFinalWorkflowService valida el actor y el permiso granular de cada operación. DIRDAC revisa y firma el AOCR remitido, o devuelve a DIRCAV con motivo. Los contratos y estados de firma son diferentes por tipo documental; no existe un actor Director genérico que sustituya ambos roles.

## Concurrencia y versiones

BaseDAO utiliza transacciones ReadCommitted por defecto, y el flujo crítico agrega bloqueos de solicitud y filas. No debe describirse todo el sistema como serializable. Las versiones esperadas se comparan antes de decidir, y los contadores de versión cambian cuando se registra una transición. Un 409 debe conducir a recargar y revisar, no a enviar la misma versión hasta que alguna llamada pase.

La generación conserva version_documento, vigente, completo, bloqueado, hash_pdf y version_concurrencia. La misma evidencia no crea otra versión. Las escrituras rechazan documentos enviados o firmados. La unicidad del vigente se preserva con índices; las pruebas y diagnósticos deben utilizar esas restricciones reales, no una tabla temporal sin índices equivalentes.

## Firma y entrega final

La operación criptográfica exige certificado y contraseña válidos, actor, estado, archivo e integridad. La persistencia de firma y el flujo final verifican la relación con el documento vigente. El hash por sí solo no atribuye autoridad ni una firma válida; la validación de firmante y certificado son condiciones adicionales.

La evaluación de firmas completas y el cierre institucional no equivalen a que el RT haya recibido físicamente un correo. EntregaFinalService y EntregaFinalDAO gestionan solicitudes de entrega, documentos autorizados, evidencia, cola y seguimiento. Las jefaturas aeroportuarias requieren destinatarios activos configurados; no se inventan direcciones ni se utiliza una cuenta personal como sustituto de un catálogo faltante.

Fuentes: [CapaNegocio/Services/DocumentosFinalesWorkflowService.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaNegocio/Services/DocumentosFinalesWorkflowService.cs), [CapaDatos/DAOs/DocumentosFinalesWorkflowDAO.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaDatos/DAOs/DocumentosFinalesWorkflowDAO.cs), [CapaNegocio/Services/AocrFinalWorkflowService.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaNegocio/Services/AocrFinalWorkflowService.cs), [CapaNegocio/Services/EntregaFinalService.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaNegocio/Services/EntregaFinalService.cs).

# Comunicaciones almacenamiento y seguridad

## Cola persistente y procesamiento SMTP

EmailQueueService utiliza las tablas email_queue y email_attachment. Se registran destinatario, asunto, cuerpo, estado, referencias a solicitud u orden, event_key, tipo_notificacion y correlation_id. Los adjuntos contienen metadatos y ruta; no se debe considerar disponible un adjunto solo porque existe una fila si el archivo falta, está vacío o no coincide con su integridad registrada.

EmailQueueProcessor se inicia en Global.asax mediante Task.Run, se cancela al finalizar la aplicación y registra reintentos con esperas configuradas en código. Existe un máximo por defecto y demoras de 5, 15 y 30 minutos en el procesador revisado; los límites concretos deben comprobarse en esa ruta, porque un campo de un DTO no prueba que la tabla o el trabajador lo utilicen. Los adjuntos inválidos pueden llevar el correo a ERROR antes de intentar transporte.

El consumo usa control de concurrencia y recuperación de mensajes abandonados en ENVIANDO. Para soporte se deben distinguir PENDIENTE, ENVIANDO, ENVIADO, ERROR y los estados que la implementación concreta admita. Una marca de envío del transporte no demuestra lectura del buzón. La entrega externa de los casos reportados no se probó durante esta revisión.

## Prioridades de configuración

SecureConfigurationService permite sobreescritura por variables de entorno para sus llamadas. Para conexiones construye AOCR_CONNSTR_ más el nombre en mayúsculas. Sin embargo, CertificadoDAO lee AOCRConnection directamente desde ConfigurationManager, mientras EmailQueueService resuelve PostgreSQL por el servicio seguro. Por tanto, defina y contraste ambos alias: no asuma que AOCR_CONNSTR_POSTGRESQL cambia también las lecturas directas de AOCRConnection.

El catálogo 17 entrega claves sin valores. Incluye familias de AS400 y correo para servidor, puerto, autenticación, remitente y SSL. La clave RT_MaxFileSizeMb y las raíces RT_BasePathStorage y RT_FileStorageRoot se resuelven por las rutas de FileStorageHelper; las reglas de una pantalla concreta pueden imponer un límite menor al global. Nunca copie una contraseña o cadena de conexión de producción a una captura, manual o comando que quede en el historial.

## Archivos y descargas

FileStorageHelper y los servicios de documentos resuelven rutas de almacenamiento y raíces permitidas. El caso operativo tiene requisitos de carpeta compartida; la identidad del pool IIS debe tener los permisos de lectura y escritura necesarios en la raíz configurada. No basta conceder acceso al usuario interactivo que publica la aplicación.

DocumentoSeguroService y las descargas del flujo comprueban documento, solicitud, ruta y raíces antes de servir el archivo. Deben mantenerse validaciones de pertenencia, estado, compañía y tipo documental. Las cabeceras de contenido y el modo inline o descarga no reemplazan el control de acceso. Los PDF oficiales no deben alojarse como un recurso público que el RT pueda adivinar por nombre.

Las rutas heredadas almacenadas como App_Data/Uploads pueden resolverse a la raíz física configurada; no asuma que son siempre carpetas locales de C:/inetpub. Cuando falle el guardado, capture ruta lógica, raíz resuelta y mensaje sin exponer credenciales. No borre versiones firmadas para reparar un permiso de carpeta.

## Autenticación sesiones y secretos

El sitio configura Forms Authentication y utiliza estado de sesión y contexto de usuario. RestoreAuthenticatedSessionAttribute rehidrata el contexto cuando existe cookie válida y fuerza la compañía del RT cuando procede. La identidad autenticada, el rol activo y la compañía son controles relacionados pero distintos; una cookie no concede acceso a expedientes de otra compañía.

UsuarioBL.Autenticar verifica la contraseña mediante PasswordHelper y puede migrar hashes legacy. PasswordHelper implementa PBKDF2-SHA256 con salt de 16 bytes, clave de 32 bytes y 100.000 iteraciones en ese corte, conservando compatibilidad SHA256. No se afirma que todos los registros históricos ya estén migrados. Se localizó además una excepción de activación por dirección específica antes de verificar contraseña; debe someterse a revisión institucional de seguridad y no reproducirse como regla de operación.

Los formularios críticos utilizan tokens antifalsificación y diversas variantes de validación para AJAX. Antes de añadir un endpoint se debe mantener la validación del token y la pertenencia al contexto, no copiar solo el atributo de autorización. El parámetro solicitudId, estacionId, documentoId o versión recibida debe contrastarse con la entidad real antes de escribir o descargar.

Fuentes: [CapaDatos/Services/EmailQueueService.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaDatos/Services/EmailQueueService.cs), [CapaDatos/Services/SecureConfigurationService.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaDatos/Services/SecureConfigurationService.cs), [CapaNegocio/Helpers/FileStorageHelper.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaNegocio/Helpers/FileStorageHelper.cs), [CapaNegocio/Helpers/PasswordHelper.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaNegocio/Helpers/PasswordHelper.cs), [CapaNegocio/UsuarioBL.cs](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaNegocio/UsuarioBL.cs).

# Operación logs errores y caché

## Evidencia y correlación

Los filtros y servicios registran información de acción, usuario y correlación. Se utilizan logs de archivo y persistencia de negocio como aocr_tblog, además de eventos del flujo y notificaciones. Para un incidente identifique solicitud, inspección, estación, documento, versión, hora y CID o referencia, y correlacione esas fuentes sin suponer que todas escriben en el mismo archivo.

En el caso del paquete final se añadieron referencias ENVIO-COORD y registros de aceptación o rechazo de la operación. Un mensaje genérico de transacción revertida debe conducir al error técnico preservado en los registros, no a editar el estado directamente para que aparezca en una bandeja. Los registros pueden contener información personal o rutas: distribuya extractos mínimos y nunca incluya certificados privados o contraseñas.

## Errores frecuentes y diagnóstico dirigido

| Síntoma | Comprobación y acción autorizada |
| --- | --- |
| HTTP 400 en LV o informe | Contrastar id, estación, campos y versión del formulario. Recargar la estación correcta; no manipular identificadores ocultos. |
| HTTP 403 o No autorizado | Revisar autenticación, rol activo, compañía, permiso granular, asignación y etapa. No usar Administrador para firmar. |
| HTTP 409 | Conflicto de versión o estado; recargar evidencia y volver a decidir si sigue siendo válida. |
| HTTP 500 o transacción revertida | Buscar error correlacionado y confirmar que no cambió el estado ni se encolaron avisos parciales. |
| 23505 en documento vigente | Conservar índices. Revisar publicación de la escritura secuencial transaccional de versiones. |
| PDF anterior después de guardar | Guardar no regenera. Revisar versión vigente, regeneración permitida, ruta servida y caché. |
| MAC inválida del certificado | Verificar p12/pfx y contraseña con el titular. No registrar ni compartir la clave. |
| Acceso denegado al guardar firmado | Comprobar identidad IIS, raíz real y permisos compartidos/NTFS. No cambiar el hash ni borrar un firmado. |
| Correo no recibido | Distinguir transición fallida, falta de encolado, estado pendiente/error de transporte y entrega en buzón. |
| Bandeja Coordinador vacía | Usar PaquetesCoordinador para paquete final y comprobar estado DOCUMENTOS_FINALES_COORDINADOR. |
| Modificación rechazada | Revisar discrepancia del CHECK de tipo antes de cambiar el tipo de solicitud del usuario. |

## Caché y recursos publicados

Las descargas PDF del flujo agregan cabeceras para evitar reutilización indebida de respuestas. Los scripts se sirven con un identificador de versión en la vista. Para diagnosticar una actualización no visible compare el binario y la vista publicados con el corte y el hash del paquete; después compruebe la URL y respuesta de archivo. No atribuya automáticamente todo PDF anterior a caché: puede ser una versión persistida que nunca fue reemplazada por un error de transacción.

El inventario de recursos distingue archivos propios y dependencias candidatas. No existe en este manual una política global de invalidación de caché acreditada para todos los módulos. La configuración y los posibles caches de catálogos deben revisarse en su servicio efectivo antes de reiniciar producción.

## Monitoreo y trabajos

HealthController expone acciones de salud, liveness/readiness y diagnóstico de dependencias según su autorización y ruta real. Use /Health/Live o /Health/Ready por la ruta convencional cuando corresponda, en lugar de inventar una API distinta. El estado saludable del sitio no acredita una firma correcta, una carga documental o un envío DB2.

El procesador de correo es un trabajo dentro del aplicativo. Fr3OutboxWorkerService y acciones de SyncAdmin permiten procesamiento o diagnóstico de integración. La existencia de una clase Worker no acredita un servicio Windows o una tarea programada externa instalada. No se localizaron evidencias de todas las programaciones, ventanas y cuentas externas del servidor; deben confirmarse con infraestructura.

# Instalación publicación respaldo y restauración

## Preparación del entorno

1. Obtenga el repositorio autorizado y haga checkout del commit de corte, no solo del nombre de rama que puede avanzar después.
2. Verifique el hash y un árbol limpio. Conserve el manifiesto de archivos del paquete aprobado para compararlo con lo publicado.
3. Prepare Visual Studio/MSBuild y las herramientas de compilación para el destino .NET Framework 4.6.2. AOCR.Tests requiere 4.8; diferencie esas necesidades de las políticas de soporte del sistema operativo del host.
4. Restaure los paquetes NuGet exactos de la solución. No actualice versiones durante una publicación correctiva sin sus pruebas.
5. Obtenga una base autorizada y una copia de archivos consistente. El esquema sin datos adjunto no crea cuentas, catálogos ni permisos semilla suficientes para operar desde cero.
6. Configure los alias de conexión del entorno y compruebe que no apuntan a producción desde una instancia de pruebas.
7. Prepare el driver IBM i Access y DSN/arquitectura cuando el entorno deba utilizar AS400. Confirme con infraestructura las bibliotecas y permisos externos.
8. Defina las raíces de archivos y permisos de la identidad IIS. Configure SMTP con los valores institucionales en el mecanismo seguro correspondiente.

Comandos de referencia, sin secretos incorporados:

```powershell
git checkout 6d885a984bb875507366a824a290681450376d87
nuget restore AOCR.sln
msbuild CapaPresentacion/CapaPresentacion.csproj /t:Build /p:Configuration=Release /p:MvcBuildViews=true
```

Los nombres y rutas de herramientas dependen del host. La solución contiene un sitio MVC heredado que requiere las herramientas web de Visual Studio; dotnet build no se presenta como sustituto probado de ese proceso. Configure una identidad de servicio propia, no una cuenta interactiva del desarrollador para ejecución permanente.

## Verificación previa a publicar

1. Congele el commit y registre ambiente, versión de base y scripts aprobados. No aplique todos los .sql del repositorio como si fueran una instalación acumulativa.
2. Ejecute la compilación de aplicación y Razor, y registre advertencias y errores. Resuelva incompatibilidades que afecten el host, especialmente en librerías PDF/firma.
3. Ejecute pruebas con una conexión de pruebas aislada. Algunos tests llamados Unit abren Npgsql; no asuma que el nombre del directorio impide escrituras.
4. Restaure el respaldo de referencia en una base desechable y compruebe restricciones, tablas, catálogos y archivos requeridos por los casos de prueba.
5. Verifique una cuenta por rol, compañías y asignaciones de prueba. Pruebe que DIRCAV y DIRDAC no puedan intercambiar firmas ni que Administrador las sustituya.
6. Genere el artefacto publicable desde CapaPresentacion y compare DLL, vistas, Scripts, Content y herramientas de PDF. No utilice una copia de public/public1 sin manifiesto como prueba de que el servidor ya está actualizado.
7. Revise configuraciones transformadas y secretos fuera del paquete público. Valide debug, autenticación, límites de carga, errores y rutas por entorno.
8. Prepare ventana de cambio, respaldo, monitoreo y rollback, con responsables institucionales. La aprobación del manual no equivale a aprobación de despliegue.

## Publicación en IIS

1. Cree o seleccione el sitio y aplicación virtual aprobados. El ejemplo histórico utiliza /aocr, pero la ruta del ambiente debe confirmarse.
2. Configure el pool para CLR v4 e integración compatible con la aplicación, la identidad de servicio y la arquitectura que requiera ODBC.
3. Obtenga respaldo de binarios, configuración y archivos antes de reemplazar. Detenga o aísle el servicio mediante el procedimiento institucional de ventana de cambio.
4. Copie el paquete completo de la misma compilación, incluyendo vistas y scripts; no mezcle DLL nuevas con una vista o script antiguo.
5. Configure conexiones y raíces del ambiente sin incluir contraseñas en comandos o capturas. Mantenga el acceso de la identidad del pool al almacenamiento compartido.
6. Aplique únicamente los scripts aprobados para esa base, con control de errores y respaldo. Una regularización de borradores no autoriza modificar PDFs firmados.
7. Inicie el aplicativo y revise arranque, carga de Unity, filtros, lectura de configuración y cola. Un 503 de readiness requiere revisar la dependencia, no ocultar el resultado.
8. Ejecute smoke tests de acceso, rol, compañía, consulta y archivo. Luego valide el flujo institucional en datos de pruebas, con la aprobación para cualquier correo real o integración externa.
9. Compare el manifiesto publicado y registre resultado, tiempo y responsable. Conserve evidencia de rollback disponible.

No se ejecutó una publicación IIS durante este levantamiento. La configuración concreta de bindings, certificado TLS, cuenta del pool, balanceo o proxy inverso no se deduce de una captura del navegador y figura como información pendiente.

## Respaldo consistente

1. Defina alcance y ventana: base, archivos de documentos, configuración protegida y binarios del mismo corte. El respaldo de la base sin los PDF no basta para restaurar el expediente.
2. Use pg_dump con una cuenta autorizada y autenticación fuera de la línea de comando visible. Mantenga los valores secretos en el mecanismo aprobado del host.
3. Respalde la raíz de archivos preservando estructura, versiones y permisos. Registre tamaño y hashes para verificar la copia.
4. Conserve las configuraciones del entorno en un almacén restringido separado de los manuales o un ZIP distribuible.
5. Registre fecha, versión de servidor/cliente, commit y scripts aplicados. Pruebe la lectura del respaldo antes de la ventana de cambio.

Ejemplos con marcadores que deben sustituirse por el entorno autorizado:

```powershell
pg_dump --host=SERVIDOR --username=CUENTA --format=custom --file=RESPALDO.dump BASE
pg_dump --host=SERVIDOR --username=CUENTA --schema-only --no-owner --no-privileges --file=ESQUEMA.sql BASE
```

No sustituya esos marcadores por una contraseña en el mismo comando. No se establece un período de retención o RPO/RTO institucional que no haya sido proporcionado.

## Restauración y comprobación

1. Cree una base vacía aislada con permisos apropiados y compruebe la versión de herramientas. No restaure sobre producción como prueba.
2. Para un dump completo utilice pg_restore; para un SQL de esquema utilice psql con ON_ERROR_STOP. Conserve la salida y no ignore fallos por objetos o extensiones faltantes.
3. Restaure catálogos y cuentas únicamente desde fuentes autorizadas; el esquema del paquete no contiene datos de personas ni credenciales iniciales.
4. Restaure archivos y permisos con la misma correspondencia de rutas lógicas. Compruebe una muestra autorizada de hashes y documentos firmados sin regenerarlos.
5. Configure el aplicativo de pruebas contra esa base y raíces aisladas. Deshabilite o dirija a un destino de pruebas las integraciones externas según configuración efectiva.
6. Valide autenticación, roles, compañía, consultas y archivos; después ejecute casos de flujo y transacciones sobre registros desechables.
7. Registre las diferencias respecto al entorno origen y no declare una restauración completa por contar tablas únicamente.

```powershell
pg_restore --host=SERVIDOR_PRUEBAS --username=CUENTA --dbname=BASE_VACIA --exit-on-error RESPALDO.dump
psql --host=SERVIDOR_PRUEBAS --username=CUENTA --dbname=BASE_VACIA --set=ON_ERROR_STOP=1 --file=ESQUEMA.sql
```

La validación de estructura de este paquete se ejecuta por separado en PostgreSQL temporal local, no en la base de referencia. Debe identificarse su motor y resultado en el informe de validación; no equivale a validar datos semilla ni compatibilidad con todos los destinos posibles.

## Rollback y mantenimiento

1. Identifique el punto de falla y el alcance de transacciones ya confirmadas. Un correo enviado o una firma externa no se deshacen por volver a copiar una DLL anterior.
2. Aísle el servicio por el procedimiento aprobado y conserve los logs, eventos, archivos nuevos y referencias para auditoría.
3. Restaure el artefacto y configuración anteriores del mismo corte. Verifique su compatibilidad con el esquema que permanece; no asuma que toda migración tiene reversión automática.
4. Si se requiere restaurar base y archivos, decida el punto consistente y el tratamiento de decisiones posteriores con el responsable institucional. No borre evidencia firmada sin un procedimiento formal.
5. Reinicie y ejecute los smoke tests, consulta de documentos y colas. Registre qué quedó pendiente de conciliación externa.
6. Cierre la incidencia con causa, evidencias y cambio preventivo. El mantenimiento de índices, espacio y retención debe respetar la conservación documental y la política que se confirme.

Fuentes de operación: scripts/golive, scripts/db y catálogos de configuración. Las automatizaciones existentes se inventarían, pero su ejecución real y calendario en el servidor no se afirman sin evidencia de infraestructura.

# Contratos técnicos de los procedimientos

Las fichas siguientes vinculan los procedimientos de usuario a declaraciones reales de este corte. Los parámetros y retornos se obtienen de sintaxis, no de una API imaginada. Los catálogos 02 a 05 amplían la información a todas las acciones, servicios, interfaces, DAO y modelos; la tabla o prueba de referencia textual no constituye por sí sola cobertura ejecutada.

## P01 Ingresar y seleccionar el contexto de trabajo

**Actor y situación:** Todos los roles; Implementado.

**Controlador y contrato declarado:** Login(string returnUrl, string af) -> ActionResult; Login(LoginViewModel model, string returnUrl) -> ActionResult; SeleccionarCompania(string companiaSeleccionada, string returnUrl) -> ActionResult; SeleccionarCompania(SeleccionCompaniaViewModel model) -> ActionResult; CambiarCompaniaActiva(string companiaCodigo, string returnUrl) -> ActionResult; CambiarRol(string rolSeleccionado) -> ActionResult; Logout() -> ActionResult.

**Servicios contrastados:** UsuarioContextoService, AocrUserContextService. **Persistencia de referencia:** UsuarioDAO. Las tablas relacionadas con el procedimiento son usuario, usuario_rol, rol; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Usuario habilitado, rol asignado y navegador con acceso al portal. Usuario, contraseña y rol activo. El RT debe seleccionar una compañía autorizada cuando se solicite.

**Resultado funcional que debe acreditarse:** Sesión autenticada y contexto de rol y compañía definido. No se acepta documentación ni se firma un documento por iniciar sesión.

**Tratamiento de fallos y comunicaciones:** Acceso no autorizado: revise el rol activo y la compañía. Si el rol no fue asignado, solicite su revisión al Administrador; no utilice la cuenta de otra autoridad. El acceso no acredita envío de documentos. Los correos de recuperación o credenciales corresponden a acciones específicas.

**Referencia de fuente:** [CapaPresentacion/Controllers/AccountController.cs línea 45](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/AccountController.cs#L45). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P02 Registrar al RT y tramitar su designación

**Actor y situación:** RT y Administrador en su aprobación específica; Implementado con aprobación y datos institucionales requeridos.

**Controlador y contrato declarado:** Registro() -> ActionResult; GuardarRegistro(RegistroRTVM vm) -> ActionResult; Declaracion(int solicitudId) -> ActionResult; AceptarDeclaracion(DeclaracionRTVM vm) -> ActionResult; SubirDesignacion(DesignacionUploadVM vm) -> ActionResult; Enviar(int solicitudId) -> ActionResult; DescargarConstancia() -> ActionResult.

**Servicios contrastados:** SolicitudAocrInfraBL. **Persistencia de referencia:** UsuarioInternoRTDAO. Las tablas relacionadas con el procedimiento son usuario, aocr_tbsolicitud; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** La compañía y la persona deben existir o poder registrarse según el contexto autorizado. Razón social, RUC, teléfono, correo, declaración de responsabilidad y archivo de designación que solicite la pantalla.

**Resultado funcional que debe acreditarse:** Registro y designación disponibles para el control administrativo previsto. La habilitación del trámite depende del estado y permisos resultantes.

**Tratamiento de fallos y comunicaciones:** Archivo faltante o registro bloqueado: complete el documento exigido y vuelva a consultar el estado. Un rechazo requiere corregir la causa indicada, no crear varias cuentas para la misma persona. Las acciones de alta, aprobación y reenvío pueden generar avisos. Que el correo exista no acredita aprobación; compruebe el estado del registro.

**Referencia de fuente:** [CapaPresentacion/Controllers/RTController.cs línea 79](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/RTController.cs#L79). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P03 Crear o continuar una orden de recaudación

**Actor y situación:** RT; Implementado.

**Controlador y contrato declarado:** Obligatoria(string estado) -> ActionResult; Nueva() -> ActionResult; Nueva(OrdenRecaudacionNuevaVM model) -> Task<ActionResult>; Detalles(int id, bool abrirPago) -> Task<ActionResult>; Editar(int id) -> Task<ActionResult>; Editar(OrdenRecaudacionModel model, string accion) -> ActionResult; Generar(int id) -> Task<ActionResult>; Enviar(int id) -> ActionResult.

**Servicios contrastados:** OrdenRecaudacionOrchestrator. **Persistencia de referencia:** OrdenRecaudacionDAO. Las tablas relacionadas con el procedimiento son aocr_or_orden; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Compañía activa seleccionada y cuenta habilitada para iniciar el componente económico. Compañía, conceptos y detalles que ofrece el catálogo, cantidades, tarifas y datos del trámite. Las tarifas no se escriben libremente para eludir el catálogo.

**Resultado funcional que debe acreditarse:** Orden registrada con sus detalles y estado económico. Todavía no constituye un pago aprobado ni un AOCR.

**Tratamiento de fallos y comunicaciones:** Orden pendiente o tarifa no localizada: continúe la orden existente o solicite revisión del catálogo. No cambie el tipo de trámite para obtener una tarifa distinta. El servicio de orquestación administra las notificaciones aplicables a la transición. La confirmación de pantalla y el estado de la orden son la referencia inmediata.

**Referencia de fuente:** [CapaPresentacion/Controllers/OrdenRecaudacionController.cs línea 354](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/OrdenRecaudacionController.cs#L354). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P04 Registrar un comprobante de pago

**Actor y situación:** RT; Implementado.

**Controlador y contrato declarado:** Detalles(int id, bool abrirPago) -> Task<ActionResult>; RegistrarPago(int id) -> ActionResult; RegistrarPago(int id, string Monto, string NumeroFactura, string MetodoPago, string Banco, HttpPostedFileBase ComprobanteArchivo, string Observaciones) -> ActionResult.

**Servicios contrastados:** ComprobanteService. **Persistencia de referencia:** OrdenRecaudacionDAO. Las tablas relacionadas con el procedimiento son aocr_or_orden; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Orden enviada y habilitada para registrar comprobante. Monto, número de factura o referencia que pida el formulario, método de pago, banco, comprobante y observaciones.

**Resultado funcional que debe acreditarse:** Comprobante asociado a la orden y pendiente de la validación que corresponda.

**Tratamiento de fallos y comunicaciones:** Monto, formato o archivo rechazado: corrija el campo señalado. Si cargó un comprobante en otra orden, solicite revisión; no reemplace la evidencia de un pago ya validado. Los avisos de revisión económica no sustituyen la comprobación del estado de pago.

**Referencia de fuente:** [CapaPresentacion/Controllers/OrdenRecaudacionController.cs línea 1465](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/OrdenRecaudacionController.cs#L1465). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P05 Validar el pago y registrar la factura

**Actor y situación:** Financiero; Implementado; DB2 y emisión financiera externa requieren validación de entorno.

**Controlador y contrato declarado:** Dashboard(string estado) -> ActionResult; AprobarPagoConFactura(AprobarPagoConFacturaVM model, HttpPostedFileBase facturaFile) -> ActionResult; RechazarOrden(int id, string motivo) -> ActionResult; ReintentarFr3(int ordenId) -> ActionResult; DetalleOrden(int id) -> ActionResult.

**Servicios contrastados:** ComprobanteService, Fr3OutboxWorkerService. **Persistencia de referencia:** OrdenRecaudacionDAO, ControlFR3DAO. Las tablas relacionadas con el procedimiento son aocr_or_orden, email_queue; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Orden en revisión financiera y comprobante válido. Se requiere permiso de aprobación, no solo visibilidad del menú. Orden, pago, número y autorización de factura, fecha de emisión, subtotal, IVA, total, observaciones y archivo de factura.

**Resultado funcional que debe acreditarse:** Aprobación o rechazo económico registrado. El envío al sistema externo debe comprobarse por separado cuando aplique.

**Tratamiento de fallos y comunicaciones:** Solo se puede registrar factura en estados admitidos; si existe corrección financiera, utilice el circuito de corrección. Las advertencias FR3 se escalan con el número de orden y el registro de integración. Los avisos financieros se registran según la transición. No se documenta el correo como prueba de factura emitida en DB2.

**Referencia de fuente:** [CapaPresentacion/Controllers/FinancieroController.cs línea 31](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/FinancieroController.cs#L31). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P06 Corregir una orden en el circuito financiero

**Actor y situación:** Financiero; Implementado.

**Controlador y contrato declarado:** CorregirOrden(int id) -> ActionResult; GuardarCorreccionOrden(int id, long correccionId, List<CapaDatos.Entidades.DetalleOrden> detalles, string observacion) -> ActionResult; ValidarCorreccionOrden(int id, long correccionId, decimal totalEsperado, decimal montoVerificado, string referencia, HttpPostedFileBase facturaRectificada, HttpPostedFileBase comprobanteRectificado) -> ActionResult.

**Servicios contrastados:** Fr3OutboxWorkerService. **Persistencia de referencia:** OrdenRecaudacionDAO. Las tablas relacionadas con el procedimiento son aocr_or_orden; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Orden elegible para corrección y permiso financiero activo. Id de corrección, detalles corregidos, observación, total esperado, monto verificado, referencia y archivos rectificados cuando se soliciten.

**Resultado funcional que debe acreditarse:** Corrección identificada y validada según sus reglas. No sustituye decisiones técnicas ni altera documentos institucionales firmados.

**Tratamiento de fallos y comunicaciones:** Total inconsistente o versión de corrección desactualizada: recargue y contraste importes. No use la aprobación ordinaria para saltar una corrección pendiente. El envío de avisos depende de la transición concreta; revise la campana y el estado de la corrección.

**Referencia de fuente:** [CapaPresentacion/Controllers/FinancieroController.cs línea 832](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/FinancieroController.cs#L832). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P07 Registrar la solicitud de emisión o renovación

**Actor y situación:** RT; Implementado; requisitos económicos y de compañía condicionan acceso.

**Controlador y contrato declarado:** GuardarFlota(GuardarFlotaRequest request) -> JsonResult; FormularioEmisionAOCR(int? oid, int? tipoSolicitud) -> ActionResult; FormularioCompleto(SolicitudAOCRViewModel vm) -> ActionResult; GuardarProgreso() -> JsonResult; MisSolicitudes() -> ActionResult; Detalle(int id) -> ActionResult.

**Servicios contrastados:** SolicitudAOCRBL. **Persistencia de referencia:** SolicitudAOCRDAO, AeronaveSolicitudDAO. Las tablas relacionadas con el procedimiento son aocr_tbsolicitud, aocr_tbaeronave_solicitud; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Compañía activa, RT habilitado y orden o pago en el estado exigido por el sistema. Tipo de solicitud 1 emisión o 2 renovación, datos del explotador, RUC, razón social, país, AOC y datos operativos del formulario. La obligatoriedad adicional depende del tipo y catálogo.

**Resultado funcional que debe acreditarse:** Solicitud identificada y vinculada al RT y compañía. El guardado de progreso no reemplaza la remisión documental ni la aceptación institucional.

**Tratamiento de fallos y comunicaciones:** Acceso bloqueado por orden, compañía o rol: resuelva esa dependencia antes de repetir el formulario. Si un campo no se guarda, revise el mensaje de su sección y confirme en el detalle, no solo en el navegador. El guardado de una sección no acredita una notificación institucional de aceptación. Revise los avisos generados al enviar el expediente.

**Referencia de fuente:** [CapaPresentacion/Controllers/SolicitudAOCRController.cs línea 259](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/SolicitudAOCRController.cs#L259). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P08 Solicitar una modificación y reconocer su limitación actual

**Actor y situación:** RT; Parcial en la base consultada.

**Controlador y contrato declarado:** FormularioEmisionAOCR(int? oid, int? tipoSolicitud) -> ActionResult; CerrarFaseDocumentalNuevoAeropuertoModificacion(int id, string observacion) -> ActionResult; GenerarCondicionesLimitacionesModificacion(int id, string observacion) -> ActionResult.

**Servicios contrastados:** AocrModificationWorkflowService, AocrCierrePorTipoTramiteService. **Persistencia de referencia:** SolicitudAOCRDAO. Las tablas relacionadas con el procedimiento son aocr_tbsolicitud; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Reconocimiento original identificable y operación de modificación autorizada. Tipo 3 y alcance de modificación; datos de flota, representante o nuevo aeropuerto según las secciones que realmente se habiliten.

**Resultado funcional que debe acreditarse:** Procedimiento documentado con bloqueo conocido; no se acredita un alta o cierre de modificación de extremo a extremo en esta base.

**Tratamiento de fallos y comunicaciones:** Tipo de solicitud rechazado: requiere corrección técnica del esquema y prueba institucional. Es una limitación del corte, no un dato que el RT deba falsear. No se promete notificación final para un trámite que no pudo persistirse o completar su flujo.

**Referencia de fuente:** [CapaPresentacion/Controllers/SolicitudAOCRController.cs línea 674](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/SolicitudAOCRController.cs#L674). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P09 Cargar y consultar los documentos del expediente

**Actor y situación:** RT; Implementado.

**Controlador y contrato declarado:** Lista(int solicitudId, string modo, string origen, int? inspeccionId) -> ActionResult; Detalle(int id) -> ActionResult; Subir(int? solicitudId) -> ActionResult; Subir(int? solicitudId, string tipoDocumento, HttpPostedFileBase archivo, string observaciones) -> ActionResult; Descargar(int id, bool vistaPrevia) -> ActionResult.

**Servicios contrastados:** FileStorageHelper, DocumentoBL. **Persistencia de referencia:** DocumentoDAO. Las tablas relacionadas con el procedimiento son aocr_tbdocumento; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Solicitud y compañía autorizadas; tipo documental disponible para esa etapa. Solicitud, tipo de documento, archivo y observaciones. La carga directa revisada admite PDF, JPG, JPEG y PNG con máximo de 10 MB; otras pantallas pueden aplicar límites distintos.

**Resultado funcional que debe acreditarse:** Archivo almacenado y asociado al expediente. El estado de carga no equivale a aceptación documental.

**Tratamiento de fallos y comunicaciones:** Archivo o tamaño no permitido: prepare una versión legible dentro del límite de esa pantalla. Si no aparece, recargue la lista y verifique solicitud, estado y compañía antes de repetirlo. Las comunicaciones de observación y revisión se producen en las acciones de revisión; una carga por sí sola no prueba aceptación ni entrega.

**Referencia de fuente:** [CapaPresentacion/Controllers/DocumentoController.cs línea 57](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/DocumentoController.cs#L57). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P10 Revisar documentos y registrar observaciones

**Actor y situación:** Inspector y Coordinador según la etapa; Implementado con restricciones por rol y etapa.

**Controlador y contrato declarado:** Lista(int solicitudId, string modo, string origen, int? inspeccionId) -> ActionResult; AceptarDocumentoSolicitud(int idDocumento, int codigoSolicitud, int? codigoInspeccion) -> JsonResult; DevolverDocumentoSolicitud(int idDocumento, int codigoSolicitud, int? codigoInspeccion, string motivoDevolucion) -> JsonResult; ReabrirDocumentoSolicitud(int idDocumento, int codigoSolicitud, int? codigoInspeccion) -> JsonResult.

**Servicios contrastados:** RevisionDocumentalService. **Persistencia de referencia:** DocumentoDAO, RevisionDocumentalDAO. Las tablas relacionadas con el procedimiento son aocr_tbdocumento; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Expediente asignado al revisor y etapa documental abierta. Solicitud, documento, decisión de aceptación o devolución y motivo de observación.

**Resultado funcional que debe acreditarse:** Decisiones documentales registradas por ítem. La remisión del conjunto requiere la acción de cierre o revisión final prevista.

**Tratamiento de fallos y comunicaciones:** No aparecen Aceptar o Devolver: revise si abrió modo ver, si la fase cerró o si el expediente no está asignado. No interprete una pantalla de consulta como un fallo de carga. La devolución y los avances documentales pueden generar correo y notificación interna. El mensaje debe relacionarse con la misma solicitud.

**Referencia de fuente:** [CapaPresentacion/Controllers/DocumentoController.cs línea 57](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/DocumentoController.cs#L57). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P11 Subsanar documentos observados

**Actor y situación:** RT; Implementado.

**Controlador y contrato declarado:** Subsanar(int id) -> ActionResult; EnviarSubsanacionAlInspector(int codigoSolicitud, string comentario) -> ActionResult; SubsanarPost(int codigoSolicitud, string comentario) -> ActionResult.

**Servicios contrastados:** RevisionDocumentalService. **Persistencia de referencia:** DocumentoDAO, SolicitudAOCRDAO. Las tablas relacionadas con el procedimiento son aocr_tbdocumento, aocr_tbsolicitud; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Documentos devueltos u observados y compañía del trámite seleccionada. Documento observado, archivo corregido y respuesta a la observación.

**Resultado funcional que debe acreditarse:** Subsanación remitida para nueva revisión, conservando la relación con el documento observado.

**Tratamiento de fallos y comunicaciones:** Reenvío no habilitado: compruebe que todos los documentos observados tengan una corrección guardada y que está usando el RT y compañía del trámite. El flujo contempla avisos de subsanación y revisión. Verifique el estado y no utilice la llegada de un correo como única evidencia de aceptación.

**Referencia de fuente:** [CapaPresentacion/Controllers/SolicitudAOCRController.cs línea 4793](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/SolicitudAOCRController.cs#L4793). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P12 Revisar y remitir el expediente documental a DIRCAV

**Actor y situación:** Coordinador; Implementado; comprobar la transición persistida.

**Controlador y contrato declarado:** RevisionVerificacion() -> ActionResult; DevolverAlInspector(int solicitudId, string comentario) -> ActionResult; RemitirADircav(int solicitudId, string observacion) -> ActionResult.

**Servicios contrastados:** RevisionDocumentalCoordinadorService. **Persistencia de referencia:** RevisionDocumentalCoordinadorDAO. Las tablas relacionadas con el procedimiento son aocr_revision_documental_coordinador, aocr_tbsolicitud; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Revisión del Inspector completada y expediente en etapa del Coordinador. Solicitud, observación final y versión o estado vigente que utilice la pantalla.

**Resultado funcional que debe acreditarse:** Expediente pendiente de aceptación formal por DIRCAV o devuelto al Inspector según la decisión.

**Tratamiento de fallos y comunicaciones:** Conflicto de versión o estado: recargue el expediente antes de decidir. El Coordinador no sustituye la aceptación ni la designación formal de DIRCAV. La remisión debe generar los avisos que el servicio registra. El envío transaccional y la entrega SMTP son hechos diferentes.

**Referencia de fuente:** [CapaPresentacion/Controllers/CoordinacionJefaturaController.cs línea 677](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/CoordinacionJefaturaController.cs#L677). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P13 Aceptar la documentación como DIRCAV

**Actor y situación:** DIRCAV; Implementado; validar permisos y condiciones del expediente.

**Controlador y contrato declarado:** BandejaDocumentacion() -> ActionResult; Detalle(int id) -> ActionResult; AceptarDocumentacion(int id, string observacion, int? versionEsperada) -> ActionResult; DevolverCoordinador(int id, string motivo) -> ActionResult.

**Servicios contrastados:** DircavDesignacionService, RevisionDocumentalCoordinadorService. **Persistencia de referencia:** AocrDesignacionDAO, RevisionDocumentalCoordinadorDAO. Las tablas relacionadas con el procedimiento son aocr_tbsolicitud, aocr_revision_documental_coordinador, aocr_tbinspeccion; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Expediente remitido por el Coordinador, sin observaciones incompatibles y con la documentación y pago exigidos. Solicitud, observación y versión esperada cuando se proporcione.

**Resultado funcional que debe acreditarse:** Documentación aceptada y condiciones para continuar a inspección establecidas, o expediente devuelto al Coordinador.

**Tratamiento de fallos y comunicaciones:** Error interno en la transacción: la pantalla no acredita aceptación. Consulte el estado persistido y reporte fecha, solicitud y mensaje al soporte. Los avisos de avance y asignación se generan en sus transiciones. Debe verificarse al RT real y al Inspector, sin agregar destinatarios personales no configurados.

**Referencia de fuente:** [CapaPresentacion/Controllers/DircavController.cs línea 179](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/DircavController.cs#L179). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P14 Designar al Inspector y configurar las estaciones

**Actor y situación:** DIRCAV; Coordinador en asignación preliminar autorizada; Implementado.

**Controlador y contrato declarado:** InspectoresDisponibles() -> ActionResult; DesignarInspector(int id, string inspectorCedula, string inspectorApoyoCedula, string observacion, int? estacionId, int? versionEsperada) -> ActionResult; DesignacionDetalle(int id) -> ActionResult.

**Servicios contrastados:** DircavDesignacionService. **Persistencia de referencia:** AocrDesignacionDAO, SolicitudEstacionDAO. Las tablas relacionadas con el procedimiento son aocr_tbsolicitud_estacion, aocr_tbinspeccion; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Aceptación documental completada y Inspector disponible para la función. Cédula del Inspector principal, apoyo opcional, observación, estación y versión esperada; fechas de inicio y fin por estación según la programación autorizada.

**Resultado funcional que debe acreditarse:** Inspector y estaciones relacionados con la inspección, con oficio pendiente de firma cuando corresponda.

**Tratamiento de fallos y comunicaciones:** Inspector no encontrado, estación ajena o versión cambiada: seleccione datos vigentes del mismo expediente y recargue antes de reintentar. La designación forma parte de los eventos notificables al Inspector y al RT. Verifique los destinatarios reales del expediente.

**Referencia de fuente:** [CapaPresentacion/Controllers/DircavController.cs línea 233](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/DircavController.cs#L233). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P15 Revisar firmar y descargar el oficio de designación

**Actor y situación:** DIRCAV para firmar; Inspector y RT para consultar según autorización; Implementado; firma y entrega real requieren prueba institucional.

**Controlador y contrato declarado:** VistaPreviaDesignacion(int id) -> ActionResult; FirmarDesignacion(int id, string passwordCertificado, HttpPostedFileBase certificadoDigital) -> ActionResult; DescargarDesignacion(int id) -> ActionResult; DescargarDesignacionInspector(int id) -> ActionResult.

**Servicios contrastados:** DesignacionDocumentoService, FirmaDigitalService. **Persistencia de referencia:** AocrDesignacionDAO. Las tablas relacionadas con el procedimiento son aocr_tbsolicitud, aocr_tbsolicitud_estacion; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Designación formal registrada, oficio generado y firmante DIRCAV autorizado. Certificado .p12 o .pfx y contraseña del certificado. Datos del explotador, Inspector y fechas deben estar correctos antes de firmar.

**Resultado funcional que debe acreditarse:** Oficio firmado almacenado, o operación rechazada sin acreditar firma.

**Tratamiento de fallos y comunicaciones:** MAC inválida: revise archivo y contraseña del certificado. Acceso denegado a la ruta: soporte debe comprobar permisos de la identidad del aplicativo sobre el almacenamiento compartido; no repita firmas para resolver un permiso de carpeta. La implementación contempla distribución del oficio al RT y al Inspector. Un correo en cola no prueba recepción final; consulte el resultado de entrega.

**Referencia de fuente:** [CapaPresentacion/Controllers/DircavController.cs línea 454](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/DircavController.cs#L454). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P16 Llenar finalizar y firmar la LV por estación

**Actor y situación:** Inspector asignado; Implementado por estación.

**Controlador y contrato declarado:** Detalle(string id) -> ActionResult; VerLvEaeOficial(int codigoInspeccion, int? estacionId) -> ActionResult; GuardarListaVerificacionOperacionalEae() -> ActionResult; FinalizarListaVerificacionOperacionalEae(int id) -> ActionResult; FirmarListaVerificacionOperacionalEae(int? id, string passwordCertificado) -> ActionResult.

**Servicios contrastados:** AocrFlujoValidacionService. **Persistencia de referencia:** ListaVerificacionOperacionalEaeDAO, SolicitudEstacionDAO. Las tablas relacionadas con el procedimiento son aocr_tbsolicitud_estacion; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Fase documental habilitada y asignación del Inspector al expediente y estación. Estación seleccionada, respuestas de la LV, observaciones, aeronaves realmente inspeccionadas y datos editables del numeral 4. La versión y estación deben corresponder al formulario abierto.

**Resultado funcional que debe acreditarse:** LV de una estación guardada, finalizada o firmada. Cada estación mantiene su evidencia independiente.

**Tratamiento de fallos y comunicaciones:** La LV no coincide con la estación: cierre el formulario, recargue la inspección y vuelva a seleccionar la estación. No cambie el identificador oculto ni intente reutilizar una LV histórica firmada. No se acredita una notificación de aceptación por guardar respuestas. Los avisos de avance se producen en las transiciones previstas.

**Referencia de fuente:** [CapaPresentacion/Controllers/InspeccionController.cs línea 800](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/InspeccionController.cs#L800). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P17 Preparar y firmar el informe técnico por estación

**Actor y situación:** Inspector asignado; Implementado por estación.

**Controlador y contrato declarado:** ModalInformeTecnico(int codigoInspeccion) -> ActionResult; GuardarInformeTecnico() -> ActionResult; PrevisualizarInformeTecnico() -> JsonResult; FinalizarInformeTecnico(int id) -> ActionResult; FirmarInformeInspector(int? id, string passwordCertificado) -> ActionResult; ReintentarNotificacionInformeDcav(int id) -> ActionResult.

**Servicios contrastados:** AocrFlujoValidacionService. **Persistencia de referencia:** InspeccionInformeDAO. Las tablas relacionadas con el procedimiento son aocr_tbinforme_inspeccion; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** LV finalizada y firmada para la estación seleccionada. Estación, contenido técnico, hallazgos, conclusión, resultado y adjuntos que solicite el formulario.

**Resultado funcional que debe acreditarse:** Informe firmado y trazabilidad por estación; avance o remisión a la revisión que corresponda. Los informes no satisfactorios conducen al tratamiento de no conformidades.

**Tratamiento de fallos y comunicaciones:** Debe finalizar y firmar la LV: compruebe la evidencia de la estación. Un HTTP 400 o 409 requiere revisar id, estación, versión y estado antes de repetir la operación. La firma del informe genera la remisión y notificación implementadas a DIRCAV; existen reintentos de comunicación sin repetir la firma.

**Referencia de fuente:** [CapaPresentacion/Controllers/InspeccionController.cs línea 1103](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/InspeccionController.cs#L1103). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P18 Registrar revisar y subsanar una no conformidad

**Actor y situación:** Inspector, Coordinador y RT en etapas distintas; Implementado con ciclos y precondiciones de revisión.

**Controlador y contrato declarado:** FirmarNcInspector(int? id, string tipoRuta, string passwordCertificado) -> ActionResult; CoordinadorAprobarNc(int id, string passwordCertificado) -> ActionResult; CoordinadorDevolverNc(int id, string observacionDevolucion) -> ActionResult; AceptarSubsanacionNc(int codigoNoConformidad, int codigoInspeccion) -> ActionResult; DevolverSubsanacionNc(int codigoNoConformidad, int codigoInspeccion, string observacionDevolucion) -> ActionResult; RegistrarHallazgo(Hallazgo h) -> ActionResult; RegistrarNoConforme(int id, string descripcion, string criticidad) -> ActionResult.

**Servicios contrastados:** Gate8WorkflowEventService. **Persistencia de referencia:** NoConformidadDAO. Las tablas relacionadas con el procedimiento son aocr_tbnoconformidad; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Hallazgo o resultado que requiere no conformidad y actor autorizado para la etapa. Hallazgo, no conformidad, motivo técnico, documento de subsanación, respuesta y relación con la inspección o ciclo.

**Resultado funcional que debe acreditarse:** NC registrada y tratada en su ciclo, con aceptación o nueva devolución de la subsanación. La carga no implica cierre automático.

**Tratamiento de fallos y comunicaciones:** NC de otro trámite o evidencia pendiente: abra la relación correcta. No marque el informe satisfactorio para eludir una NC que continúa abierta. Los eventos de NC, revisión y subsanación alimentan avisos del flujo. La trazabilidad debe relacionar NC raíz, inspección y respuesta.

**Referencia de fuente:** [CapaPresentacion/Controllers/InspeccionController.cs línea 3742](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/InspeccionController.cs#L3742). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P19 Solicitar y seguir una nueva inspección

**Actor y situación:** RT e Inspector según etapa; Implementado con dependencias de nueva orden y ciclo.

**Controlador y contrato declarado:** SolicitarNuevaInspeccion(int codigoSolicitud) -> ActionResult.

**Servicios contrastados:** Gate8WorkflowEventService. **Persistencia de referencia:** InspeccionDAO, NoConformidadDAO. Las tablas relacionadas con el procedimiento son aocr_tbinspeccion, aocr_tbnoconformidad, aocr_or_orden; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Expediente y NC o resultado que habilitan una nueva inspección. Solicitud original y relación con el ciclo, orden y nueva inspección cuando se creen.

**Resultado funcional que debe acreditarse:** Nueva actuación vinculada al expediente o ciclo original; no una sobrescritura del informe firmado previo.

**Tratamiento de fallos y comunicaciones:** Acción no habilitada: revise la NC, el resultado y la fase del trámite. No duplique manualmente solicitudes para simular el ciclo previsto. El catálogo de eventos contempla nueva inspección, nueva solicitud y nueva orden. La ejecución y entrega deben verificarse en el entorno institucional.

**Referencia de fuente:** [CapaPresentacion/Controllers/RTController.cs línea 437](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/RTController.cs#L437). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P20 Preparar los datos y generar AOCR y Condiciones

**Actor y situación:** Inspector asignado; Implementado con limitaciones de formato heredado y operación única por estación.

**Controlador y contrato declarado:** Index(int solicitudId) -> ActionResult; GuardarDatos(GuardarDatosFirmaAocrRequest request) -> ActionResult; GenerarPdf(int solicitudId) -> ActionResult; VerPdf(int solicitudId, bool firmado, string tipoDocumento) -> ActionResult; DescargarPdf(int solicitudId, bool firmado, string tipoDocumento) -> ActionResult.

**Servicios contrastados:** FirmaAocrWorkflowService, DatosDocumentalesAocrReglas, AocrCierrePorTipoTramiteService. **Persistencia de referencia:** CertificadoDAO, AocrDocumentoGeneradoDAO. Las tablas relacionadas con el procedimiento son aocr_tbsolicitud, aocr_tbcertificado, aocr_tbdocumento_generado; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Todos los informes exigidos finalizados, firmados y satisfactorios; Inspector asignado y expediente no enviado o firmado. Permiso CNAC y sus dos fechas; vigencia del AOC; país de bandera; contactos independientes de explotador, gerente de Operaciones, RT y representante legal en Ecuador; operación por estación; vencimiento y nombres de firmantes institucionales.

**Resultado funcional que debe acreditarse:** PDF vigentes de los documentos requeridos y versiones anteriores conservadas. El plan actual de modificación genera CL; no se presume un nuevo AOCR para ese tipo.

**Tratamiento de fallos y comunicaciones:** Campos incompletos o fechas invertidas: corrija la fuente y vuelva a validar. Error de unicidad al regenerar: revise que esté publicada la corrección del registro transaccional de versiones; no elimine índices ni borre el historial. La generación registra avances; aún no representa el envío del paquete al Coordinador ni habilita firmas institucionales.

**Referencia de fuente:** [CapaPresentacion/Controllers/FirmaAocrController.cs línea 38](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/FirmaAocrController.cs#L38). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P21 Enviar y revisar el paquete final con el Coordinador

**Actor y situación:** Inspector para enviar y Coordinador para revisar; Implementado; recepción del caso reportado pendiente de validación institucional.

**Controlador y contrato declarado:** FinalizarDocumentosYEnviarParaFirma(int solicitudId) -> ActionResult; PaquetesCoordinador() -> ActionResult; RevisarPaquete(int solicitudId, long versionEsperada, bool devolver, string observacion) -> ActionResult.

**Servicios contrastados:** DocumentosFinalesWorkflowService. **Persistencia de referencia:** DocumentosFinalesWorkflowDAO. Las tablas relacionadas con el procedimiento son aocr_tbdocumento_generado, aocr_proceso_estado, aocr_evento_workflow, aocr_tbnotificacion, email_queue; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** PDF requeridos vigentes, completos, íntegros y todavía editables; sin NC u observaciones que bloqueen el envío. Solicitud, versiones y evidencias de ambos documentos; observación del Coordinador y versión esperada para decidir.

**Resultado funcional que debe acreditarse:** Paquete bloqueado en revisión del Coordinador, devuelto al Inspector o remitido a DIRCAV. Las firmas siguen separadas por autoridad.

**Tratamiento de fallos y comunicaciones:** Versión cambiada: recargue y vuelva a revisar la evidencia. No existen destinatarios activos: Administración debe revisar roles y correos. Rechazo de transacción: soporte consulta la referencia y no reenvía masivamente por fuera del sistema. La misma transacción registra estados, evento, avisos internos y correos de AOCR y CL al Coordinador. Un fallo revierte todo; SMTP se procesa después del commit.

**Referencia de fuente:** [CapaPresentacion/Controllers/FirmaAocrController.cs línea 348](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/FirmaAocrController.cs#L348). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P22 Revisar y firmar Condiciones como DIRCAV

**Actor y situación:** DIRCAV; Implementado; convive con circuito heredado de CL.

**Controlador y contrato declarado:** Index(int solicitudId) -> ActionResult; DescargarFirmado(int solicitudId, string tipoDocumento) -> ActionResult; Firmar(FirmarAocrInstitucionalRequest request, int solicitudId) -> ActionResult.

**Servicios contrastados:** FirmaAocrDigitalService, DocumentosFinalesWorkflowService. **Persistencia de referencia:** DocumentosFinalesWorkflowDAO, AocrFirmaDocumentoDAO. Las tablas relacionadas con el procedimiento son aocr_tbdocumento_generado, aocr_tbfirma_documento; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Paquete revisado por Coordinador y CL en estado habilitado para DIRCAV; firmante institucional autorizado. Documento vigente, certificado .p12/.pfx, contraseña y datos de firma de la pantalla.

**Resultado funcional que debe acreditarse:** CL firmadas por DIRCAV o documento devuelto; AOCR todavía requiere remisión formal y firma DIRDAC.

**Tratamiento de fallos y comunicaciones:** Documento no habilitado, devuelto o versión diferente: revise el recorrido Coordinador-DIRCAV. Si ve el formato anterior con matrículas o AOC usado como AOCR, no lo valide por el solo hecho de ser una vista previa. La firma y avance generan los avisos del circuito. La distribución final al RT depende del cierre y entrega institucionales.

**Referencia de fuente:** [CapaPresentacion/Controllers/FirmaAocrController.cs línea 38](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/FirmaAocrController.cs#L38). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P23 Remitir el AOCR formalmente a DIRDAC

**Actor y situación:** DIRCAV; Implementado con permiso granular y precondiciones.

**Controlador y contrato declarado:** RemitirAocrDirdac(RemitirAocrDirdacRequest request) -> ActionResult; RemitirDIRDAC(int id, string observacion, long versionEsperada, int documentoId, int versionAocrEsperada) -> ActionResult.

**Servicios contrastados:** AocrFinalWorkflowService. **Persistencia de referencia:** AocrFinalWorkflowDAO. Las tablas relacionadas con el procedimiento son aocr_proceso_estado, aocr_tbdocumento_generado; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** CL y AOCR en estados exigidos; revisión de informes y documentos finalizada; actor DIRCAV con permiso DIRCAV_REMITIR_DIRDAC. Solicitud, observación, documento y versiones esperadas de expediente y AOCR.

**Resultado funcional que debe acreditarse:** AOCR pendiente de revisión o firma DIRDAC según la transición. DIRCAV conserva su decisión y evidencia de remisión.

**Tratamiento de fallos y comunicaciones:** Permiso requerido o versión obsoleta: revise el rol activo, permiso y versión antes de reintentar. Administrador no debe operar como autoridad firmante para resolver el bloqueo. Se generan los eventos y notificaciones previstos para la autoridad receptora. El correo no reemplaza el estado y permisos del expediente.

**Referencia de fuente:** [CapaPresentacion/Controllers/DircavController.cs línea 949](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/DircavController.cs#L949). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P24 Revisar y firmar el AOCR como DIRDAC

**Actor y situación:** DIRDAC; Implementado con segregación de autoridad.

**Controlador y contrato declarado:** BandejaAocr() -> ActionResult; Detalle(int id) -> ActionResult; DevolverAocrDircav(DevolverAocrDircavRequest request) -> ActionResult; FirmarLegalizarAocr(FirmarLegalizarAocrRequest request) -> ActionResult.

**Servicios contrastados:** AocrFinalWorkflowService. **Persistencia de referencia:** AocrFinalWorkflowDAO. Las tablas relacionadas con el procedimiento son aocr_tbdocumento_generado, aocr_tbfirma_documento, aocr_proceso_estado; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Remisión formal DIRCAV y permiso DIRDAC_FIRMAR_AOCR; AOCR vigente e íntegro. Solicitud, observación o motivo, versiones esperadas y certificado institucional para la firma.

**Resultado funcional que debe acreditarse:** AOCR firmado/legalizado o devuelto. El sistema debe evaluar el cumplimiento del conjunto de firmas antes de entregar los documentos.

**Tratamiento de fallos y comunicaciones:** No existe remisión DIRCAV o falta permiso: el AOCR no debe aparecer como firmable. Si el certificado falla, no comparta su contraseña con otro usuario para que firme por usted. Los avisos de firma y cierre se procesan conforme al servicio de flujo. La entrega final se comprueba en su operación y cola específicas.

**Referencia de fuente:** [CapaPresentacion/Controllers/DirdacController.cs línea 27](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/DirdacController.cs#L27). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P25 Verificar cierre entrega y descarga final

**Actor y situación:** RT; DIRDAC y Administración solo en sus funciones de control; Implementado; entrega externa real por validar en cada entorno.

**Controlador y contrato declarado:** DocumentosFinales() -> ActionResult.

**Servicios contrastados:** EntregaFinalService, AocrFinalWorkflowService. **Persistencia de referencia:** EntregaFinalDAO, AocrFinalWorkflowDAO. Las tablas relacionadas con el procedimiento son aocr_proceso_estado, aocr_tbdocumento_generado, email_queue, email_attachment; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Conjunto de documentos y firmas exigidos por el tipo de trámite, integridad verificada y cierre institucional habilitado. Solicitud, archivos firmados y destinatarios autorizados. Las jefaturas de aeropuerto dependen de configuración real.

**Resultado funcional que debe acreditarse:** Documentos finales disponibles y entrega trazable cuando el proceso se confirma. Recepción SMTP y descarga del RT son evidencias diferentes.

**Tratamiento de fallos y comunicaciones:** Archivo no encontrado, vacío o hash distinto: no distribuya esa copia. Soporte debe revisar almacenamiento, metadatos y hash del firmado. El RT no debe recibir una versión preliminar como final. La entrega utiliza cola y adjuntos con rutas e integridad. Un estado pendiente o fallido de email requiere tratamiento de comunicación, sin volver a firmar PDFs.

**Referencia de fuente:** [CapaPresentacion/Controllers/RTController.cs línea 582](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/RTController.cs#L582). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P26 Consultar el archivo documental después de cerrar la fase

**Actor y situación:** Inspector asignado y revisores autorizados; Parcial en navegación de auditoría por rol.

**Controlador y contrato declarado:** Lista(int solicitudId, string modo, string origen, int? inspeccionId) -> ActionResult; Detalle(int id) -> ActionResult; Descargar(int id, bool vistaPrevia) -> ActionResult.

**Servicios contrastados:** RevisionDocumentalBandejaService. **Persistencia de referencia:** DocumentoDAO. Las tablas relacionadas con el procedimiento son aocr_tbdocumento; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Permiso para el expediente, incluso si ya no figura como pendiente de revisión. Solicitud, documento y relación con la inspección o compañía autorizada.

**Resultado funcional que debe acreditarse:** Consulta de la evidencia disponible; no reapertura de la fase ni modificación del historial.

**Tratamiento de fallos y comunicaciones:** Bandeja vacía no prueba eliminación de documentos. Si recibe acceso denegado, valide asignación y contexto; no copie archivos directamente desde una carpeta compartida como método de trabajo del usuario final. Consultar o descargar no significa enviar una comunicación institucional.

**Referencia de fuente:** [CapaPresentacion/Controllers/DocumentoController.cs línea 57](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/DocumentoController.cs#L57). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P27 Administrar usuarios roles y permisos

**Actor y situación:** Administrador; Implementado con permisos administrativos.

**Controlador y contrato declarado:** Index(string filtro, bool? activo, string tipo, string perfil, string tab, string rolFiltro) -> ActionResult; Create() -> ActionResult; Create(AdminUsuarioFormViewModel model) -> ActionResult; Edit(int id) -> ActionResult; Edit(AdminUsuarioFormViewModel model) -> ActionResult; CambiarEstado(int id, bool activo) -> ActionResult; ReenviarCredenciales(int id) -> ActionResult; ResetPassword(int id) -> ActionResult; Roles(int id) -> ActionResult; Roles(AdminUsuarioRolesViewModel model) -> ActionResult; PermisosRol(int? codigoRol) -> ActionResult; PermisosRol(AdminRolPermisosViewModel model) -> ActionResult.

**Servicios contrastados:** UsuarioContextoService. **Persistencia de referencia:** UsuarioDAO, RolDAO. Las tablas relacionadas con el procedimiento son usuario, rol, usuario_rol; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Cuenta administrativa autorizada; solicitud y justificación de acceso identificadas. Identidad, correo, perfil, estado, roles y permisos que ofrece la pantalla. Las credenciales temporales se gestionan por las acciones previstas, no se documentan en este manual.

**Resultado funcional que debe acreditarse:** Cuenta y permisos modificados con trazabilidad. Las decisiones de expediente siguen perteneciendo a sus actores institucionales.

**Tratamiento de fallos y comunicaciones:** El rol se ve pero la acción no funciona: compruebe permiso granular, estado de cuenta, contexto activo y restricciones del expediente. Un menú visible no demuestra autorización completa. Altas, restablecimientos y reenvíos pueden generar correos de cuenta. No publique claves ni capture pantallas que las expongan.

**Referencia de fuente:** [CapaPresentacion/Controllers/AdminUsuariosController.cs línea 106](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/AdminUsuariosController.cs#L106). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P28 Administrar parámetros catálogos y destinatarios

**Actor y situación:** Administrador; Implementado; valores oficiales y aprobación de cambio por confirmar.

**Controlador y contrato declarado:** Index() -> ActionResult; Crear() -> ActionResult; Crear(Parametro parametro) -> ActionResult; Editar(int id) -> ActionResult; Editar(Parametro parametro) -> ActionResult; AplicarTarifasAocrOficiales() -> ActionResult; SincronizarConceptosAocr() -> ActionResult.

**Servicios contrastados:** SecureConfigurationService. **Persistencia de referencia:** ParametroDAO. Las tablas relacionadas con el procedimiento son email_queue; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Permiso de administración y cambio funcional aprobado fuera de este manual. Clave, valor y datos de catálogo que muestre la pantalla; destinatarios institucionales activos y sus ámbitos.

**Resultado funcional que debe acreditarse:** Configuración actualizada dentro de su alcance. No se valida automáticamente el efecto integral sobre trámites históricos.

**Tratamiento de fallos y comunicaciones:** Cambio sin efecto: revise prioridades de entorno/configuración y caché con soporte. No repita cambios de tarifas en producción para probar un problema de pantalla. La configuración de un destinatario no equivale a enviar un aviso; la comunicación se genera al ejecutar una transición.

**Referencia de fuente:** [CapaPresentacion/Controllers/ParametroController.cs línea 15](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/ParametroController.cs#L15). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

## P29 Consultar auditoría notificaciones y correos pendientes

**Actor y situación:** Administrador para soporte; cada usuario para sus notificaciones; Implementado con visibilidad y herramientas según permisos.

**Controlador y contrato declarado:** Index() -> ActionResult; ObtenerNoLeidas() -> JsonResult; MarcarComoLeida(int id) -> JsonResult; MarcarTodasComoLeidas() -> JsonResult.

**Servicios contrastados:** EmailQueueProcessor, LoggingService. **Persistencia de referencia:** LogDAO. Las tablas relacionadas con el procedimiento son aocr_tbnotificacion, email_queue, aocr_tblog; la matriz distingue las referencias directas de los nombres candidatos del archivo.

**Validación de entrada y alcance:** Cuenta autorizada para la consulta; solicitud o incidencia identificada. Usuario, trámite, fecha, acción y referencia de error o comunicación.

**Resultado funcional que debe acreditarse:** Aviso atendido o incidencia de comunicación localizada y escalada. Leer o eliminar un aviso no revierte la decisión de expediente.

**Tratamiento de fallos y comunicaciones:** No hay registro en cola ni cambio de estado: investigue la transición, no solo SMTP. Si el correo figura enviado, compruebe las evidencias de transporte y buzón mediante el procedimiento institucional de soporte. El procesador de email trabaja en segundo plano dentro de la aplicación; un reinicio de IIS puede interrumpir o retrasar la cola.

**Referencia de fuente:** [CapaPresentacion/Controllers/NotificacionController.cs línea 46](https://github.com/gercavi82/AOCR2026.1/blob/6d885a984bb875507366a824a290681450376d87/CapaPresentacion/Controllers/NotificacionController.cs#L46). Las excepciones capturadas de cada método y referencias de prueba figuran en los catálogos.

# Pruebas evidencia y verificación del paquete

## Compilación y pruebas del corte

Se ejecutó compilación de aplicación y vistas Razor y una selección de pruebas de flujo, autorización, estaciones, documentos finales y entrega. El resultado de la selección es **246 correctas y 15 omitidas**, 261 casos ejecutados o considerados por el adaptador. El inventario estático localiza 782 métodos de prueba; la selección no acredita la suite completa ni convierte los casos omitidos en aprobados.

Los resultados anteriores citados en registros de cambios sirven como antecedentes, con su fecha y entorno. No se suman como pruebas nuevas de esta documentación. Tampoco se incluyen como prueba de firma, SMTP, DB2 o publicación las pruebas que solo inspeccionan texto de fuente. La matriz de trazabilidad señala cuándo una referencia se obtiene por coincidencia textual.

El esquema se exportó sin datos. Su restauración estructural en instancia temporal se registra en el informe de validación, separada de la versión del servidor origen. No se ejecutó un circuito institucional con certificados reales ni se enviaron correos para obtener capturas.

## Plan mínimo de validación institucional

| Caso | Evidencia que se debe conservar |
| --- | --- |
| Acceso por siete roles | Rol activo, compañía, permisos y denegación de acciones de otra autoridad. |
| Orden y pago | Orden única, comprobante, factura, decisión y resultado de integración separado. |
| Revisión y subsanación | Decisión por documento, motivo, nueva evidencia e historial. |
| Aceptación DIRCAV | Cambio documental y habilitación técnica para Inspector asignado. |
| Estaciones independientes | LV e informe de UIO y GYE sin cruce; historial firmado intacto. |
| NC y nueva inspección | Relación de NC raíz/ciclo y nueva orden/inspección si aplica. |
| Datos y PDF oficiales | CNAC/AOC/contactos separados, modelos sin matrícula en plantilla actual y número original consistente. |
| Paquete final | Ambos PDF y versiones, estado Coordinador, avisos internos, cola e idempotencia. |
| Firmas separadas | DIRCAV firma CL; remisión formal; DIRDAC firma AOCR; denegación de Administrador. |
| Entrega | Conjunto de firmas y hashes, destinatarios válidos, estado de email y descarga autorizada del RT. |
| Recuperación | Fallo forzado, rollback transaccional, reintento, reinicio de trabajador y restauración de respaldo. |

Las evidencias pendientes deben actualizarse cuando exista una sesión de pruebas autorizada. No se utilizan usuarios genéricos de autoridad para producir una apariencia de aprobación institucional en los documentos.

## Verificación documental y accesibilidad

Los Word utilizan estilos de título y encabezado, listas con reinicio, tablas con cabecera repetible, texto alternativo y figuras acompañadas de contenido textual. Los diagramas se entregan editables en draw.io y Mermaid. Los PDF derivan de los mismos Word; la revisión visual debe comprobar cada página, márgenes, cortes, tabla de contenido y ausencia de páginas vacías.

La revisión de accesibilidad del documento no acredita WCAG ni accesibilidad del aplicativo. La única captura histórica disponible se rotula y no se presenta como una colección de pantallas vigentes. Las tablas extensas de inventario se entregan en CSV para conservar legibilidad y permitir filtros, en vez de comprimir miles de filas dentro del Word.

# Catálogos anexos y mantenimiento documental

| Anexo | Contenido y límite de interpretación |
| --- | --- |
| 01_proyectos.csv | Todos los csproj localizados y condición de inclusión en solución; no suma archivos históricos como proyectos activos. |
| 02_controladores_acciones.csv | Acciones y sobrecargas, entrada/salida, verbos y atributos, fuente y referencias. Las rutas convencionales son candidatas a contrastar con RouteConfig. |
| 03_servicios_interfaces.csv | Servicios e interfaces con métodos, dependencias localizadas y excepciones capturadas. |
| 04_dao_metodos.csv | DAO y métodos; tablas locales y tablas candidatas del archivo separadas. |
| 05_modelos_viewmodels.csv | Entidades, solicitudes, respuestas, DTO y propiedades declaradas. |
| 06_estados_constantes.csv | Constantes de estados y workflow con archivo y línea; no equivale a transición habilitada. |
| 07_casos_prueba.csv | Métodos de prueba localizados y fuente, sin promoverlos a ejecutados. |
| 08_diccionario_datos.csv | Columnas reales del esquema consultado, tipos, nulabilidad, longitud y defaults seguros. |
| 09_relaciones_restricciones.csv y 10_indices.csv | FK, PK, UNIQUE, CHECK e índices del esquema. |
| 11_vistas_bd.csv | Vistas PostgreSQL localizadas, separadas de Razor. |
| 12_vistas_partiales.csv y 13_javascript_css.csv | Vistas y partials, modelos, formularios/referencias y recursos con hashes. |
| 14_plantillas_pdf.csv | Candidatos por nombre para localizar plantillas; confirmar el generador en las fichas. |
| 15_eventos_correos.csv | Asignaciones literales de tipos y eventos; los valores dinámicos se revisan en servicio. |
| 16_menu.csv y 17_configuracion_sin_valores.csv | Etiquetas y destinos del menú, condiciones localizadas y claves de configuración sin secretos. |
| 18_scripts_bd.csv y 19_inventario_archivos.csv | Scripts y archivos con fuentes y huellas; no autoriza ejecución automática. |
| 20_matriz_trazabilidad.csv y 21_matriz_roles.csv | Requisito-procedimiento-pantalla-controlador-servicio-DAO-tabla-prueba y alcance funcional por rol. |
| Esquema y JSON de referencia | Estructura sin datos, metadatos y comentarios observados; no incluye seeds operativos ni credenciales. |
| Diagramas editables | Flujo general y recorridos por siete roles; los retornos y condiciones se precisan en los procedimientos. |

## Información pendiente de confirmación

Se entrega una lista separada con responsables institucionales por definir. Incluye códigos documentales oficiales, aprobación, URL y ambiente, parámetros de infraestructura y AS400, políticas de respaldo/retención/RPO/RTO, calendario de trabajadores externos, capturas por rol y validación de firma y SMTP. Las contradicciones tipo 3, formato CL, operación múltiple y archivo documental siguen identificadas como pendientes o parciales.

No se encontró un acta de aprobación formal de todas las rutas heredadas ni evidencia de que el requisito de entrega final del caso reportado haya sido satisfecho. Esos vacíos deben conservarse en la entrega y no borrarse del manual para que parezca terminado el sistema.

## Actualización posterior del manual

1. Fije nueva rama, commit, esquema y fecha de corte. Conserve el paquete anterior sin sobrescribir su evidencia.
2. Regenerar inventarios no basta: revise los procedimientos afectados y sus validaciones de negocio.
3. Actualice pruebas y capturas con su entorno y resultado, separando casos de fuente de casos funcionales.
4. Revise Word y PDF de todas las páginas, confirme enlaces, matriz, diccionario y ausencia de secretos.
5. Registre el cambio y obtenga revisión y aprobación institucionales con nombres reales. No complete firmas de aprobación de forma automática.

| Versión documental | Fecha | Cambio |
| --- | --- | --- |
| 1.0 | 03/11/2026 | Levantamiento del commit y esquema confirmados, contratos técnicos, operación, catálogos, procedimientos y limitaciones trazables. |
