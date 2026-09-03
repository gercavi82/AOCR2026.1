-- =========================================================================================
-- SCRIPT DE MIGRACIÓN: 20260903_ac07_lista_verificacion_por_estacion.sql
-- OBJETIVO: AC-07 - Lista de Verificación independiente por inspección o estación.
-- Permite que cada estación solicitada (AC-02) o reinspección posea su propia LV
-- identificable, persistente, inmutable tras la firma y con versionado histórico.
-- IDEMPOTENTE, ADITIVO Y SEGURO.
-- =========================================================================================

DO $$
BEGIN
    -- 1. Añadir columnas aditivas a aocr_tblv_operacional_eae si no existen
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'aocr_tblv_operacional_eae') THEN
        
        -- solicitud_id
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tblv_operacional_eae' AND column_name = 'solicitud_id') THEN
            ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN solicitud_id INTEGER NULL;
        END IF;

        -- estacion_id
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tblv_operacional_eae' AND column_name = 'estacion_id') THEN
            ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN estacion_id INTEGER NULL;
        END IF;

        -- tipo_lista
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tblv_operacional_eae' AND column_name = 'tipo_lista') THEN
            ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN tipo_lista VARCHAR(50) NOT NULL DEFAULT 'EAE';
        END IF;

        -- vigente
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tblv_operacional_eae' AND column_name = 'vigente') THEN
            ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN vigente BOOLEAN NOT NULL DEFAULT TRUE;
        END IF;

    END IF;
END $$;

-- 2. Migración controlada de datos históricos:
-- Actualizar solicitud_id a partir de aocr_tbinspeccion si está nulo
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'aocr_tblv_operacional_eae' AND column_name = 'solicitud_id')
       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'aocr_tbinspeccion') THEN
        
        UPDATE public.aocr_tblv_operacional_eae lv
           SET solicitud_id = i.codigo_solicitud
          FROM public.aocr_tbinspeccion i
         WHERE lv.codigo_inspeccion = i.codigo_inspeccion
           AND (lv.solicitud_id IS NULL OR lv.solicitud_id = 0);
    END IF;
END $$;

-- 3. Índices de optimización y búsqueda por estación
CREATE INDEX IF NOT EXISTS ix_aocr_tblv_eae_solicitud_estacion
    ON public.aocr_tblv_operacional_eae(solicitud_id, estacion_id, codigo_inspeccion, version DESC);

CREATE INDEX IF NOT EXISTS ix_aocr_tblv_eae_vigente_lookup
    ON public.aocr_tblv_operacional_eae(solicitud_id, COALESCE(estacion_id, 0), tipo_lista)
    WHERE vigente = TRUE;

-- 4. Restricción / Índice único parcial de unicidad por versión vigente:
-- Garantiza que para una misma solicitud, estación y tipo de lista, exista exactamente una LV vigente
DROP INDEX IF EXISTS uq_aocr_tblv_eae_vigente;
CREATE UNIQUE INDEX uq_aocr_tblv_eae_vigente
    ON public.aocr_tblv_operacional_eae(solicitud_id, COALESCE(estacion_id, 0), tipo_lista)
    WHERE vigente = TRUE;
