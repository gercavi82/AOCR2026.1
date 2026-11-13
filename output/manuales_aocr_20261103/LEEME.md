# Entrega documental AOCR — 03/11/2026

Versión documental 1.0, para revisión técnica e institucional. Rama confirmada: `fix/cierre-flujo-aocr`. Commit: `6d885a984bb875507366a824a290681450376d87`.

## Archivos principales

- `Manual_Usuario_AOCR.docx` y `.pdf`: 46 páginas, siete roles y 29 procedimientos.
- `Manual_Tecnico_AOCR.docx` y `.pdf`: 45 páginas, arquitectura, contratos, persistencia, operación, instalación, recuperación y pruebas.
- `Flujograma_General_AOCR.drawio`: editable en diagrams.net; secuencia principal y devoluciones. La versión `.mmd` permite edición textual en Mermaid. Las condiciones concretas se precisan en los procedimientos.
- `Matriz_Trazabilidad_AOCR.csv`: relación de procedimientos, pantallas, acciones, servicios, DAO, tablas y pruebas.
- `Diccionario_Datos_AOCR.csv`: columnas del esquema consultado; 263 tablas y 9 vistas. Los comentarios no existentes no se sustituyen por significados inventados.
- `INVENTARIO_TECNICO_AOCR.md`: índice de los catálogos exhaustivos.
- `LEVANTAMIENTO_Y_CONTRADICCIONES.md` e `INFORMACION_PENDIENTE.md`: hallazgos, límites y confirmaciones requeridas.
- `INFORME_VALIDACION.md`: evidencia ejecutada y sus límites.
- `MANUAL_USUARIO_AOCR.md` y `MANUAL_TECNICO_AOCR.md`: fuentes editables complementarias; los Word contienen la maquetación final.

## Uso de los anexos

Abra los CSV como UTF-8, con separador coma y comillas dobles. Los contratos y listas extensas se entregan en CSV para permitir búsqueda y filtros sin reducirlos a tablas ilegibles en Word. Los JSON conservan metadatos estructurados. Los archivos `procedimientos.json` y `roles.json` respaldan la organización documental.

`anexos/diagramas` contiene el flujo principal, la versión con devoluciones y los siete recorridos por rol. Las imágenes PNG muestran las secuencias principales incluidas en los manuales; las devoluciones se desarrollan en los procedimientos y en el diagrama editable adicional.

`anexos/esquema_referencia_20261103.sql` es un esquema PostgreSQL sin datos, propietarios ni grants. No contiene cuentas iniciales ni semillas suficientes para operar desde cero. No debe aplicarse automáticamente sobre producción. La restauración estructural se verificó en una base local temporal y se detuvo esa instancia.

## Límites que deben conservarse

La documentación usa la base configurada, autorizada por el usuario, solo como referencia de lectura. No se exportaron registros de personas ni credenciales. La fecha del esquema no acredita que el servidor publicado utilice exactamente ese mismo corte.

Se usó la evidencia visual existente autorizada: una captura histórica. Faltan capturas autenticadas actuales de los siete roles, aprobación institucional y validaciones externas de firmas, SMTP, DB2 y entrega final. Las funciones parciales y contradicciones se identifican; esta entrega no las convierte en terminadas ni modifica la aplicación.

Los códigos documentales son propuestos. La autoría nominal, revisión, aprobación y vigencia de la marca deben confirmarse por la institución.

`MANIFIESTO_SHA256.csv` y `.json` permiten comprobar tamaño y huella de todos los archivos de la entrega, excepto los propios manifiestos.
