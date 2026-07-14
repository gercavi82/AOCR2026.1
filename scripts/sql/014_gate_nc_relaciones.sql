BEGIN;

ALTER TABLE public.aocr_tbnoconformidad ADD COLUMN IF NOT EXISTS codigo_nc_raiz INTEGER NULL;
ALTER TABLE public.aocr_tbnoconformidad ADD COLUMN IF NOT EXISTS codigo_solicitud_origen INTEGER NULL;
ALTER TABLE public.aocr_tbnoconformidad ADD COLUMN IF NOT EXISTS codigo_inspeccion_origen INTEGER NULL;
ALTER TABLE public.aocr_tbnoconformidad ADD COLUMN IF NOT EXISTS codigo_informe_origen INTEGER NULL;
ALTER TABLE public.aocr_tbnoconformidad ADD COLUMN IF NOT EXISTS codigo_solicitud_nueva INTEGER NULL;
ALTER TABLE public.aocr_tbnoconformidad ADD COLUMN IF NOT EXISTS codigo_inspeccion_nueva INTEGER NULL;
ALTER TABLE public.aocr_tbnoconformidad ADD COLUMN IF NOT EXISTS codigo_informe_cierre INTEGER NULL;
ALTER TABLE public.aocr_tbnoconformidad ADD COLUMN IF NOT EXISTS ciclo_evaluacion INTEGER NOT NULL DEFAULT 1;
ALTER TABLE public.aocr_tbnoconformidad ADD COLUMN IF NOT EXISTS fecha_cierre TIMESTAMP NULL;
ALTER TABLE public.aocr_tbnoconformidad ADD COLUMN IF NOT EXISTS usuario_cierre INTEGER NULL;
ALTER TABLE public.aocr_tbnoconformidad ADD COLUMN IF NOT EXISTS observacion_cierre TEXT NULL;
ALTER TABLE public.aocr_tbnoconformidad ADD COLUMN IF NOT EXISTS correlation_id VARCHAR(100) NULL;

UPDATE public.aocr_tbnoconformidad
SET codigo_solicitud_origen=COALESCE(codigo_solicitud_origen,codigo_solicitud),
    codigo_inspeccion_origen=COALESCE(codigo_inspeccion_origen,codigo_inspeccion),
    codigo_informe_origen=COALESCE(codigo_informe_origen,codigo_informe),
    ciclo_evaluacion=CASE WHEN ciclo_evaluacion IS NULL OR ciclo_evaluacion<1 THEN 1 ELSE ciclo_evaluacion END;

WITH raices AS (
    SELECT codigo_no_conformidad,
           CASE
             WHEN NULLIF(TRIM(numero_no_conformidad),'') IS NULL THEN codigo_no_conformidad
             ELSE MIN(codigo_no_conformidad) OVER (PARTITION BY UPPER(TRIM(numero_no_conformidad)))
           END AS raiz
    FROM public.aocr_tbnoconformidad
)
UPDATE public.aocr_tbnoconformidad nc
SET codigo_nc_raiz=r.raiz
FROM raices r
WHERE r.codigo_no_conformidad=nc.codigo_no_conformidad
  AND nc.codigo_nc_raiz IS NULL;

CREATE OR REPLACE FUNCTION public.aocr_fn_nc_asignar_raiz()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.codigo_nc_raiz IS NULL THEN
        NEW.codigo_nc_raiz := NEW.codigo_no_conformidad;
    END IF;
    RETURN NEW;
END $$;

DROP TRIGGER IF EXISTS trg_aocr_nc_asignar_raiz ON public.aocr_tbnoconformidad;
CREATE TRIGGER trg_aocr_nc_asignar_raiz
BEFORE INSERT ON public.aocr_tbnoconformidad
FOR EACH ROW EXECUTE FUNCTION public.aocr_fn_nc_asignar_raiz();

ALTER TABLE public.aocr_tbnoconformidad ALTER COLUMN codigo_nc_raiz SET NOT NULL;

-- Los registros legacy actuales contienen referencias de origen huérfanas. Estas FK se
-- retiran si una versión preliminar de la migración las instaló, porque impedirían actualizar
-- las NC existentes. Las columnas e índices se conservan para su reconciliación posterior.
ALTER TABLE public.aocr_tbnoconformidad DROP CONSTRAINT IF EXISTS fk_aocr_nc_solicitud_origen;
ALTER TABLE public.aocr_tbnoconformidad DROP CONSTRAINT IF EXISTS fk_aocr_nc_inspeccion_origen;
ALTER TABLE public.aocr_tbnoconformidad DROP CONSTRAINT IF EXISTS fk_aocr_nc_informe_origen;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_aocr_nc_tipo_ruta') THEN
        ALTER TABLE public.aocr_tbnoconformidad ADD CONSTRAINT ck_aocr_nc_tipo_ruta
            CHECK (UPPER(tipo_ruta) IN ('CON_INSPECCION','SIN_INSPECCION')) NOT VALID;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_aocr_nc_version_positiva') THEN
        ALTER TABLE public.aocr_tbnoconformidad ADD CONSTRAINT ck_aocr_nc_version_positiva
            CHECK (version>0 AND ciclo_evaluacion>0) NOT VALID;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_aocr_nc_raiz') THEN
        ALTER TABLE public.aocr_tbnoconformidad ADD CONSTRAINT fk_aocr_nc_raiz
            FOREIGN KEY(codigo_nc_raiz) REFERENCES public.aocr_tbnoconformidad(codigo_no_conformidad) NOT VALID;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_aocr_nc_solicitud_nueva') THEN
        ALTER TABLE public.aocr_tbnoconformidad ADD CONSTRAINT fk_aocr_nc_solicitud_nueva
            FOREIGN KEY(codigo_solicitud_nueva) REFERENCES public.aocr_tbsolicitud(codigo_solicitud) NOT VALID;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_aocr_nc_inspeccion_nueva') THEN
        ALTER TABLE public.aocr_tbnoconformidad ADD CONSTRAINT fk_aocr_nc_inspeccion_nueva
            FOREIGN KEY(codigo_inspeccion_nueva) REFERENCES public.aocr_tbinspeccion(codigo_inspeccion) NOT VALID;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_aocr_nc_informe_cierre') THEN
        ALTER TABLE public.aocr_tbnoconformidad ADD CONSTRAINT fk_aocr_nc_informe_cierre
            FOREIGN KEY(codigo_informe_cierre) REFERENCES public.aocr_tbinforme_inspeccion(codigo_informe) NOT VALID;
    END IF;
END $$;

ALTER TABLE public.aocr_tbnoconformidad VALIDATE CONSTRAINT ck_aocr_nc_tipo_ruta;
ALTER TABLE public.aocr_tbnoconformidad VALIDATE CONSTRAINT ck_aocr_nc_version_positiva;
ALTER TABLE public.aocr_tbnoconformidad VALIDATE CONSTRAINT fk_aocr_nc_raiz;

CREATE UNIQUE INDEX IF NOT EXISTS ux_aocr_nc_raiz_version
    ON public.aocr_tbnoconformidad(codigo_nc_raiz,version);
CREATE UNIQUE INDEX IF NOT EXISTS ux_aocr_nc_numero_version
    ON public.aocr_tbnoconformidad(UPPER(TRIM(numero_no_conformidad)),version)
    WHERE NULLIF(TRIM(numero_no_conformidad),'') IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_aocr_nc_solicitud_activa_por_raiz
    ON public.aocr_tbnoconformidad(codigo_nc_raiz)
    WHERE codigo_solicitud_nueva IS NOT NULL AND fecha_cierre IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_aocr_nc_solicitud_nueva
    ON public.aocr_tbnoconformidad(codigo_solicitud_nueva)
    WHERE codigo_solicitud_nueva IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_aocr_nc_correlation
    ON public.aocr_tbnoconformidad(correlation_id)
    WHERE NULLIF(TRIM(correlation_id),'') IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_aocr_nc_relaciones_origen
    ON public.aocr_tbnoconformidad(codigo_solicitud_origen,codigo_inspeccion_origen,codigo_informe_origen);
CREATE INDEX IF NOT EXISTS ix_aocr_nc_relaciones_reevaluacion
    ON public.aocr_tbnoconformidad(codigo_solicitud_nueva,codigo_inspeccion_nueva,codigo_informe_cierre);

COMMIT;
