# Información pendiente de confirmación

La lista distingue datos institucionales que faltan de defectos o validaciones funcionales. No se utilizan estos campos vacíos para completar firmas, códigos o configuraciones supuestas.

| ID | Información o validación | Responsable a confirmar | Efecto sobre la entrega |
|---|---|---|---|
| I01 | Código documental oficial de ambos manuales y versión formal | Dirección de Tecnologías de Información y Comunicación | Los códigos se muestran como propuestos |
| I02 | Nombres de elaboración revisión y aprobación y acta de aceptación | Autoridades institucionales | No consta aprobación; no se simulan firmas |
| I03 | URL y ambiente de uso, bindings TLS, proxy y cuenta del pool IIS | Infraestructura | Los procedimientos usan rutas de referencia y configuración por entorno |
| I04 | Vigencia de la imagen institucional del logotipo del repositorio | Comunicación institucional | Se utiliza el recurso existente con su origen identificado |
| I05 | Capturas autenticadas actuales de RT y selección de compañía, orden, pago y solicitud | Responsable de pruebas RT | No se inventan capturas ni se confirman pantallas solo por una lista de archivos |
| I06 | Capturas de validación y corrección financiera | Responsable de pruebas Financiero | Se requieren evidencias de factura y tratamiento FR3 |
| I07 | Capturas de revisión documental y paquete final del Coordinador | Responsable de pruebas Coordinador | No consta recepción real del caso reportado |
| I08 | Capturas de aceptación, designación, CL y remisión de DIRCAV | Responsable de pruebas DIRCAV | Deben mostrar el flujo autorizado y versión correcta de PDF |
| I09 | Capturas del Inspector, LV e informe por estación y archivo documental | Responsable de pruebas Inspector | La única captura del repositorio es un ejemplo anterior de solo lectura |
| I10 | Capturas de devolución/firma AOCR de DIRDAC y cierre | Responsable de pruebas DIRDAC | No se utiliza firma o cuenta de autoridad simulada para completar páginas |
| I11 | Capturas administrativas de roles, permisos, parámetros y auditoría | Administración | Deben excluir credenciales y datos personales no necesarios |
| I12 | Corrección y prueba del tipo 3 frente al CHECK de la base consultada | Desarrollo y DBA | Modificación se clasifica parcial; código y esquema difieren |
| I13 | Selección múltiple de R NR RC ET A por estación | Análisis funcional y desarrollo | En este corte solo hay un Codigo por estación |
| I14 | Unificación de formato y circuito CL institucional/heredado | Análisis funcional y desarrollo | No se afirma que firmar cambie el formato revisado |
| I15 | Acceso completo de auditoría al archivo de expedientes cerrados por Inspector | Análisis funcional y seguridad | Consulta y navegación requieren validación por asignación y fase |
| I16 | Validación integral de paquete Inspector-Coordinador-DIRCAV-DIRDAC | Responsables de pruebas de los cuatro roles | Mensaje de envío no equivale a transacción y recepción confirmadas |
| I17 | Prueba con certificados institucionales, cadena y política de validación | Autoridades firmantes y seguridad | Compilación o prueba de fuente no acredita firma real |
| I18 | Confirmación de entrega SMTP y recepción final al RT y destinatarios por estación | Correo institucional y pruebas | Cola, transporte y lectura de buzón son evidencias distintas |
| I19 | Destinatarios institucionales vigentes de jefaturas aeroportuarias | Unidad responsable | No se crean direcciones de correo supuestas |
| I20 | Entorno DB2/AS400, driver, DSN, bibliotecas, permisos y ventanas | Infraestructura financiera | No se probó escritura externa ni facturación real |
| I21 | Calendario y cuentas de tareas externas a IIS, si existen | Infraestructura | No se presume servicio Windows por existir una clase Worker |
| I22 | Política de respaldo, retención documental, RPO, RTO y restauración completa con datos | Infraestructura DBA y archivo | La restauración probada es solo estructural en instancia temporal |
| I23 | Fuentes aprobadas de cuentas permisos y catálogos semilla para instalación nueva | Administración y DBA | El esquema sin datos no es un bootstrap funcional completo |
| I24 | Revisión de la excepción de activación por correo en UsuarioBL y rutas administrativas amplias | Seguridad y desarrollo | Se informa el riesgo sin divulgar el correo ni darlo como política aprobada |
| I25 | Validación de accesibilidad del aplicativo con teclado y lector de pantalla | Calidad y accesibilidad | La estructura accesible de los Word no acredita la aplicación |

Confirmaciones recibidas: rama y commit de corte, uso de evidencias existentes y base configurada para el diccionario. La evidencia se conserva en version_corte.json; no se requieren contraseñas ni exportaciones de registros de usuarios para revisar estos manuales.
