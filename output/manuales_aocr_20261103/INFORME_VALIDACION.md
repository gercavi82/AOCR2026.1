# Informe de validación de la entrega documental

Fecha: 03/11/2026. Rama `fix/cierre-flujo-aocr`, commit `6d885a984bb875507366a824a290681450376d87`. Estado: para revisión técnica e institucional.

## Código y pruebas

- El árbol estaba limpio al iniciar; fetch del remoto confirmó el mismo commit en la rama remota, sin diferencia de commits.
- Compilación de aplicación y vistas Razor en Release: correcta. Compilación de pruebas: correcta. Persisten advertencias de dependencias, incluidas las de iText; el build no acredita compatibilidad de firma real en todo host.
- Selección de pruebas de flujo, permisos, estaciones y documentos: 261 resultados del adaptador, 246 correctos y 15 omitidos. Los resultados individuales se entregan en `anexos/28_resultados_pruebas.csv`.
- El inventario estático identifica 782 métodos de prueba; no se declara ejecutada la suite completa. Algunas pruebas requieren entorno específico.

## Base de referencia

Se consultaron únicamente metadatos y asignaciones de permisos por rol, sin exportar usuarios ni credenciales. Origen PostgreSQL 16.10; esquema de 263 tablas y 9 vistas. Se exportó SQL de estructura sin datos, propietarios ni grants.

Restauración estructural correcta en instancia temporal local PostgreSQL 18.1: 263 tablas restauradas. No es una prueba de instalación operativa, semillas de negocio, compatibilidad de todos los motores ni restauración de datos reales. La instancia temporal se detuvo al terminar. La evidencia resumida se conserva en `anexos/validacion/resultado-esquema.txt`.

## Word y PDF

Ambos Word se abrieron con Microsoft Word 15, se actualizaron índices y números de página y se exportaron a PDF. Se renderizaron las páginas para revisión visual: 46 del manual de usuario y 45 del técnico. Se revisaron las 91 páginas, incluyendo diagramas, tablas, márgenes, pies y continuidad. Se corrigió la cabecera aislada de la última tabla del manual de usuario y se revisaron sus páginas afectadas.

Las comprobaciones geométricas no detectaron páginas sin contenido ni palabras fuera del área segura. Las figuras tienen texto alternativo; las tablas conservan cabeceras. La auditoría automatizada de accesibilidad de ambos DOCX informó 0 hallazgos altos, medios y bajos. Esto no certifica accesibilidad WCAG del aplicativo ni reemplaza una evaluación con tecnologías de asistencia.

Se revisaron los nuevos archivos de texto del paquete con patrones de secretos: no se detectaron coincidencias pendientes. Los catálogos de configuración contienen claves y ubicaciones sin valores. El inventario de pruebas no reproduce argumentos DataRow potencialmente sensibles.

## Límites de validación

No se publicaron cambios en IIS, no se modificaron decisiones de expedientes reales, no se enviaron correos de prueba, no se hicieron firmas institucionales ni escrituras DB2. No se acredita resuelta la incidencia real de recepción del paquete por el Coordinador. Las capturas actuales por rol, aprobación institucional, parámetros de infraestructura y demás pendientes se mantienen en `INFORMACION_PENDIENTE.md`.

Las evidencias automatizadas documentales se incluyen en `anexos/validacion`. Los pendientes funcionales del corte siguen visibles en ambos manuales y en el levantamiento de contradicciones.
