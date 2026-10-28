-- Dependencia: 20261027_datos_documentales_certificado.sql.
-- Corrige exclusivamente numeracion interna de borradores. No genera PDF,
-- no modifica archivos ni numeros de expedientes enviados o firmados.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='30s';
SELECT pg_advisory_xact_lock(12920261027);
LOCK TABLE public.aocr_tbsolicitud, public.aocr_tbcertificado,
    public.aocr_tbdocumento_generado, public.aocr_tbfirma_documento IN SHARE ROW EXCLUSIVE MODE;

CREATE TEMP TABLE aocr_numero_protegido ON COMMIT DROP AS
SELECT codigo_solicitud FROM public.aocr_tbfirma_documento
UNION SELECT codigo_solicitud FROM public.aocr_tbdocumento_generado
 WHERE bloqueado OR NULLIF(ruta_pdf_firmado,'') IS NOT NULL OR NULLIF(hash_pdf_firmado,'') IS NOT NULL
UNION SELECT codigo_solicitud FROM public.aocr_tbcertificado c
 WHERE NULLIF(to_jsonb(c)->>'firmado_por','') IS NOT NULL
 OR UPPER(COALESCE(to_jsonb(c)->>'estado','')) ~ '(FIRMAD|FINALIZAD|LEGALIZAD|ENTREGAD)'
UNION SELECT codigo_solicitud FROM public.aocr_tbsolicitud
 WHERE UPPER(COALESCE(estado,'')) ~ '(FIRMAD|FINALIZAD|LEGALIZAD|ENTREGAD|DOCUMENTOS_FINALES_COORDINADOR|DOCUMENTOS_FINALES_EN_FIRMA)';

-- Incluye el circuito heredado de CL si esta instalado, sin crearlo ni reescribirlo.
DO $$ BEGIN
 IF to_regclass('public.aocr_tbcondiciones_limitaciones') IS NOT NULL THEN
  LOCK TABLE public.aocr_tbcondiciones_limitaciones IN SHARE ROW EXCLUSIVE MODE;
  INSERT INTO aocr_numero_protegido
  SELECT codigo_solicitud FROM public.aocr_tbcondiciones_limitaciones
  WHERE NULLIF(ruta_pdf_firmado,'') IS NOT NULL OR fecha_firma_dircav IS NOT NULL
     OR estado NOT IN ('CL_BORRADOR','CL_DEVUELTO_INSPECTOR');
 END IF;
END $$;

CREATE TEMP TABLE aocr_numero_origen ON COMMIT DROP AS
WITH datos AS (
 SELECT codigo_solicitud,numero_aocr,codigo_oaci,ruc,
        SUBSTRING(TRIM(numero_solicitud) FROM '(?i)AOCR-?([0-9]+)$') consecutivo
 FROM public.aocr_tbsolicitud
 WHERE tipo_solicitud=1 AND UPPER(TRIM(codigo_oaci)) ~ '^[A-Z0-9]{3}$'
   AND (numero_aocr ~ '^DGAC-129-[A-Z0-9]{3}-[0-9]+$' OR numero_aocr=numero_solicitud
        OR numero_aocr ~ '^AOCR-[0-9]{4}-[0-9]+$')
), numeros AS (
 SELECT *, 'DGAC-129-'||UPPER(TRIM(codigo_oaci))||'-'||
        LPAD(consecutivo,GREATEST(3,LENGTH(consecutivo)),'0') nuevo
 FROM datos WHERE consecutivo IS NOT NULL AND LENGTH(LTRIM(consecutivo,'0'))>0
)
SELECT * FROM numeros WHERE numero_aocr IS DISTINCT FROM nuevo;

CREATE TEMP TABLE aocr_numero_correccion ON COMMIT DROP AS
SELECT o.codigo_solicitud origen_id,s.codigo_solicitud,s.numero_aocr anterior,o.nuevo
FROM aocr_numero_origen o JOIN public.aocr_tbsolicitud s ON
 s.codigo_solicitud=o.codigo_solicitud OR
 (s.tipo_solicitud IN (2,3) AND s.numero_aocr=o.numero_aocr
  AND UPPER(TRIM(s.codigo_oaci))=UPPER(TRIM(o.codigo_oaci))
  AND COALESCE(TRIM(s.ruc),'')=COALESCE(TRIM(o.ruc),''));

-- Si una renovacion/modificacion ya fue enviada o firmada, se conserva todo
-- el grupo original. Nunca dejar referencias de una misma emision desalineadas.
DELETE FROM aocr_numero_correccion c WHERE c.origen_id IN (
 SELECT x.origen_id FROM aocr_numero_correccion x JOIN aocr_numero_protegido p USING(codigo_solicitud));
DO $$ BEGIN
 IF EXISTS(SELECT codigo_solicitud FROM aocr_numero_correccion GROUP BY codigo_solicitud HAVING COUNT(*)>1)
 OR EXISTS(SELECT 1 FROM aocr_numero_correccion c JOIN public.aocr_tbsolicitud s
 ON s.tipo_solicitud=1 AND s.numero_aocr=c.nuevo
 WHERE s.codigo_solicitud<>c.origen_id) THEN
  RAISE EXCEPTION 'Referencia original ambigua o numero AOCR ocupado. Revise antes de regularizar.';
 END IF;
END $$;

-- La excepcion administrativa esta limitada a esta transaccion y a las filas
-- comprobadas. Auditoria e historial permanecen activos. Un error revierte todo.
ALTER TABLE public.aocr_tbsolicitud DISABLE TRIGGER trg_aocr_numero_original_inmutable;
UPDATE public.aocr_tbsolicitud s SET numero_aocr=c.nuevo,updated_at=NOW()
FROM aocr_numero_correccion c WHERE s.codigo_solicitud=c.codigo_solicitud;
ALTER TABLE public.aocr_tbsolicitud ENABLE TRIGGER trg_aocr_numero_original_inmutable;
UPDATE public.aocr_tbcertificado s SET numero_certificado=c.nuevo,ruta_pdf=NULL
FROM aocr_numero_correccion c WHERE s.codigo_solicitud=c.codigo_solicitud;
UPDATE public.aocr_tbdocumento_generado d
SET vigente=FALSE,estado='VERSION_ANTERIOR',version_concurrencia=version_concurrencia+1
FROM aocr_numero_correccion c WHERE d.codigo_solicitud=c.codigo_solicitud AND d.vigente=TRUE;
DO $$ BEGIN
 IF to_regclass('public.aocr_tbcondiciones_limitaciones') IS NOT NULL THEN
  UPDATE public.aocr_tbcondiciones_limitaciones d SET vigente=FALSE
  FROM aocr_numero_correccion c WHERE d.codigo_solicitud=c.codigo_solicitud
    AND d.vigente=TRUE AND d.estado IN ('CL_BORRADOR','CL_DEVUELTO_INSPECTOR');
 END IF;
END $$;
-- Los PDF anteriores conservan su numero y archivo: quedan como historial.
SELECT codigo_solicitud,origen_id,anterior,nuevo,'REGENERAR_PDF' accion
FROM aocr_numero_correccion ORDER BY codigo_solicitud;
COMMIT;
