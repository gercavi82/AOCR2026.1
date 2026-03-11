-- AOCR - Ajustes base para flujo BPMN (AOCR + Inspecciones)
-- Fecha: 2026-03-10
-- Script idempotente

BEGIN;

-- =========================================================
-- 1) Campos de soporte para inspecciones (viaticos/pago/resultado)
-- =========================================================
ALTER TABLE public.aocr_tbinspeccion
    ADD COLUMN IF NOT EXISTS viaticos_requeridos BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS viaticos_monto NUMERIC(12,2),
    ADD COLUMN IF NOT EXISTS pago_viaticos_validado BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS fecha_pago_viaticos TIMESTAMP NULL,
    ADD COLUMN IF NOT EXISTS estado_documental VARCHAR(50),
    ADD COLUMN IF NOT EXISTS resultado_evaluacion VARCHAR(50);

CREATE INDEX IF NOT EXISTS idx_aocr_tbinspeccion_estado
    ON public.aocr_tbinspeccion (estado);

CREATE INDEX IF NOT EXISTS idx_aocr_tbinspeccion_codigo_solicitud
    ON public.aocr_tbinspeccion (codigo_solicitud);

-- =========================================================
-- 2) Constraint de estado de inspeccion (compatibilidad + BPMN)
-- Nota: en algunos ambientes el nombre existente es chk_estado_inspeccion.
-- =========================================================
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname IN ('chk_aocr_tbinspeccion_estado', 'chk_estado_inspeccion')
          AND conrelid = 'public.aocr_tbinspeccion'::regclass
    ) THEN
        ALTER TABLE public.aocr_tbinspeccion
            DROP CONSTRAINT IF EXISTS chk_aocr_tbinspeccion_estado;
        ALTER TABLE public.aocr_tbinspeccion
            DROP CONSTRAINT IF EXISTS chk_estado_inspeccion;
    END IF;

    ALTER TABLE public.aocr_tbinspeccion
        ADD CONSTRAINT chk_estado_inspeccion
        CHECK (
            estado IS NULL OR estado IN (
                -- Core tecnico
                'CREADA',
                'PROGRAMADA',
                'EN_CURSO',
                'APLAZADA',
                'FINALIZADA',
                'APROBADA',
                'RECHAZADA',
                'CANCELADA',
                'CERRADA',
                -- Estados BPMN extendidos de inspeccion
                'SOLICITUD_INSPECCION_CREADA',
                'VERIFICACION_SOLICITUD',
                'ACEPTADA',
                'OBSERVADA',
                'SUBSANADA',
                'VIATICOS_REQUERIDOS',
                'PAGO_VALIDADO',
                'EN_INSPECCION',
                'INFORME_ELABORADO',
                'RESULTADO_SATISFACTORIO',
                'RESULTADO_NO_SATISFACTORIO',
                'OBSERVACION_DOCUMENTAL'
            )
        );
END $$;

-- =========================================================
-- 3) Constraint de estado de solicitud AOCR (BPMN + legacy)
-- =========================================================
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'chk_aocr_tbsolicitud_estado'
          AND conrelid = 'public.aocr_tbsolicitud'::regclass
    ) THEN
        ALTER TABLE public.aocr_tbsolicitud
            DROP CONSTRAINT chk_aocr_tbsolicitud_estado;
    END IF;

    ALTER TABLE public.aocr_tbsolicitud
        ADD CONSTRAINT chk_aocr_tbsolicitud_estado
        CHECK (
            estado IS NULL OR estado IN (
                -- Legacy
                'Pendiente',
                'En Revision',
                'Documentacion Completa',
                'Pago Pendiente',
                'Pago Validado',
                'Inspeccion Programada',
                'Inspeccion Realizada',
                'Aprobada',
                'Rechazada',
                'Certificado Emitido',
                'Anulada',
                -- BPMN AOCR
                'Solicitud Creada',
                'Documentacion Pendiente',
                'Observada',
                'Subsanada',
                'Aceptacion Documental',
                'En Inspeccion',
                'AOCR En Elaboracion',
                'AOCR En Revision',
                'AOCR Validado',
                'AOCR Legalizado',
                'AOCR Emitido/Recibido'
            )
        );
END $$;

COMMIT;
