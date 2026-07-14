BEGIN;

ALTER TABLE public.aocr_tbdocumento_subsanacion
    ALTER COLUMN codigo_subsanacion DROP NOT NULL,
    ADD COLUMN IF NOT EXISTS codigo_no_conformidad integer,
    ADD COLUMN IF NOT EXISTS codigo_documento_origen integer,
    ADD COLUMN IF NOT EXISTS codigo_documento_nueva_version integer,
    ADD COLUMN IF NOT EXISTS version_anterior integer,
    ADD COLUMN IF NOT EXISTS version_nueva integer,
    ADD COLUMN IF NOT EXISTS observacion_origen varchar(2000),
    ADD COLUMN IF NOT EXISTS hash_sha256 varchar(64),
    ADD COLUMN IF NOT EXISTS correlation_id varchar(100);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_docsub_nc_gate2') THEN
        ALTER TABLE public.aocr_tbdocumento_subsanacion
            ADD CONSTRAINT fk_docsub_nc_gate2 FOREIGN KEY (codigo_no_conformidad)
            REFERENCES public.aocr_tbnoconformidad(codigo_no_conformidad) ON DELETE RESTRICT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_docsub_origen_gate2') THEN
        ALTER TABLE public.aocr_tbdocumento_subsanacion
            ADD CONSTRAINT fk_docsub_origen_gate2 FOREIGN KEY (codigo_documento_origen)
            REFERENCES public.aocr_tbdocumento(codigo_documento) ON DELETE RESTRICT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_docsub_nueva_gate2') THEN
        ALTER TABLE public.aocr_tbdocumento_subsanacion
            ADD CONSTRAINT fk_docsub_nueva_gate2 FOREIGN KEY (codigo_documento_nueva_version)
            REFERENCES public.aocr_tbdocumento(codigo_documento) ON DELETE RESTRICT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='chk_docsub_fuente_gate2') THEN
        ALTER TABLE public.aocr_tbdocumento_subsanacion ADD CONSTRAINT chk_docsub_fuente_gate2 CHECK
        (codigo_subsanacion IS NOT NULL OR
         (codigo_no_conformidad IS NOT NULL AND codigo_documento_origen IS NOT NULL AND
          codigo_documento_nueva_version IS NOT NULL AND version_anterior > 0 AND version_nueva=version_anterior+1 AND
          hash_sha256 ~ '^[0-9a-f]{64}$'));
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_docsub_nueva_gate2
    ON public.aocr_tbdocumento_subsanacion(codigo_documento_nueva_version)
    WHERE codigo_documento_nueva_version IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_docsub_nc_gate2
    ON public.aocr_tbdocumento_subsanacion(codigo_no_conformidad, fecha_carga DESC)
    WHERE codigo_no_conformidad IS NOT NULL;

COMMENT ON COLUMN public.aocr_tbdocumento_subsanacion.codigo_no_conformidad IS
    'NC SIN_INSPECCION que habilita la sustitucion individual.';
COMMENT ON COLUMN public.aocr_tbdocumento_subsanacion.codigo_documento_origen IS
    'Version observada conservada de forma inmutable.';
COMMENT ON COLUMN public.aocr_tbdocumento_subsanacion.codigo_documento_nueva_version IS
    'Nueva version pendiente de revision del inspector.';

COMMIT;
