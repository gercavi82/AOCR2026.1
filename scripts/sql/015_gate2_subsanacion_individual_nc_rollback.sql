BEGIN;

DROP INDEX IF EXISTS public.ix_docsub_nc_gate2;
DROP INDEX IF EXISTS public.ux_docsub_nueva_gate2;
ALTER TABLE public.aocr_tbdocumento_subsanacion
    DROP CONSTRAINT IF EXISTS chk_docsub_fuente_gate2,
    DROP CONSTRAINT IF EXISTS fk_docsub_nueva_gate2,
    DROP CONSTRAINT IF EXISTS fk_docsub_origen_gate2,
    DROP CONSTRAINT IF EXISTS fk_docsub_nc_gate2;

-- Rollback no destructivo: las columnas y sus datos se conservan. La nulabilidad
-- de codigo_subsanacion tampoco se revierte porque existen filas Gate 2 sin ese FK.

COMMIT;
