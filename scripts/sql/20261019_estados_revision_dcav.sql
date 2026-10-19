-- Ejecutar antes de remitir a DCAV con el flujo actualizado.
-- Amplia la restriccion existente sin cambiar solicitudes ni revisiones.
BEGIN;
DO $$
DECLARE
    expresion text;
BEGIN
    SELECT pg_get_expr(conbin, conrelid) INTO expresion
    FROM pg_constraint
    WHERE conrelid = 'public.aocr_revision_documental_coordinador'::regclass
      AND conname = 'chk_aocr_revdoc_coord_estado'
      AND contype = 'c';

    IF expresion IS NOT NULL AND
       (position('PENDIENTE_ACEPTACION_DCAV' in expresion) = 0 OR
        position('ACEPTADA_POR_DCAV' in expresion) = 0) THEN
        ALTER TABLE public.aocr_revision_documental_coordinador
            DROP CONSTRAINT chk_aocr_revdoc_coord_estado;
        EXECUTE format(
            'ALTER TABLE public.aocr_revision_documental_coordinador ADD CONSTRAINT chk_aocr_revdoc_coord_estado CHECK ((%s) OR estado IN (''PENDIENTE_ACEPTACION_DCAV'', ''ACEPTADA_POR_DCAV''))',
            expresion);
    END IF;
END $$;
COMMIT;
