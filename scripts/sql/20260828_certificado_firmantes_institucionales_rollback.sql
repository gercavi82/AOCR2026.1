BEGIN;

-- Recupera en el campo legacy el nombre de DCAV antes de retirar las columnas.
UPDATE public.aocr_tbcertificado
SET firmado_por = COALESCE(NULLIF(BTRIM(emitido_por), ''), firmado_por)
WHERE NULLIF(BTRIM(emitido_por), '') IS NOT NULL;

ALTER TABLE public.aocr_tbcertificado DROP COLUMN IF EXISTS aprobado_por;
ALTER TABLE public.aocr_tbcertificado DROP COLUMN IF EXISTS emitido_por;

COMMIT;
