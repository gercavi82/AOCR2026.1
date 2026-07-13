-- P0: endurecimiento del versionamiento de PDF oficiales.
-- No crea tablas ni columnas. Ejecutar primero en QA y conservar el resultado del preflight.
BEGIN;

-- Preflight visible: debe devolver cero filas.
SELECT codigo_inspeccion,
       CASE WHEN UPPER(TRIM(tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO'
            ELSE 'CONDICIONES_LIMITACIONES' END tipo_documento,
       version, COUNT(*) total,
       ARRAY_AGG(codigo_documento ORDER BY codigo_documento) ids
FROM public.aocr_tbdocumento_inspeccion
WHERE UPPER(TRIM(tipo_documento)) IN ('RECONOCIMIENTO','AOCR','CONDICIONES','CONDICIONES_LIMITACIONES')
GROUP BY codigo_inspeccion, CASE WHEN UPPER(TRIM(tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO' ELSE 'CONDICIONES_LIMITACIONES' END, version
HAVING COUNT(*) > 1;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM public.aocr_tbdocumento_inspeccion
        WHERE UPPER(TRIM(tipo_documento)) IN ('RECONOCIMIENTO','AOCR','CONDICIONES','CONDICIONES_LIMITACIONES')
        GROUP BY codigo_inspeccion, CASE WHEN UPPER(TRIM(tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO' ELSE 'CONDICIONES_LIMITACIONES' END, version HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION 'Existen versiones PDF duplicadas. Resolverlas sin borrar históricos antes de crear el índice.';
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_documento_pdf_oficial_version
ON public.aocr_tbdocumento_inspeccion
   (codigo_inspeccion, (CASE WHEN UPPER(TRIM(tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO' ELSE 'CONDICIONES_LIMITACIONES' END), version)
WHERE UPPER(TRIM(tipo_documento)) IN ('RECONOCIMIENTO','AOCR','CONDICIONES','CONDICIONES_LIMITACIONES');

CREATE INDEX IF NOT EXISTS ix_documento_pdf_oficial_origen
ON public.aocr_tbdocumento_inspeccion(codigo_documento_base, version DESC)
WHERE UPPER(TRIM(tipo_documento)) IN ('RECONOCIMIENTO','AOCR','CONDICIONES','CONDICIONES_LIMITACIONES');

-- Registros incompletos que requieren diagnóstico físico con /Diagnostico/ConsistenciaPdf.
SELECT codigo_documento,codigo_inspeccion,tipo_documento,version,ruta_archivo,hash_archivo,tamano_bytes
FROM public.aocr_tbdocumento_inspeccion
WHERE UPPER(TRIM(tipo_documento)) IN ('RECONOCIMIENTO','AOCR','CONDICIONES','CONDICIONES_LIMITACIONES')
  AND (NULLIF(TRIM(ruta_archivo),'') IS NULL OR NULLIF(TRIM(hash_archivo),'') IS NULL OR COALESCE(tamano_bytes,0)<=0);

COMMIT;
