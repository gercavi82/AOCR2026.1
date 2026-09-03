-- =====================================================================
-- MIGRACIÓN ADITIVA E IDEMPOTENTE: AC-02 FECHAS DE INSPECCIÓN POR ESTACIÓN
-- FECHA: 2026-09-03
-- DESCRIPCIÓN:
-- Crea la tabla aditiva aocr_tbsolicitud_estacion para permitir que cada
-- estación (aeropuerto o base operativa) asociada a una solicitud AOCR
-- posea sus propias fechas independientes de inspección (inicio y fin),
-- con soporte de versionado, auditoría y compatibilidad histórica.
-- =====================================================================

DO $$
BEGIN
    -- 1. Crear tabla aditiva de estaciones por solicitud
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables 
        WHERE table_schema = 'public' 
          AND table_name = 'aocr_tbsolicitud_estacion'
    ) THEN
        CREATE TABLE public.aocr_tbsolicitud_estacion (
            id SERIAL PRIMARY KEY,
            solicitud_id INTEGER NOT NULL,
            estacion_codigo VARCHAR(20) NOT NULL,
            estacion_nombre VARCHAR(150) NOT NULL,
            fecha_inicio DATE NOT NULL,
            fecha_fin DATE NOT NULL,
            inspector_id INTEGER NULL,
            inspector_nombre VARCHAR(200) NULL,
            inspeccion_id INTEGER NULL,
            estado VARCHAR(50) NOT NULL DEFAULT 'SOLICITADA',
            version INTEGER NOT NULL DEFAULT 1,
            activo BOOLEAN NOT NULL DEFAULT TRUE,
            observacion TEXT NULL,
            creado_en TIMESTAMP NOT NULL DEFAULT NOW(),
            creado_por INTEGER NULL,
            actualizado_en TIMESTAMP NULL,
            actualizado_por INTEGER NULL,
            CONSTRAINT chk_fechas_estacion CHECK (fecha_fin >= fecha_inicio)
        );

        COMMENT ON TABLE public.aocr_tbsolicitud_estacion IS 'Estaciones operativas solicitadas y rangos de fechas de inspección independientes (AC-02)';
        COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.solicitud_id IS 'Identificador de la solicitud AOCR (aocr_tbsolicitud.codigo_solicitud)';
        COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.estacion_codigo IS 'Código OACI/IATA o identificador de la estación (ej. UIO, GYE, MEC, LTX)';
        COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.estacion_nombre IS 'Nombre descriptivo de la estación/aeropuerto';
        COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.fecha_inicio IS 'Fecha inicial programada o solicitada para la inspección de la estación';
        COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.fecha_fin IS 'Fecha final programada o solicitada para la inspección de la estación';
        COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.inspector_id IS 'ID del inspector asignado específicamente a la estación (si aplica)';
        COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.inspeccion_id IS 'ID de la inspección vinculada en aocr_tbinspeccion (si aplica)';
        COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.estado IS 'Estado de la estación en el flujo (SOLICITADA, PLANIFICADA, INSPECCIONADA, etc.)';
    END IF;

    -- 2. Índice por solicitud_id para optimizar consultas de expedientes
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE schemaname = 'public' 
          AND tablename = 'aocr_tbsolicitud_estacion' 
          AND indexname = 'idx_solicitud_estacion_solicitud'
    ) THEN
        CREATE INDEX idx_solicitud_estacion_solicitud 
        ON public.aocr_tbsolicitud_estacion (solicitud_id) 
        WHERE (activo = TRUE);
    END IF;

    -- 3. Índice único para evitar duplicados de la misma estación activa en una solicitud
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE schemaname = 'public' 
          AND tablename = 'aocr_tbsolicitud_estacion' 
          AND indexname = 'idx_solicitud_estacion_unicidad'
    ) THEN
        CREATE UNIQUE INDEX idx_solicitud_estacion_unicidad 
        ON public.aocr_tbsolicitud_estacion (solicitud_id, UPPER(estacion_codigo)) 
        WHERE (activo = TRUE);
    END IF;
END $$;
