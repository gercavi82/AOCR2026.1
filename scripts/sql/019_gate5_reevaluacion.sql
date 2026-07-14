BEGIN;

ALTER TABLE public.aocr_tbinforme_inspeccion
    ADD COLUMN IF NOT EXISTS codigo_informe_anterior INTEGER NULL,
    ADD COLUMN IF NOT EXISTS codigo_no_conformidad_origen INTEGER NULL,
    ADD COLUMN IF NOT EXISTS ciclo_evaluacion INTEGER NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS es_reevaluacion BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE public.aocr_tblv_operacional_eae
    ADD COLUMN IF NOT EXISTS codigo_lista_anterior INTEGER NULL,
    ADD COLUMN IF NOT EXISTS codigo_no_conformidad_origen INTEGER NULL,
    ADD COLUMN IF NOT EXISTS ciclo_evaluacion INTEGER NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS es_reevaluacion BOOLEAN NOT NULL DEFAULT FALSE;

DO $$ BEGIN
    ALTER TABLE public.aocr_tbinforme_inspeccion ADD CONSTRAINT fk_informe_ciclo_anterior
        FOREIGN KEY (codigo_informe_anterior) REFERENCES public.aocr_tbinforme_inspeccion(codigo_informe);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
    ALTER TABLE public.aocr_tbinforme_inspeccion ADD CONSTRAINT fk_informe_ciclo_nc
        FOREIGN KEY (codigo_no_conformidad_origen) REFERENCES public.aocr_tbnoconformidad(codigo_no_conformidad);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
    ALTER TABLE public.aocr_tblv_operacional_eae ADD CONSTRAINT fk_lv_ciclo_anterior
        FOREIGN KEY (codigo_lista_anterior) REFERENCES public.aocr_tblv_operacional_eae(codigo_lv);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
    ALTER TABLE public.aocr_tblv_operacional_eae ADD CONSTRAINT fk_lv_ciclo_nc
        FOREIGN KEY (codigo_no_conformidad_origen) REFERENCES public.aocr_tbnoconformidad(codigo_no_conformidad);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

ALTER TABLE public.aocr_tbinforme_inspeccion DROP CONSTRAINT IF EXISTS ck_informe_ciclo_positivo;
ALTER TABLE public.aocr_tbinforme_inspeccion ADD CONSTRAINT ck_informe_ciclo_positivo CHECK (ciclo_evaluacion > 0);
ALTER TABLE public.aocr_tblv_operacional_eae DROP CONSTRAINT IF EXISTS ck_lv_ciclo_positivo;
ALTER TABLE public.aocr_tblv_operacional_eae ADD CONSTRAINT ck_lv_ciclo_positivo CHECK (ciclo_evaluacion > 0);

CREATE UNIQUE INDEX IF NOT EXISTS ux_informe_reevaluacion_antecedente
    ON public.aocr_tbinforme_inspeccion(codigo_informe_anterior)
    WHERE codigo_informe_anterior IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_lv_reevaluacion_antecedente
    ON public.aocr_tblv_operacional_eae(codigo_lista_anterior)
    WHERE codigo_lista_anterior IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_informe_nc_ciclo
    ON public.aocr_tbinforme_inspeccion(codigo_no_conformidad_origen, ciclo_evaluacion);

COMMIT;
