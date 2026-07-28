BEGIN;

UPDATE aocr_tbparametro p
SET descripcion = b.descripcion_anterior,
    updatedat = NOW(),
    updatedby = 'SYSTEM_ROLLBACK_TEXTO'
FROM aocr_tbparametro_descripcion_bak_20260727 b
WHERE p.clave = b.clave;

DROP TABLE IF EXISTS aocr_tbparametro_descripcion_bak_20260727;

COMMIT;
