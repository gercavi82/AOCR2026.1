-- Migración: Arreglar estructura de tabla aocr_tbauditoria
-- Fecha: 2026-09-22
-- Descripción: Se agregan columnas faltantes (usuario_id, fecha, entidad) para compatibilidad con DAOs
-- que usan diferentes formatos de INSERT

BEGIN;

-- Agregar columnas faltantes (si no existen)
ALTER TABLE IF EXISTS public.aocr_tbauditoria
ADD COLUMN IF NOT EXISTS usuario_id INTEGER NULL,
ADD COLUMN IF NOT EXISTS fecha TIMESTAMP NULL,
ADD COLUMN IF NOT EXISTS entidad VARCHAR(100) NULL;

-- Crear índices para mejorar búsquedas
CREATE INDEX IF NOT EXISTS idx_aocr_tbauditoria_usuario_id 
ON public.aocr_tbauditoria (usuario_id);

CREATE INDEX IF NOT EXISTS idx_aocr_tbauditoria_fecha 
ON public.aocr_tbauditoria (fecha);

CREATE INDEX IF NOT EXISTS idx_aocr_tbauditoria_modulo 
ON public.aocr_tbauditoria (modulo);

-- Comentarios descriptivos
COMMENT ON COLUMN public.aocr_tbauditoria.usuario_id IS 'ID del usuario que realizó la acción (referencia a tabla usuario.idusuario)';
COMMENT ON COLUMN public.aocr_tbauditoria.fecha IS 'Timestamp de la acción (alias para fecha_accion para compatibilidad)';
COMMENT ON COLUMN public.aocr_tbauditoria.entidad IS 'Nombre de la entidad auditada (compatibilidad con AocrDesignacionDAO)';

-- Crear vista para compatibilidad si algún DAO espera una columna "fecha" específica
-- Esto permite que "INSERT INTO aocr_tbauditoria (fecha)" funcione aunque sea una alias
CREATE OR REPLACE FUNCTION aocr_audit_insert_compat()
RETURNS TRIGGER AS $$
BEGIN
    -- Si fecha es NULL pero fecha_accion tiene valor, sincronizar
    IF NEW.fecha IS NULL AND NEW.fecha_accion IS NOT NULL THEN
        NEW.fecha := NEW.fecha_accion;
    END IF;
    -- Si fecha_accion es NULL pero fecha tiene valor, sincronizar
    IF NEW.fecha_accion IS NULL AND NEW.fecha IS NOT NULL THEN
        NEW.fecha_accion := NEW.fecha;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Crear trigger para sincronización automática
DROP TRIGGER IF EXISTS trg_aocr_audit_sync_fecha ON public.aocr_tbauditoria;
CREATE TRIGGER trg_aocr_audit_sync_fecha
BEFORE INSERT OR UPDATE ON public.aocr_tbauditoria
FOR EACH ROW
EXECUTE FUNCTION aocr_audit_insert_compat();

COMMIT;
