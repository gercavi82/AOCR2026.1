-- =====================================================================
-- FIX: chk_estado en aocr_or_orden
-- Causa: la constraint fue creada sin incluir 'ANULADA' (y posiblemente
--        'GENERADA'), por lo que CambiarEstado lanza 23514 al intentar
--        escribir esos valores.
-- Acción: ampliar la constraint con todos los estados válidos del sistema.
-- Ejecutar en pgAdmin / DBeaver contra la BD de AOCR.
-- =====================================================================

-- 1. Ver la definición actual (informativo, no cambia nada)
SELECT pg_get_constraintdef(c.oid) AS definicion_actual
FROM   pg_constraint c
JOIN   pg_class      t ON t.oid = c.conrelid
WHERE  t.relname = 'aocr_or_orden'
  AND  c.conname = 'chk_estado';

-- 2. Corregir la constraint
BEGIN;

ALTER TABLE aocr_or_orden
    DROP CONSTRAINT IF EXISTS chk_estado;

ALTER TABLE aocr_or_orden
    ADD CONSTRAINT chk_estado
    CHECK (estado IN (
        'BORRADOR',
        'GENERADA',
        'PENDIENTE',
        'COMPLETADA',
        'FACTURADA',
        'PAGADA',
        'ANULADA'
    ));

COMMIT;

-- 3. Verificación final
SELECT conname, pg_get_constraintdef(oid) AS nueva_definicion
FROM   pg_constraint
WHERE  conname = 'chk_estado'
  AND  conrelid = 'aocr_or_orden'::regclass;
