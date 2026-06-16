-- Extiende chk_estado_documento para subsanación documental v2 (estados institucionales).
-- Idempotente: elimina y recrea el CHECK con legacy + v2.

BEGIN;

DO $$
BEGIN
    IF to_regclass('public.aocr_tbdocumento') IS NULL THEN
        RAISE NOTICE 'Tabla aocr_tbdocumento no existe; se omite migración.';
        RETURN;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint c
        JOIN pg_class t ON t.oid = c.conrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        WHERE n.nspname = 'public'
          AND t.relname = 'aocr_tbdocumento'
          AND c.conname = 'chk_estado_documento'
    ) THEN
        ALTER TABLE public.aocr_tbdocumento DROP CONSTRAINT chk_estado_documento;
    END IF;

    ALTER TABLE public.aocr_tbdocumento
        ADD CONSTRAINT chk_estado_documento
        CHECK (
            estado IS NULL
            OR estado IN (
                -- Legacy UI / histórico
                'Cargado',
                U&'En Revisi\00F3n',
                'Aprobado',
                'Rechazado',
                'Subsanado',
                -- Institucional v2 (subsanación documental)
                'PENDIENTE_REVISION',
                'PENDIENTE_REVISION_SUBSANACION',
                'ACEPTADO',
                'APROBADO',
                'OBSERVADO',
                'DEVUELTO',
                'DEVUELTO_INSPECTOR',
                'PENDIENTE_SUBSANACION',
                'SUBSANADO_RT',
                'SUBSANADO',
                'SUBSANACION',
                'EN_REVISION_INSPECTOR',
                'RECHAZADO',
                'BLOQUEADO',
                'VERSION_ANTERIOR',
                'CARGADO'
            )
        );
END $$;

COMMIT;
