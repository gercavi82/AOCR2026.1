-- Migración: Agregar columna usuario_id a tabla aocr_tbauditoria
-- Fecha: 2026-09-22
-- Descripción: Se agrega la columna usuario_id (INTEGER NULL) a aocr_tbauditoria 
-- para rastrear qué usuario realizó cambios en estaciones de inspección y condiciones/limitaciones

BEGIN;

-- Agregar columna usuario_id si no existe
ALTER TABLE IF EXISTS public.aocr_tbauditoria
ADD COLUMN IF NOT EXISTS usuario_id INTEGER NULL;

-- Crear índice para usuario_id para mejorar búsquedas futuras
CREATE INDEX IF NOT EXISTS idx_aocr_tbauditoria_usuario_id 
ON public.aocr_tbauditoria (usuario_id);

-- Agregar comentario a la columna
COMMENT ON COLUMN public.aocr_tbauditoria.usuario_id IS 'ID del usuario que realizó la acción auditada (referencia a tabla usuario.idusuario)';

COMMIT;
