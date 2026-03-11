-- AOCR FormularioCompleto Hardening (idempotente)
-- Fecha: 2026-03-10
-- Objetivo:
-- 1) Mantener consistencia del CHECK de estado en aocr_tbdocumento.
-- 2) Garantizar la existencia de una tabla de historial compatible
--    sin duplicar cuando ya existe aocr_tbhistorial_estado.

BEGIN;

-- =========================================================
-- 1) Documento: CHECK de estado coherente con flujo AOCR
-- =========================================================
DO $$
BEGIN
    IF to_regclass('public.aocr_tbdocumento') IS NOT NULL THEN
        IF EXISTS (
            SELECT 1
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'public'
              AND t.relname = 'aocr_tbdocumento'
              AND c.conname = 'chk_estado_documento'
        ) THEN
            ALTER TABLE public.aocr_tbdocumento
                DROP CONSTRAINT chk_estado_documento;
        END IF;

        ALTER TABLE public.aocr_tbdocumento
            ADD CONSTRAINT chk_estado_documento
            CHECK (
                estado IS NULL
                OR estado IN (
                    'Cargado',
                    U&'En Revisi\00F3n',
                    'Aprobado',
                    'Rechazado',
                    'Subsanado'
                )
            );
    END IF;
END $$;

-- =========================================================
-- 2) Historial: crear solo si no existe ninguna variante
-- =========================================================
DO $$
BEGIN
    IF to_regclass('public.aocr_tbhistorialestado') IS NULL
       AND to_regclass('public.aocr_tbhistorial_estado') IS NULL THEN
        CREATE TABLE public.aocr_tbhistorialestado (
            codigohistorial SERIAL PRIMARY KEY,
            codigosolicitud INTEGER NOT NULL,
            estadoanterior VARCHAR(50),
            estadonuevo VARCHAR(50) NOT NULL,
            codigousuario INTEGER,
            observaciones TEXT,
            fechacambio TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        ALTER TABLE public.aocr_tbhistorialestado
            ADD CONSTRAINT fk_historial_solicitud
            FOREIGN KEY (codigosolicitud)
            REFERENCES public.aocr_tbsolicitud(codigo_solicitud)
            ON DELETE CASCADE;

        CREATE INDEX IF NOT EXISTS idx_historial_solicitud
            ON public.aocr_tbhistorialestado(codigosolicitud);

        CREATE INDEX IF NOT EXISTS idx_historial_fecha
            ON public.aocr_tbhistorialestado(fechacambio);
    END IF;
END $$;

COMMIT;
