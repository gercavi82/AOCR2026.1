BEGIN;

ALTER TABLE public.aocr_tbdocumento
    ADD COLUMN IF NOT EXISTS nombre_original varchar(500);

ALTER TABLE public.aocr_tbdocumento
    ADD COLUMN IF NOT EXISTS nombre_visible varchar(500);

ALTER TABLE public.aocr_tbdocumento
    ADD COLUMN IF NOT EXISTS nombre_fisico varchar(500);

UPDATE public.aocr_tbdocumento
SET nombre_fisico = nombre_archivo
WHERE (nombre_fisico IS NULL OR btrim(nombre_fisico) = '')
  AND nombre_archivo IS NOT NULL
  AND btrim(nombre_archivo) <> '';

UPDATE public.aocr_tbdocumento
SET nombre_visible = CASE
    WHEN upper(coalesce(tipo_documento, '')) = 'COMPROBANTE_PAGO' THEN 'Comprobante_pago.pdf'
    WHEN upper(coalesce(tipo_documento, '')) = 'COPIA_AOC_VALIDA' THEN 'Copia_AOC_valida.pdf'
    WHEN upper(coalesce(tipo_documento, '')) = 'MANUAL_OPERACIONES' THEN 'Manual_operaciones.pdf'
    WHEN upper(coalesce(tipo_documento, '')) = 'CERTIFICADO_AERONAVEGABILIDAD' THEN 'Certificado_aeronavegabilidad.pdf'
    WHEN upper(coalesce(tipo_documento, '')) LIKE '%CERTIFICADO%RUIDO%' THEN 'Certificado_ruido_aeronaves.pdf'
    WHEN upper(coalesce(tipo_documento, '')) LIKE '%PERMISO%OPERACION%' THEN 'Permiso_operacion_CNAC.pdf'
    WHEN upper(coalesce(tipo_documento, '')) LIKE '%OPSPECS%' THEN 'OpSpecs_Especificaciones_operacionales.pdf'
    WHEN upper(coalesce(tipo_documento, '')) LIKE '%COPIA_CERTIFICADA%PODER%' THEN 'Copia_certificada_poder_representante.pdf'
    WHEN nullif(btrim(nombre_original), '') IS NOT NULL THEN btrim(nombre_original)
    WHEN nullif(btrim(nombre_archivo), '') IS NOT NULL
     AND nombre_archivo !~* '^[a-f0-9]{24,}\.[a-z0-9]+$' THEN btrim(nombre_archivo)
    ELSE 'Documento_cargado.pdf'
END
WHERE nombre_visible IS NULL
   OR btrim(nombre_visible) = ''
   OR nombre_visible ~* '^[a-f0-9]{24,}\.[a-z0-9]+$';

COMMIT;
