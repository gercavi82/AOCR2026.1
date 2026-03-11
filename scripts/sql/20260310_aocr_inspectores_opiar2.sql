-- AOCR - Persistencia de Técnico Responsable / Inspectores (OPINSPECTORES OPIAR2)
-- Fecha: 2026-03-10
-- Script idempotente

BEGIN;

-- =========================================================
-- 1) Solicitud AOCR: datos de técnico responsable desde AS400
-- =========================================================
ALTER TABLE public.aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS tecnico_responsable_cedula VARCHAR(20),
    ADD COLUMN IF NOT EXISTS tecnico_responsable_nombre VARCHAR(200),
    ADD COLUMN IF NOT EXISTS tecnico_responsable_tipo VARCHAR(10),
    ADD COLUMN IF NOT EXISTS inspector_apoyo_cedula VARCHAR(20),
    ADD COLUMN IF NOT EXISTS inspector_apoyo_nombre VARCHAR(200),
    ADD COLUMN IF NOT EXISTS inspector_apoyo_tipo VARCHAR(10);

CREATE INDEX IF NOT EXISTS idx_aocr_tbsolicitud_tecnico_cedula
    ON public.aocr_tbsolicitud (tecnico_responsable_cedula);

-- =========================================================
-- 2) Inspección: trazabilidad de principal y apoyo AS400
-- =========================================================
ALTER TABLE public.aocr_tbinspeccion
    ADD COLUMN IF NOT EXISTS inspector_principal_cedula VARCHAR(20),
    ADD COLUMN IF NOT EXISTS inspector_principal_nombre VARCHAR(200),
    ADD COLUMN IF NOT EXISTS inspector_principal_tipo VARCHAR(10),
    ADD COLUMN IF NOT EXISTS inspector_apoyo_cedula VARCHAR(20),
    ADD COLUMN IF NOT EXISTS inspector_apoyo_nombre VARCHAR(200),
    ADD COLUMN IF NOT EXISTS inspector_apoyo_tipo VARCHAR(10);

CREATE INDEX IF NOT EXISTS idx_aocr_tbinspeccion_insp_principal_cedula
    ON public.aocr_tbinspeccion (inspector_principal_cedula);

COMMIT;
