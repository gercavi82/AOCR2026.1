-- -----------------------------------------------------------------------------
-- MIGRACIÓN DE NUMERACIÓN Y CORRELATIVO ÚNICO DE ORDEN AOCR
-- -----------------------------------------------------------------------------

BEGIN;

-- 1. Crear tabla de correlativo orden por año
CREATE TABLE IF NOT EXISTS public.aocr_correlativo_orden (
    id SERIAL PRIMARY KEY,
    anio INTEGER NOT NULL,
    ultimo_numero INTEGER NOT NULL DEFAULT 0,
    fecha_actualizacion TIMESTAMP WITHOUT TIME ZONE DEFAULT NOW(),
    CONSTRAINT uq_aocr_correlativo_orden_anio UNIQUE (anio)
);

-- 2. Crear índice único para la tabla de correlativos por año
CREATE UNIQUE INDEX IF NOT EXISTS ux_aocr_correlativo_orden_anio
ON public.aocr_correlativo_orden(anio);

-- 3. Inicializar correlativo con el valor máximo existente en la base de datos
INSERT INTO public.aocr_correlativo_orden (anio, ultimo_numero)
SELECT 
    CAST(regexp_replace(numero_orden, '^DGAC-OR-(\d{4})-AOCR.*$', '\1') AS INTEGER) AS anio,
    MAX(CAST(regexp_replace(numero_orden, '.*AOCR0*', '') AS INTEGER)) AS ultimo
FROM public.aocr_or_orden
WHERE numero_orden ~ '^DGAC-OR-\d{4}-AOCR\d+$'
GROUP BY CAST(regexp_replace(numero_orden, '^DGAC-OR-(\d{4})-AOCR.*$', '\1') AS INTEGER)
ON CONFLICT (anio) DO UPDATE SET 
    ultimo_numero = GREATEST(aocr_correlativo_orden.ultimo_numero, EXCLUDED.ultimo_numero),
    fecha_actualizacion = NOW();

-- 4. Índice único estricto en aocr_or_orden para evitar duplicidad de número
CREATE UNIQUE INDEX IF NOT EXISTS ux_aocr_or_orden_numero_orden
ON public.aocr_or_orden(numero_orden)
WHERE numero_orden IS NOT NULL AND TRIM(numero_orden) != '';

COMMIT;
