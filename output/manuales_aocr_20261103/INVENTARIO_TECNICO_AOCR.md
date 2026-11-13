# Inventario técnico AOCR

Corte: 03/11/2026, commit `6d885a984bb875507366a824a290681450376d87`.

Se revisaron los siete proyectos de la solución: 645 archivos compilados, 1.109 tipos, 6.349 métodos, 508 acciones MVC candidatas, 212 vistas Razor y 782 métodos de prueba definidos. Los proyectos históricos encontrados fuera de la solución se clasifican sin sumarlos como capas activas.

La base de referencia contiene 263 tablas y 9 vistas, con 3.628 columnas y 638 restricciones. El diccionario distingue tablas y vistas y conserva los metadatos reales.

## Catálogos

Las cantidades siguientes son filas de inventario, no funcionalidades independientes ni pruebas aprobadas.

| Archivo en anexos | Filas |
|---|---:|
| `01_proyectos.csv` | 71 |
| `02_controladores_acciones.csv` | 508 |
| `03_servicios_interfaces.csv` | 1,277 |
| `04_dao_metodos.csv` | 1,311 |
| `05_modelos_viewmodels.csv` | 2,921 |
| `06_estados_constantes.csv` | 261 |
| `07_casos_prueba.csv` | 782 |
| `08_diccionario_datos.csv` | 3,628 |
| `09_relaciones_restricciones.csv` | 638 |
| `10_indices.csv` | 747 |
| `11_vistas_bd.csv` | 9 |
| `12_vistas_partiales.csv` | 212 |
| `13_javascript_css.csv` | 1,381 |
| `14_plantillas_pdf.csv` | 36 |
| `15_eventos_correos.csv` | 36 |
| `16_menu.csv` | 148 |
| `17_configuracion_sin_valores.csv` | 127 |
| `18_scripts_bd.csv` | 99 |
| `19_inventario_archivos.csv` | 645 |
| `20_matriz_trazabilidad.csv` | 29 |
| `21_matriz_roles.csv` | 7 |
| `22_permisos_roles_bd.csv` | 59 |
| `23_configuracion_archivos.csv` | 7 |
| `24_trabajos_integracion.csv` | 3 |
| `25_rutas_explicitas.csv` | 18 |
| `26_scripts_operacion.csv` | 255 |
| `27_documentos_generadores.csv` | 186 |
| `28_resultados_pruebas.csv` | 261 |

## Interpretación

Las coincidencias textuales de tablas, llamadas y pruebas se rotulan como tales: sirven para navegar a la fuente, sin acreditar por sí solas un grafo semántico, autorización efectiva o cobertura ejecutada. Las fichas de 29 procedimientos y la matriz de trazabilidad contrastan además los caminos principales.

Las rutas convencionales son candidatas a contrastar con RouteConfig. Los atributos Route se conservan como declaraciones; no se presume su registro. Los valores de configuración se omiten. Los candidatos a plantilla o generador se identifican por su criterio de búsqueda.

El inventario JSON amplía los contratos, propiedades, atributos, dependencias y ubicaciones. El esquema SQL es estructura de referencia, no un respaldo operativo completo. Consulte los manuales y el informe de validación antes de interpretar los catálogos como evidencia funcional.
