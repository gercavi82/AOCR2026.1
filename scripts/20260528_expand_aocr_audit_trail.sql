-- ============================================================================
-- Migración: ampliar trazabilidad AOCR sin romper historial existente
-- Fecha: 2026-05-28
-- Propósito: evitar fallos silenciosos por longitudes cortas en aocr_audit_trail
-- IDEMPOTENTE
-- ============================================================================

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'aocr_audit_trail') THEN
        ALTER TABLE aocr_audit_trail ALTER COLUMN accion TYPE VARCHAR(100);
        ALTER TABLE aocr_audit_trail ALTER COLUMN modulo TYPE VARCHAR(100);

        IF NOT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_name = 'aocr_audit_trail'
              AND column_name = 'estado_anterior'
        ) THEN
            ALTER TABLE aocr_audit_trail ADD COLUMN estado_anterior VARCHAR(100);
        END IF;

        IF NOT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_name = 'aocr_audit_trail'
              AND column_name = 'estado_nuevo'
        ) THEN
            ALTER TABLE aocr_audit_trail ADD COLUMN estado_nuevo VARCHAR(100);
        END IF;

        RAISE NOTICE 'aocr_audit_trail ampliada correctamente.';
    ELSE
        RAISE NOTICE 'aocr_audit_trail no existe; no se aplicaron cambios.';
    END IF;
END $$;