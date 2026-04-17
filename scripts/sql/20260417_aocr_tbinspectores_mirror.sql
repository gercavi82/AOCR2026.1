-- AOCR - Tabla espejo de inspectores AS400 en PostgreSQL
-- Fecha: 2026-04-17
-- Propósito: Almacenar localmente los inspectores de OPIAR2 (AS400/DB2)
--            para consultas independientes del AS400 y gestión local.
-- Cómo sincronizar: SyncAdmin/RunInspectoresRt (requiere rol Administrador)

BEGIN;

CREATE TABLE IF NOT EXISTS public.aocr_tbinspectores (
    id              SERIAL          PRIMARY KEY,
    cedula          VARCHAR(20)     NOT NULL,
    nombre_completo VARCHAR(200),
    estado          VARCHAR(5),     -- 'AC' = activo
    tipo            VARCHAR(10),    -- 'OPS', 'AIR'
    created_at      TIMESTAMP       NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMP       NOT NULL DEFAULT NOW()
);

-- Unicidad por cédula (normalizada)
CREATE UNIQUE INDEX IF NOT EXISTS uq_aocr_tbinspectores_cedula
    ON public.aocr_tbinspectores (LOWER(TRIM(cedula)));

-- Índice de búsqueda directa
CREATE INDEX IF NOT EXISTS idx_aocr_tbinspectores_cedula
    ON public.aocr_tbinspectores (cedula);

-- Índice de estado y tipo para filtros frecuentes
CREATE INDEX IF NOT EXISTS idx_aocr_tbinspectores_estado_tipo
    ON public.aocr_tbinspectores (estado, tipo);

COMMENT ON TABLE public.aocr_tbinspectores IS
    'Espejo local de DGACDAT.OPIAR2 (AS400). Se sincroniza vía SyncAdmin/RunInspectoresRt.';

COMMIT;
