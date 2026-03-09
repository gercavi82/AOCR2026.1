-- Migración idempotente: relación formal Usuario RT <-> Compañía
-- Fecha: 2026-03-06
-- Base: PostgreSQL

BEGIN;

CREATE TABLE IF NOT EXISTS aocr_usuario_compania_rt
(
    id              SERIAL PRIMARY KEY,
    usuario_id      INT NOT NULL REFERENCES usuario(idusuario) ON DELETE CASCADE,
    compania_codigo VARCHAR(10) NOT NULL,
    compania_nombre VARCHAR(250) NULL,
    activo          BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by      VARCHAR(120) NOT NULL DEFAULT 'migracion',
    updated_at      TIMESTAMP NULL,
    updated_by      VARCHAR(120) NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS uix_aocr_usuario_compania_rt_usuario_codigo
    ON aocr_usuario_compania_rt(usuario_id, compania_codigo);

CREATE INDEX IF NOT EXISTS idx_aocr_usuario_compania_rt_usuario_activo
    ON aocr_usuario_compania_rt(usuario_id)
    WHERE activo = TRUE;

CREATE INDEX IF NOT EXISTS idx_aocr_usuario_compania_rt_compania_activo
    ON aocr_usuario_compania_rt(compania_codigo)
    WHERE activo = TRUE;

-- Backfill de compatibilidad: si el usuario tenía empresa principal, se crea asignación.
INSERT INTO aocr_usuario_compania_rt
    (usuario_id, compania_codigo, compania_nombre, activo, created_at, created_by)
SELECT
    u.idusuario,
    TRIM(u.empresa_codigo),
    NULL,
    TRUE,
    NOW(),
    'backfill_legacy_empresa_codigo'
FROM usuario u
WHERE COALESCE(TRIM(u.empresa_codigo), '') <> ''
  AND NOT EXISTS
  (
      SELECT 1
      FROM aocr_usuario_compania_rt r
      WHERE r.usuario_id = u.idusuario
        AND UPPER(TRIM(r.compania_codigo)) = UPPER(TRIM(u.empresa_codigo))
  );

COMMIT;

SELECT '20260306_aocr_usuario_compania_rt: OK' AS result;
