BEGIN;

-- Los documentos finales tienen autoridades distintas. "firmado_por" se
-- conserva por compatibilidad y se migra como firmante de Condiciones/DCAV.
ALTER TABLE public.aocr_tbcertificado
    ADD COLUMN IF NOT EXISTS emitido_por VARCHAR(100);

ALTER TABLE public.aocr_tbcertificado
    ADD COLUMN IF NOT EXISTS aprobado_por VARCHAR(100);

UPDATE public.aocr_tbcertificado
SET emitido_por = NULLIF(BTRIM(firmado_por), '')
WHERE NULLIF(BTRIM(emitido_por), '') IS NULL
  AND NULLIF(BTRIM(firmado_por), '') IS NOT NULL;

COMMENT ON COLUMN public.aocr_tbcertificado.emitido_por IS
    'Nombre del Director de Certificacion Aeronautica y Vigilancia Continua para Condiciones y Limitaciones.';

COMMENT ON COLUMN public.aocr_tbcertificado.aprobado_por IS
    'Nombre del Director General de Aviacion Civil para el reconocimiento AOCR.';

COMMIT;

SELECT codigo_certificado,
       codigo_solicitud,
       emitido_por,
       aprobado_por
FROM public.aocr_tbcertificado
ORDER BY codigo_certificado DESC
LIMIT 20;
