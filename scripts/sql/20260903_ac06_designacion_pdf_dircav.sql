-- =========================================================================================
-- SCRIPT DE MIGRACIÓN: 20260903_ac06_designacion_pdf_dircav.sql
-- OBJETIVO: AC-06 - Columnas e índices para el PDF oficial de designación de inspectores
-- firmado institucionalmente por la Autoridad DIRCAV.
-- IDEMPOTENTE, ADITIVO Y SEGURO.
-- =========================================================================================

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'aocr_tbdesignacion_inspector') THEN
        
        -- ruta_pdf
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tbdesignacion_inspector' AND column_name = 'ruta_pdf') THEN
            ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN ruta_pdf VARCHAR(500) NULL;
        END IF;

        -- ruta_documento_firmado
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tbdesignacion_inspector' AND column_name = 'ruta_documento_firmado') THEN
            ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN ruta_documento_firmado VARCHAR(500) NULL;
        END IF;

        -- hash_documento
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tbdesignacion_inspector' AND column_name = 'hash_documento') THEN
            ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN hash_documento VARCHAR(256) NULL;
        END IF;

        -- firmado
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tbdesignacion_inspector' AND column_name = 'firmado') THEN
            ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN firmado BOOLEAN NOT NULL DEFAULT FALSE;
        END IF;

        -- usuario_firma
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tbdesignacion_inspector' AND column_name = 'usuario_firma') THEN
            ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN usuario_firma VARCHAR(200) NULL;
        END IF;

        -- tamanio_bytes
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tbdesignacion_inspector' AND column_name = 'tamanio_bytes') THEN
            ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN tamanio_bytes BIGINT NULL;
        END IF;

        -- mime_type
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tbdesignacion_inspector' AND column_name = 'mime_type') THEN
            ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN mime_type VARCHAR(100) NOT NULL DEFAULT 'application/pdf';
        END IF;

    END IF;
END $$;

-- Índice para acelerar búsquedas de designaciones firmadas y vigentes
CREATE INDEX IF NOT EXISTS ix_aocr_designacion_firmado
    ON public.aocr_tbdesignacion_inspector (solicitud_id, firmado, vigente);
