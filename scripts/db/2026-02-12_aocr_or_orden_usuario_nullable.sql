-- Permitir NULL en la FK de usuario para poder eliminar usuarios sin borrar órdenes
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'aocr_or_orden'
          AND column_name = 'usuario_id'
    ) THEN
        EXECUTE 'ALTER TABLE aocr_or_orden ALTER COLUMN usuario_id DROP NOT NULL';
    ELSIF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'aocr_or_orden'
          AND column_name = 'codigo_usuario'
    ) THEN
        EXECUTE 'ALTER TABLE aocr_or_orden ALTER COLUMN codigo_usuario DROP NOT NULL';
    ELSIF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'aocr_or_orden'
          AND column_name = 'codigousuario'
    ) THEN
        EXECUTE 'ALTER TABLE aocr_or_orden ALTER COLUMN codigousuario DROP NOT NULL';
    END IF;
END $$;
