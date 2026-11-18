-- Permite nombres de concepto con listas de estaciones y lugares extensos.
-- Ejecutar en la base AOCR. No elimina ni recorta datos existentes.
BEGIN;
SET LOCAL lock_timeout = '5s';

ALTER TABLE public.aocr_or_orden_detalle
    ALTER COLUMN concepto_nombre TYPE text,
    ALTER COLUMN lugar_inspeccion TYPE text;

COMMIT;

-- Verificación: ambas columnas deben mostrar data_type = text.
SELECT column_name, data_type, character_maximum_length
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'aocr_or_orden_detalle'
  AND column_name IN ('concepto_nombre', 'lugar_inspeccion');
