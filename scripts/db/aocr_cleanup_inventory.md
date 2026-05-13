# AOCR - Inventario para limpieza segura de base de datos

Base inspeccionada: `dgac_des`

Objetivo: limpiar datos operativos y transaccionales sin tocar usuarios, credenciales, roles, permisos, menus, parametros, catalogos base ni configuracion institucional.

Estado: solo analisis y preparacion de scripts. No se ejecutaron `DELETE`, `TRUNCATE` ni `ALTER SEQUENCE` destructivos.

## Tablas encontradas

```text
aocr_asignacion_rt
aocr_audit_trail
aocr_declaracion_historial
aocr_declaracion_tmp
aocr_idempotency_key
aocr_or_concepto
aocr_or_orden
aocr_or_orden_detalle
aocr_orden_recaudacion
aocr_sync_log
aocr_tb_factura_pago
aocr_tb_sync_log
aocr_tbaeronave
aocr_tbaeronave_solicitud
aocr_tbauditoria
aocr_tbcertificado
aocr_tbchecklist
aocr_tbchecklist_item
aocr_tbchecklist_solicitud
aocr_tbdocumento
aocr_tbdocumento_inspeccion
aocr_tbdocumento_subsanacion
aocr_tbfirma_documento
aocr_tbfirma_posicion_documento
aocr_tbhallazgo
aocr_tbhistorial_documental
aocr_tbhistorial_estado
aocr_tbhistorial_estado_inspeccion
aocr_tbinforme
aocr_tbinforme_inspeccion
aocr_tbinspeccion
aocr_tblog
aocr_tblv_operacional_eae
aocr_tbnotificacion
aocr_tbobservacion
aocr_tbpago
aocr_tbparametro
aocr_tbrevision_documental
aocr_tbsesiones
aocr_tbsolicitud
aocr_tbsubsanacion
aocr_tbviatico
aocr_usuario_compania_rt
aocr_usuario_interno_rt
aocr_usuario_transferencia
aocr_usuario_transferencia_detalle
auditoria_seguridad
conceptos
contribuyentes
detalles_orden
email_attachment
email_queue
fr3
fr3_detalle
fr3_detalle_pg
fr3_pg
historial_estados_orden
menu
ordenes_recaudacion
pagos
parametros
permisos
rol
seguridad_permiso
seguridad_rol_permiso
submenu
sync_log
sync_state
usuario
usuario_as400
usuario_as400_adicional
usuario_backup_eliminados
usuario_rol
```

## Dependencias clave detectadas

- `aocr_tbsolicitud` es padre de `aocr_tbdocumento`, `aocr_tbhistorial_estado`, `aocr_tbinspeccion`, `aocr_tbcertificado`, `aocr_tbobservacion`, `aocr_tbpago`, `aocr_tbsubsanacion`, `aocr_tbviatico` y `email_queue`.
- `aocr_tbinspeccion` es padre de `aocr_tbchecklist` y `aocr_tbinforme` por clave foranea; otras tablas relacionadas sin FK explicita se limpian antes por orden manual (`aocr_tbinforme_inspeccion`, `aocr_tblv_operacional_eae`, `aocr_tbfirma_*`, `aocr_tbdocumento_inspeccion`, `aocr_tbhallazgo`).
- `aocr_or_orden` es padre de `aocr_or_orden_detalle`, `aocr_tb_factura_pago` y `email_queue`.
- `ordenes_recaudacion` es padre de `detalles_orden`, `pagos` e `historial_estados_orden`.
- `email_queue` es padre de `email_attachment`.

## Clasificacion

### A. Preservar

| Tabla | Registros antes | Motivo |
|---|---:|---|
| aocr_or_concepto | 18 | Conceptos base de ordenes de recaudacion. |
| aocr_tbchecklist_item | 13 | Catalogo maestro de items checklist. |
| aocr_tbparametro | 15 | Parametros institucionales del sistema. |
| aocr_tbsesiones | 0 | Sesiones/autenticacion; no tocar por politica de acceso. |
| aocr_usuario_compania_rt | 13 | Relacion usuario externo-compania; necesaria para operar usuarios RT. |
| aocr_usuario_interno_rt | 2 | Catalogo de usuarios internos RT. |
| conceptos | 5 | Catalogo legacy de conceptos. |
| contribuyentes | 1 | Maestro/parametro de contribuyentes usado por OR legacy. |
| menu | 1 | Menu base. |
| parametros | 11 | Parametros legacy/base. |
| permisos | 2 | Seguridad/menus. |
| rol | 13 | Roles del sistema. |
| seguridad_permiso | 8 | Permisos base de seguridad. |
| seguridad_rol_permiso | 26 | Mapeo rol-permiso base. |
| submenu | 2 | Submenus base. |
| sync_state | 1 | Estado/configuracion de sincronizacion. |
| usuario | 8 | Usuarios internos/externos y credenciales. |
| usuario_as400 | 1 | Integracion/identidad AS400. |
| usuario_as400_adicional | 1 | Datos complementarios de identidad AS400. |
| usuario_backup_eliminados | 0 | Respaldo historico de usuarios; no tocar. |
| usuario_rol | 22 | Asignacion de roles a usuarios. |

### B. Limpiar

| Tabla | Registros antes | Motivo |
|---|---:|---|
| aocr_asignacion_rt | 24 | Asignaciones operativas ligadas a solicitudes. |
| aocr_audit_trail | 57 | Auditoria funcional de procesos. |
| aocr_idempotency_key | 38 | Estado tecnico/idempotencia de procesos operativos. |
| aocr_or_orden | 90 | Ordenes de recaudacion operativas vigentes. |
| aocr_or_orden_detalle | 90 | Detalle de ordenes operativas. |
| aocr_orden_recaudacion | 3 | Tabla legacy/alternativa de ordenes operativas. |
| aocr_sync_log | 57 | Log operativo de sincronizacion. |
| aocr_tb_factura_pago | 24 | Comprobantes/facturas de ordenes. |
| aocr_tb_sync_log | 33 | Log tecnico de sincronizacion OR. |
| aocr_tbaeronave | 0 | Datos operativos por solicitud. |
| aocr_tbaeronave_solicitud | 26 | Aeronaves ligadas a solicitudes AOCR. |
| aocr_tbauditoria | 277 | Auditoria funcional de aplicacion. |
| aocr_tbcertificado | 2 | Certificados generados. |
| aocr_tbchecklist | 0 | Checklist ligado a inspeccion. |
| aocr_tbchecklist_solicitud | 0 | Respuestas/checklist ligado a solicitud. |
| aocr_tbdocumento | 65 | Documentos subidos del expediente. |
| aocr_tbdocumento_inspeccion | 0 | Documentos del flujo de inspeccion. |
| aocr_tbdocumento_subsanacion | 0 | Documentos de subsanacion. |
| aocr_tbfirma_documento | 62 | Firmas de documentos generados. |
| aocr_tbfirma_posicion_documento | 19 | Posiciones de firma por solicitud/documento operativo. |
| aocr_tbhallazgo | 0 | Hallazgos de inspeccion. |
| aocr_tbhistorial_documental | 0 | Historial documental operativo. |
| aocr_tbhistorial_estado | 220 | Historial de estados de solicitud. |
| aocr_tbhistorial_estado_inspeccion | 208 | Historial de estados de inspeccion. |
| aocr_tbinforme | 0 | Informe legacy ligado a inspeccion. |
| aocr_tbinforme_inspeccion | 66 | Informes tecnicos de inspeccion. |
| aocr_tbinspeccion | 27 | Inspecciones operativas. |
| aocr_tblog | 1547 | Log funcional/aplicacion. |
| aocr_tblv_operacional_eae | 23 | Respuestas LV/EAE. |
| aocr_tbnotificacion | 58 | Notificaciones operativas. |
| aocr_tbobservacion | 0 | Observaciones de expediente. |
| aocr_tbpago | 83 | Pagos/comprobantes ligados a solicitudes. |
| aocr_tbrevision_documental | 0 | Revision documental operativa. |
| aocr_tbsolicitud | 204 | Solicitudes AOCR operativas. |
| aocr_tbsubsanacion | 0 | Subsanaciones operativas. |
| aocr_tbviatico | 0 | Viaticos operativos. |
| detalles_orden | 0 | Detalle de ordenes legacy. |
| email_attachment | 6 | Adjuntos de cola de correo. |
| email_queue | 236 | Cola de correos operativos. |
| fr3 | 0 | FR3 operativo/legacy. |
| fr3_detalle | 0 | Detalle de FR3. |
| fr3_detalle_pg | 0 | Detalle FR3 en PostgreSQL. |
| fr3_pg | 0 | FR3 PostgreSQL. |
| historial_estados_orden | 0 | Historial de estados de ordenes legacy. |
| ordenes_recaudacion | 0 | Ordenes legacy. |
| pagos | 0 | Pagos legacy. |
| sync_log | 0 | Log legacy de sincronizacion. |

### C. Revisar manualmente

| Tabla | Registros antes | Motivo |
|---|---:|---|
| aocr_declaracion_historial | 15 | Evidencia/historial de declaraciones de usuarios RT; puede afectar trazabilidad de onboarding. |
| aocr_declaracion_tmp | 0 | Cola temporal de declaraciones; segura para limpiar, pero asociada a onboarding de usuarios. |
| aocr_usuario_transferencia | 7 | Transferencias administrativas entre usuarios; no rompe login pero pertenece al modulo de usuarios. |
| aocr_usuario_transferencia_detalle | 51 | Detalle de transferencias administrativas entre usuarios. |
| auditoria_seguridad | 16 | Auditoria de seguridad; no necesaria para operar, pero sensible por cumplimiento. |

## Conteos criticos a preservar

- `usuario`: 8
- `aocr_usuario_interno_rt`: 2
- `aocr_usuario_compania_rt`: 13
- `rol`: 13
- `permisos`: 2
- `seguridad_permiso`: 8
- `seguridad_rol_permiso`: 26
- `menu`: 1
- `submenu`: 2
- `aocr_tbparametro`: 15
- `parametros`: 11
- `aocr_or_concepto`: 18
- `conceptos`: 5

## Archivos entregados

- `scripts/db/backup_aocr_before_cleanup.ps1`
- `scripts/db/aocr_operational_cleanup.sql`
- `scripts/db/aocr_reset_operational_sequences.sql`
- `scripts/db/aocr_post_cleanup_verify.sql`

## Observaciones finales

- La limpieza propuesta evita `TRUNCATE CASCADE` y usa `DELETE` ordenado por dependencias.
- El script principal termina en `ROLLBACK` por seguridad. Para ejecutar de verdad, debe revisarse el conteo posterior y cambiar a `COMMIT` manualmente.
- No se incluyeron tablas ni secuencias de usuarios, roles, permisos, menus, parametros, conceptos ni catalogos base.
- La confirmacion funcional de login, menus y creacion de nuevos procesos queda pendiente hasta ejecutar la limpieza en un entorno respaldado y correr validaciones de aplicacion.