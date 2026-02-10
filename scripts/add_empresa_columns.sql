-- ============================================
-- Script para agregar columnas de empresa
-- a la tabla usuario en PostgreSQL
-- ============================================
-- Fecha: 2026-02-09
-- Base: dgac_des
-- ============================================

-- Verifica si las columnas ya existen antes de crear
DO $$ 
BEGIN
    -- Agregar columna empresa_codigo (código OACI de AS/400)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'usuario' 
        AND column_name = 'empresa_codigo'
    ) THEN
        ALTER TABLE usuario 
        ADD COLUMN empresa_codigo VARCHAR(5);
        
        COMMENT ON COLUMN usuario.empresa_codigo IS 'Código OACI de la empresa del usuario (desde AS/400 CIAARC)';
        
        RAISE NOTICE 'Columna empresa_codigo agregada exitosamente';
    ELSE
        RAISE NOTICE 'Columna empresa_codigo ya existe';
    END IF;

    -- Agregar columna ruta_documento_legal
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'usuario' 
        AND column_name = 'ruta_documento_legal'
    ) THEN
        ALTER TABLE usuario 
        ADD COLUMN ruta_documento_legal VARCHAR(500);
        
        COMMENT ON COLUMN usuario.ruta_documento_legal IS 'Ruta al archivo PDF con carta de delegación o poder';
        
        RAISE NOTICE 'Columna ruta_documento_legal agregada exitosamente';
    ELSE
        RAISE NOTICE 'Columna ruta_documento_legal ya existe';
    END IF;
END $$;

-- Opcional: Crear índice para búsquedas por empresa
CREATE INDEX IF NOT EXISTS idx_usuario_empresa_codigo 
ON usuario(empresa_codigo) 
WHERE empresa_codigo IS NOT NULL;

-- Verificar estructura final
SELECT 
    column_name, 
    data_type, 
    character_maximum_length,
    is_nullable
FROM information_schema.columns
WHERE table_name = 'usuario'
  AND column_name IN ('empresa_codigo', 'ruta_documento_legal')
ORDER BY ordinal_position;
