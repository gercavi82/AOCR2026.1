# Procedimiento de Despliegue, Cutover y Rollback FR3 (FASE 8)

Este documento contiene el procedimiento oficial (ejecutable por humanos) para activar el nuevo flujo FR3 transaccional (Outbox) sin duplicar registros y conservando la capacidad de volver al flujo antiguo (Legacy) de ser necesario. 

> [!CAUTION]
> Durante la ejecución del despliegue, está estrictamente prohibido eliminar el flujo Legacy. El cambio de modo debe hacerse únicamente a nivel de configuración (`FR3_PROCESSING_MODE`).

---

## 1. Fase de Pre-Despliegue (Predeployment)

| Actividad | Responsable | Estado |
| :--- | :--- | :--- |
| **Definir Responsables**: Identificar al Ingeniero de Despliegue (DevOps), DBA DB2, DBA PostgreSQL y Líder QA. | Liderazgo | `[ ]` |
| **Definir Ventana**: Establecer ventana de mantenimiento (Recomendado: horario de nula operación financiera). | Liderazgo | `[ ]` |
| **Respaldo PostgreSQL**: Realizar volcado (dump) completo de la base de datos PostgreSQL de producción. | DBA Postgres | `[ ]` |
| **Validación de Restauración**: Comprobar que el volcado es funcional y restaurable en ambiente aislado. | DBA Postgres | `[ ]` |
| **Estado de Journaling DB2**: Verificar confirmación del DBA AS400 sobre soporte `STRJRNPF` en OPCAR5, OPCAR6 y OPSARC. | DBA AS400 | `[ ]` |
| **Configuración**: Asegurar que las variables `FR3_PROCESSING_MODE=Legacy`, `FR3_TRANSACTION_REQUIRED` y parámetros de reintento estén en el `web.config` u orígenes de configuración. | DevOps | `[ ]` |
| **Health Checks**: Confirmar conectividad hacia BD y ping del AS400. | DevOps | `[ ]` |
| **Criterios GO/NO-GO**: Revisar checklist. Si falla un ítem crítico, **NO-GO**. | Liderazgo | `[ ]` |
| **Plan de Comunicación**: Avisar a operaciones financieras sobre la ventana de inactividad de aprobaciones. | PM | `[ ]` |

### Consultas No Destructivas de Verificación (Pre-Despliegue)
Verificar órdenes atascadas o pendientes de procesar:
```sql
-- Órdenes "atascadas" sin FR3
SELECT orden_id, numero_factura FROM aocr_tb_factura_pago 
WHERE fr3_estado IN ('PENDIENTE_FR3', 'ERROR_FINAL');

-- Órdenes completadas sin secuencial (para reconciliación manual futura)
SELECT id_orden FROM aocr_or_orden WHERE estado = 'FACTURADA';
```

---

## 2. Fase de Despliegue y Cutover (Deployment)

Esta fase implica la actualización del código binario pero **manteniendo el flujo Legacy activo** hasta estabilizar.

1. `[ ]` **Publicar Código en Legacy**: Desplegar los nuevos binarios y frontend manteniendo la configuración `FR3_PROCESSING_MODE = Legacy`.
2. `[ ]` **Ejecutar Migraciones Aditivas**: Aplicar scripts SQL aditivos en Postgres (creación de tabla outbox, triggers, nuevas columnas, sin borrar/renombrar nada).
3. `[ ]` **Validar Sitio y Rutas**: QA verifica que las rutas base bajo `/aocr`, los permisos y los logs locales no presenten errores 500 y que la vista financiera cargue.
4. `[ ]` **Validar Conexión DB2**: Consultar en el log si el `HealthCheckController` o la validación inicial de BD es exitosa.
5. `[ ]` **Procesar Orden Controlada**: Ejecutar una orden financiera de bajo impacto. El sitio debe aprobar usando el sistema antiguo.

### Procedimiento de Cutover a Outbox

6. `[ ]` **Pausar Aprobaciones**: Notificar a tesorería/recaudación detener momentáneamente aprobaciones de pagos.
7. `[ ]` **Detener Escritor Legacy**: Deshabilitar temporalmente las peticiones AJAX a `AprobarYEnviarAS400` desde IIS o balanceador (opcional) para evitar colisiones en la transición.
8. `[ ]` **Confirmar cero operaciones en curso**:
   ```sql
   -- No debe haber órdenes en estados intermedios vulnerables
   SELECT COUNT(*) FROM aocr_tb_factura_pago WHERE fr3_estado = 'EN_PROCESO';
   ```
9. `[ ]` **Reconciliar**: Ejecutar manualmente o esperar ciclo programado de `MirrorReadService.SincronizarFr3DesdeEspejo()` para emparejar todo FR3 pendiente entre AS400 y Postgres.
10. `[ ]` **Activar Outbox**: Modificar la configuración productiva (ej. `web.config` o Environment) cambiando `FR3_PROCESSING_MODE` de `Legacy` a `Outbox`.
11. `[ ]` **Iniciar Worker**: Si el worker FR3 (Hangfire/Quartz/BackgroundService) está pausado, inicializarlo.
12. `[ ]` **Reactivar Aprobaciones**: Dar luz verde a recaudación para proceder.

---

## 3. Validación Posterior al Cutover

QA y Operaciones deberán monitorear la primera hora de actividad bajo Outbox revisando los siguientes rubros:

- `[ ]` **Pendientes**: `aocr_fr3_outbox` no debe acumular registros en `PENDIENTE` más allá del ciclo de vida del worker.
- `[ ]` **Errores**: Vigilar el archivo de logs por alertas `[FR3_SYNC]` o `ERROR_FINAL`.
- `[ ]` **Tiempo de Proceso**: Verificar que la aprobación en UI responda de manera instantánea, delegando la latencia al Worker.
- `[ ]` **Duplicados / Secuenciales**: El AS400 no debe arrojar violaciones de Primary Key ni duplicar FR3 en reportes de la misma solicitud.
- `[ ]` **Consistencia Cruzada**:
  - `OPCAR5` y `OPCAR6` deben poseer correspondencia 1 a muchos correcta.
  - `OPSARC` debe actualizar saldo.
  - `factura_pago` y `aocr_or_orden` deben pasar a `COMPLETADA`.
- `[ ]` **Correo y Auditoría**: El cliente final recibe el correo, y `sync_log` registra la pista.

---

## 4. Plan de Rollback (Manejo de Incidentes)

Si la validación posterior presenta incidentes severos o colapsos que superan el SLA (Acuerdo de Nivel de Servicio) de la ventana de soporte, se debe ejecutar el Rollback inmediato a Legacy.

> [!IMPORTANT]
> **No borrar eventos ni historial** de la tabla outbox ni facturas durante el rollback, pues sirven de auditoría.

1. `[ ]` **Pausar Aprobaciones**: Detener uso del módulo financiero en UI.
2. `[ ]` **Detener Worker**: Matar el proceso/job que lee del Outbox (`Fr3OutboxWorkerDAO`) para evitar que interfiera en pleno rollback.
3. `[ ]` **Resolver Eventos EN_PROCESO**: Identificar si un registro quedó trabado.
   ```sql
   SELECT id, event_key, locked_at FROM aocr_fr3_outbox WHERE status = 'EN_PROCESO';
   ```
4. `[ ]` **Reconciliar AS400**: Correr `MirrorReadService` para que cualquier transacción DB2 viva se empareje a PostgreSQL antes de desconectar.
5. `[ ]` **Cambiar a Legacy**: Cambiar `FR3_PROCESSING_MODE` de `Outbox` a `Legacy` en configuración y reiniciar aplicación (pool de IIS).
6. `[ ]` **Reactivar y Validar**: Aprobar una orden y constatar que el flujo transacciona de la manera síncrona antigua exitosamente.

---

## 5. Formalidad de Despliegue (Firmas)

| Rol | Nombre del Autorizador | Firma / Aprobación (Evidencia) |
| :--- | :--- | :--- |
| **Líder QA** | ____________________ | `[ ]` Aprobado |
| **DevOps / Infraestructura** | ____________________ | `[ ]` Aprobado |
| **DBA AS400** | ____________________ | `[ ]` Aprobado |
| **DBA PostgreSQL** | ____________________ | `[ ]` Aprobado |
| **Responsable de Operaciones**| ____________________ | `[ ]` Aprobado |

**Manejo de Incidentes (Guardia):**
En caso de falla post-cutover o SQL7008 imprevisible durante producción, contactar inmediatamente a Nivel 3. Se adjunta evidencia del test report (`FR3_TEST_REPORT.md`).
