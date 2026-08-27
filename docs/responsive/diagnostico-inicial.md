# Diagnóstico responsive inicial AOCR

Fecha de corte: 2026-08-27

## Inventario técnico

- 197 vistas Razor, incluidas 33 vistas compartidas.
- Layout institucional principal: `Views/Shared/_LayoutAOCR.cshtml`.
- Layouts de impresión separados: `_PdfLayoutDGAC.cshtml` y `_PdfLayoutDGACAocr.cshtml`; no deben hacerse fluidos porque conservan el formato oficial.
- Bootstrap 5.3.0 y AdminLTE 3.2 declarados por el layout principal.
- DataTables 1.13.4 y Responsive 2.4.1; 22 vistas contienen inicialización o integración DataTables.
- Select2 se carga desde los bundles locales.
- Chart.js se utiliza en dashboards y reportes.
- 395 archivos CSS (incluidas dependencias y mapas), 36 archivos JavaScript y una capa AOCR existente en `aocr-responsive.css`.
- 65 vistas contienen estilos Razor inline. Deben migrarse gradualmente, priorizando vistas operativas; los documentos PDF se mantienen aislados.
- 86 vistas contienen tablas (132 elementos detectados); solo 56 elementos tenían un contenedor responsive explícito al inicio del diagnóstico.
- 24 vistas contienen modales.

## Matriz inicial de problemas

| Vista o componente | Problema | Resolución afectada | Prioridad | Solución propuesta |
|---|---|---:|---|---|
| Layout AOCR | Viewport sin `viewport-fit=cover`; áreas seguras no contempladas | Móvil/tablet | Alta | Viewport estándar, variables y espaciado con `env(safe-area-inset-*)` |
| Sidebar compartido | Off-canvas sin ciclo completo de foco | <= 991.98 px | Crítica | Focus trap, Escape, backdrop, foco inicial y retorno al disparador |
| Tablas Razor | 76 tablas sin contenedor explícito | <= 767.98 px | Crítica | Encapsulado idempotente en región desplazable; no ocultar columnas |
| Contenido AJAX | Tablas y controles insertados después de cargar no se adaptan | Todas | Alta | Mejora idempotente mediante `MutationObserver` limitado al contenido AOCR |
| Formularios | Controles menores a 44 px y fuente móvil variable | <= 767.98 px | Alta | Tamaño táctil de 44 px y 16 px en campos de captura |
| Select2 | Ancho calculado puede superar su columna | Móvil/tablet | Alta | `width: 100%` y recálculo al cambiar orientación |
| DataTables y gráficos | No todos recalculan al rotar | Móvil/tablet horizontal | Alta | Evento `aocr:viewportchange` y recálculo DataTables con debounce |
| Modales | Algunos usan altura basada en `100vh` y diálogos extensos | Móvil | Alta | Límite con altura dinámica y scroll interno |
| Estilos compartidos | La capa heredada contiene selectores genéricos (`body`, `.row`, `.btn`) | Todas | Media | Nuevas reglas encapsuladas bajo `body.aocr-body`; migración progresiva |
| Vistas con CSS inline | 65 vistas dificultan consistencia y regresión | Todas | Media | Extraer por módulo empezando por Inspección, Solicitud y Coordinación |
| Acciones con iconos | Algunos controles dependen del `title` | Táctil/lector | Alta | Propagar nombre accesible cuando no existe texto visible |
| PDFs oficiales | Formato fijo A4 requerido | Móvil | No aplica al documento | Mantener impresión; adaptar solo visor, zoom y descarga |

## Estrategia de breakpoints

Se conserva la escala de Bootstrap 5: 575.98, 767.98, 991.98, 1199.98 y 1599.98 px. La base institucional usa componentes fluidos y reglas encapsuladas en `.aocr-*`. Las tablas complejas mantienen toda la información y desplazan solo su región. Las vistas PDF permanecen fuera de la adaptación documental.

## Riesgos y siguientes lotes

1. Inspección/Detalle y SolicitudAOCR/Detalle concentran la mayor cantidad de CSS inline y anchos mínimos; requieren pruebas funcionales con datos reales.
2. Las bandejas con muchas acciones necesitan adaptación semántica por fila, no solo scroll.
3. Las pruebas por rol requieren sesiones válidas y expedientes en cada estado del flujo.
4. Safari móvil y Chrome Android deben validarse en dispositivos o entorno autorizado; la emulación de viewport no sustituye esas pruebas.

