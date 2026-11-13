# Levantamiento previo a los manuales AOCR

Fecha de corte documental: 03/11/2026, America/Guayaquil. Rama y commit confirmados por el usuario: `fix/cierre-flujo-aocr`, `6d885a984bb875507366a824a290681450376d87`. `git fetch origin --prune` y la comparación HEAD/remoto devolvieron 0 commits de diferencia. El árbol de trabajo estaba limpio al iniciar. Se crean exclusivamente documentación y herramientas de generación; no se modifica el flujo de la aplicación ni se ejecutan transiciones o correos en el servidor.

## Hallazgos informados antes de redactar

| Hallazgo | Evidencia | Tratamiento documental |
|---|---|---|
| Los manuales anteriores atribuyen .NET 4.8 a toda la solución | Los seis proyectos de aplicación y utilidad declaran 4.6.2; AOCR.Tests declara 4.8 | Separar framework de destino y entorno de pruebas. Advertir paquetes con otros destinos y la advertencia iText de compilación |
| Aceptación y designación atribuidas al Coordinador en textos anteriores | DircavController.AceptarDocumentacion y DesignarInspector; servicios de autorización y rol canónico | DIRCAV acepta y designa; Coordinador revisa y remite. No combinar autoridades |
| Modificación tipo 3 existe en código, pero no es admisible por el CHECK consultado | AocrCierrePorTipoTramiteService y chk_tipo_solicitud de PostgreSQL con valores 0,1,2 | Clasificar modificación como parcial para la base consultada. No prometer alta o cierre sin regularización y prueba |
| Dos circuitos de Condiciones y formatos diferentes | FirmaAocrServices y CondicionesLimitacionesService; DocumentoController.DescargarCondicionesLimitaciones | Distinguir circuito institucional de paquete y ruta heredada; nunca afirmar que el formato cambia mágicamente al firmar |
| R/NR/RC/ET/A solo admite una opción por estación | Views/FirmaAocr/_DatosDocumentales.cshtml, DropDownList y propiedad Codigo | Documentar selección única real y requerimiento múltiple pendiente |
| El archivo documental desaparece de la bandeja operativa al cerrar fase | RevisionDocumentalBandejaService filtra ExcluirPorDocumentacionCerrada | Diferenciar bandeja de pendientes de conservación/consulta. El acceso de auditoría directo por rol requiere validación |
| Los 42 PNG anunciados no existen como capturas utilizables | docs/images contiene una imagen de ejemplo y README con una lista de capturas pendientes | Incorporar solo la evidencia disponible, rotulada como histórica y modo de solo lectura; registrar capturas faltantes por rol |
| Una constante o acción inventariada no prueba disponibilidad efectiva | Autorización por atributos, filtros globales y servicios; rutas convencionales y aliases | Rutas de atributo se catalogan como declaraciones; RouteConfig no llama MapMvcAttributeRoutes. Validar aliases explícitos y no presumir habilitación de cada atributo |
| Falta acta que acredite envío del paquete concreto y entrega SMTP del caso reportado | Incidencias del 27/28 en docs/OBSERVACIONES_AOCR_20261027.md; no hubo sesión institucional autenticada durante esta revisión | Separar implementación de cola, confirmación de transacción y entrega real del correo |
| La prioridad de configuración no es uniforme | CertificadoDAO lee AOCRConnection directamente; EmailQueueService resuelve PostgreSQL mediante SecureConfigurationService | Documentar ambos nombres y comprobar que apuntan al entorno previsto. No prometer que una variable de entorno reemplaza todas las conexiones |
| Existe una excepción de activación de cuenta por correo específico | UsuarioBL.Autenticar, antes de verificar contraseña, contiene una lista especial y puede activar la cuenta | Registrar como riesgo para revisión de seguridad, sin publicar la dirección ni presentarlo como política institucional aprobada |

## Inventario y método

Se analizaron sintácticamente, con Roslyn, los archivos incluidos por Compile en los siete proyectos de AOCR.sln. Se localizaron 645 archivos compilados, 1.109 tipos, 6.349 métodos, 508 acciones candidatas MVC, 782 métodos de prueba y 212 vistas Razor. Los proyectos archivados de .history y otros .csproj fuera de la solución figuran en el anexo de proyectos con su condición; no se suman como proyectos activos.

Las relaciones método-tabla y método-prueba automáticas son referencias textuales y se rotulan así. Los procedimientos troncales se contrastan manualmente con controladores, vistas y servicios; no se promueve una coincidencia de texto a cobertura ejecutada. Los catálogos omiten valores de configuración y datos de personas.

La consulta de metadatos PostgreSQL de solo lectura encontró 263 tablas y 9 vistas, 3.628 columnas y 638 restricciones, en PostgreSQL 16.10. El esquema se captura al 03/11; no se afirma que sea idéntico al esquema del día del commit. Se conserva separado del catálogo de migraciones. No se exportan registros de usuarios, catálogos operativos ni credenciales.

Pruebas ejecutadas en la selección segura del flujo: 246 correctas y 15 omitidas por requisitos de entorno. No representa la suite completa de 782 métodos. La compilación de vistas Razor termina con advertencia de dependencias de iText para un framework posterior; no constituye prueba de firma real, DB2, SMTP ni publicación IIS.

## Definición de estados de revisión

- Implementado: existe una ruta y lógica contrastables en este commit. La evidencia de prueba se indica por separado.
- Parcial: existe código, pero una contradicción, dependencia o ruta heredada impide afirmar el procedimiento completo.
- Pendiente: requisito identificado sin implementación completa o sin validación institucional de extremo a extremo.
- No localizado: no se encontró el elemento en el ámbito revisado; no se afirma su inexistencia en infraestructura externa.

Los nombres de elaboración, revisión, aprobación y códigos documentales oficiales requieren confirmación institucional. Los códigos de los manuales se proponen para revisión, sin simular aprobación.
