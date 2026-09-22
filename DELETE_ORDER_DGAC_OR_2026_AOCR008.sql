-- ============================================================================
-- ELIMINACIÓN SEGURA DE ORDEN: DGAC-OR-2026-AOCR008
-- ============================================================================
-- Fecha: 2026-09-22
-- Ambiente: Desarrollo (DEV)
-- Estado: LISTO PARA EJECUTAR
-- ============================================================================

-- PASO 1: VERIFICAR QUE LA ORDEN EXISTE (LECTURA SOLO)
-- ============================================================================
SELECT 
    id as "ID_Solicitud",
    numero_solicitud as "Número_Solicitud",
    numero_orden as "Número_Orden",
    fecha_solicitud as "Fecha_Solicitud",
    estado as "Estado",
    (SELECT COUNT(*) FROM aocr_tbsolicitud_estacion_inspeccion WHERE solicitud_id = aocr_tbsolicitud.id) as "Estaciones_Vinculadas"
FROM aocr_tbsolicitud
WHERE numero_orden = 'DGAC-OR-2026-AOCR008'
   OR numero_solicitud = 'DGAC-OR-2026-AOCR008';

-- RESULTADO ESPERADO:
-- Si ves una fila: ✅ La orden existe y está lista para eliminar
-- Si no ves filas: ❌ La orden no existe o ya fue eliminada

-- ============================================================================
-- PASO 2: ELIMINAR DATOS DEPENDIENTES
-- ============================================================================
-- Primero eliminamos todas las estaciones vinculadas a esta solicitud

DELETE FROM aocr_tbsolicitud_estacion_inspeccion
WHERE solicitud_id IN (
    SELECT id FROM aocr_tbsolicitud
    WHERE numero_orden = 'DGAC-OR-2026-AOCR008'
       OR numero_solicitud = 'DGAC-OR-2026-AOCR008'
);

-- RESULTADO ESPERADO:
-- "DELETE X" donde X es el número de estaciones eliminadas (puede ser 0 si no hay)

-- ============================================================================
-- PASO 3: ELIMINAR LA ORDEN PRINCIPAL
-- ============================================================================
-- Ahora eliminamos la solicitud AOCR misma

DELETE FROM aocr_tbsolicitud
WHERE numero_orden = 'DGAC-OR-2026-AOCR008'
   OR numero_solicitud = 'DGAC-OR-2026-AOCR008';

-- RESULTADO ESPERADO:
-- "DELETE 1" confirmando que la orden fue eliminada

-- ============================================================================
-- PASO 4: VERIFICAR QUE LA ELIMINACIÓN FUE EXITOSA (LECTURA SOLO)
-- ============================================================================
-- Confirmar que la orden ya no existe

SELECT COUNT(*) as "Órdenes_Restantes"
FROM aocr_tbsolicitud
WHERE numero_orden = 'DGAC-OR-2026-AOCR008'
   OR numero_solicitud = 'DGAC-OR-2026-AOCR008';

-- RESULTADO ESPERADO:
-- "Órdenes_Restantes: 0" confirmando eliminación exitosa

-- ============================================================================
-- ROLLBACK (Si necesitas deshacer):
-- ============================================================================
-- ROLLBACK;
-- ^ Descomenta esta línea Y ejecuta TODO el script nuevamente si necesitas
--   deshacer los cambios ANTES de hacer COMMIT

-- ============================================================================
-- CONFIRMACIÓN FINAL
-- ============================================================================
-- Una vez verificado que todo es correcto, ejecuta:
-- COMMIT;
