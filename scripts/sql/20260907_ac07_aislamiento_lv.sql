-- AC-07. Ejecutar antes de publicar la aplicación. No copia respuestas ni firmas.
BEGIN;
LOCK TABLE public.aocr_tblv_operacional_eae IN ACCESS EXCLUSIVE MODE;
ALTER TABLE public.aocr_tblv_operacional_eae
    ADD COLUMN IF NOT EXISTS solicitud_id integer,
    ADD COLUMN IF NOT EXISTS estacion_id integer,
    ADD COLUMN IF NOT EXISTS tipo_lista varchar(50) NOT NULL DEFAULT 'EAE',
    ADD COLUMN IF NOT EXISTS vigente boolean NOT NULL DEFAULT true;

-- El índice anterior confundía inspecciones diferentes de un mismo trámite.
DROP INDEX IF EXISTS public.uq_aocr_tblv_eae_vigente;
UPDATE public.aocr_tblv_operacional_eae lv
SET solicitud_id = i.codigo_solicitud
FROM public.aocr_tbinspeccion i
WHERE i.codigo_inspeccion = lv.codigo_inspeccion AND lv.solicitud_id IS NULL;

-- Solo asignar un histórico si hay una única estación compatible y todavía
-- no existe una LV específica para ella. Lo ambiguo conserva su identidad original.
WITH candidatas AS (
    SELECT i.codigo_inspeccion, min(e.id) AS estacion_id
    FROM public.aocr_tbinspeccion i
    JOIN public.aocr_tbsolicitud_estacion e ON e.solicitud_id = i.codigo_solicitud
        AND e.activo AND (e.inspeccion_id IS NULL OR e.inspeccion_id = i.codigo_inspeccion)
    GROUP BY i.codigo_inspeccion HAVING count(*) = 1
)
UPDATE public.aocr_tblv_operacional_eae lv SET estacion_id = c.estacion_id
FROM candidatas c
WHERE lv.codigo_inspeccion = c.codigo_inspeccion AND lv.estacion_id IS NULL
  AND NOT EXISTS (SELECT 1 FROM public.aocr_tblv_operacional_eae otra
    WHERE otra.codigo_inspeccion = lv.codigo_inspeccion AND otra.estacion_id = c.estacion_id);

-- Mantener historial completo; solo la última versión queda vigente por ámbito.
WITH versiones AS (
    SELECT codigo_lv, row_number() OVER (
        PARTITION BY codigo_inspeccion, estacion_id, tipo_lista
        ORDER BY version DESC, codigo_lv DESC) AS posicion
    FROM public.aocr_tblv_operacional_eae WHERE vigente
)
UPDATE public.aocr_tblv_operacional_eae lv SET vigente = false
FROM versiones v WHERE lv.codigo_lv = v.codigo_lv AND v.posicion > 1;

CREATE UNIQUE INDEX IF NOT EXISTS uq_aocr_tblv_eae_inspeccion_estacion_vigente
    ON public.aocr_tblv_operacional_eae(codigo_inspeccion, COALESCE(estacion_id, 0), tipo_lista)
    WHERE vigente;
CREATE INDEX IF NOT EXISTS ix_aocr_tblv_eae_solicitud_estacion
    ON public.aocr_tblv_operacional_eae(solicitud_id, estacion_id, codigo_inspeccion, version DESC);

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lv_estacion_ac07'
        AND conrelid = 'public.aocr_tblv_operacional_eae'::regclass) THEN
        ALTER TABLE public.aocr_tblv_operacional_eae ADD CONSTRAINT fk_lv_estacion_ac07
            FOREIGN KEY (estacion_id) REFERENCES public.aocr_tbsolicitud_estacion(id) NOT VALID;
    END IF;
END $$;
COMMIT;

-- Revisar estos históricos antes de asignarlos manualmente: nunca clonarlos.
SELECT lv.codigo_lv, lv.codigo_inspeccion, lv.solicitud_id, lv.version, lv.firmado_tecnico
FROM public.aocr_tblv_operacional_eae lv
WHERE lv.estacion_id IS NULL AND EXISTS (
    SELECT 1 FROM public.aocr_tbsolicitud_estacion e WHERE e.solicitud_id = lv.solicitud_id AND e.activo);
