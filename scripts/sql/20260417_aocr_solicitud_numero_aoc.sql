-- Migration: 20260417 - Añadir campo numero_aoc a aocr_tbsolicitud
-- Acta 17-04-2026 | Punto 3.5: Número de AOC de la compañía operadora
-- Ejecutar una sola vez en PostgreSQL

ALTER TABLE aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS numero_aoc VARCHAR(100);

COMMENT ON COLUMN aocr_tbsolicitud.numero_aoc
    IS 'Número del Certificate of Air Operator (AOC) vigente de la compañía - Acta 17-04-2026';
