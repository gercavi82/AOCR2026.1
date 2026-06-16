-- Persistir código OACI de compañía en órdenes de recaudación (contexto RT multi-compañía).
ALTER TABLE aocr_or_orden
    ADD COLUMN IF NOT EXISTS compania_codigo VARCHAR(20);

COMMENT ON COLUMN aocr_or_orden.compania_codigo IS 'Código OACI de la compañía activa RT al crear la orden.';

CREATE INDEX IF NOT EXISTS idx_aocr_or_orden_compania_codigo
    ON aocr_or_orden (compania_codigo);

-- Backfill aproximado por nombre cuando exista relación RT↔compañía.
UPDATE aocr_or_orden o
SET compania_codigo = ucr.compania_codigo
FROM aocr_usuario_compania_rt ucr
WHERE o.compania_codigo IS NULL
  AND o.codigo_usuario = ucr.usuario_id
  AND ucr.activo = TRUE
  AND (
        UPPER(TRIM(o.compania)) = UPPER(TRIM(ucr.compania_nombre))
        OR (o.ruc_cedula IS NOT NULL AND ucr.usuoid IS NOT NULL AND TRIM(o.ruc_cedula) = TRIM(ucr.usuoid))
      );
