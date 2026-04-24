-- ===================================================================
-- MIGRACIÓN: FLUJO BPMN AOCR — COLUMNAS Y TABLAS FALTANTES
-- ===================================================================
-- Fecha     : 2026-04-24
-- Propósito : Añadir columnas y relaciones necesarias para el flujo
--             completo: Financiero → RT → Coordinador → Inspector →
--             DIRDAC → Certificado AOCR.
-- 
-- Seguridad : SOLO usa ADD COLUMN IF NOT EXISTS y CREATE TABLE IF NOT EXISTS.
--             No elimina columnas. No renombra. No cambia tipos de datos
--             existentes. Compatible con la BD actual sin downtime.
-- ===================================================================

BEGIN;

-- ===================================================================
-- 1. aocr_tbsolicitud — Columnas del flujo de pago y asignación
-- ===================================================================

-- Fecha en que Financiero aprobó el pago (para auditoría)
ALTER TABLE aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS fecha_pago_aprobado TIMESTAMP NULL;

-- Usuario de Financiero que aprobó el pago
ALTER TABLE aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS codigo_usuario_pago_aprobado INTEGER NULL;

-- ID del inspector asignado explícito (complementa CodigoTecnico legacy)
ALTER TABLE aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS inspector_asignado_id INTEGER NULL;

-- Fecha de asignación del inspector
ALTER TABLE aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS fecha_asignacion_inspector TIMESTAMP NULL;

-- Usuario que realizó la asignación del inspector
ALTER TABLE aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS asignado_por_usuario_id INTEGER NULL;

-- Resultado técnico de la inspección (SATISFACTORIO / INSATISFACTORIO)
ALTER TABLE aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS resultado_tecnico VARCHAR(50) NULL;

-- Fecha en que DIRDAC aprobó el informe técnico
ALTER TABLE aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS fecha_aprobacion_dirdac TIMESTAMP NULL;

-- Usuario DIRDAC que aprobó el informe
ALTER TABLE aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS codigo_usuario_dirdac INTEGER NULL;

-- Observación de DIRDAC al devolver el informe
ALTER TABLE aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS observacion_dirdac TEXT NULL;

-- ===================================================================
-- 2. aocr_tbinspeccion — Columnas del flujo de inspección
-- ===================================================================

-- Estado de la revisión documental (PENDIENTE / EN_REVISION / APROBADA / OBSERVADA)
ALTER TABLE aocr_tbinspeccion
    ADD COLUMN IF NOT EXISTS estado_revision_documental VARCHAR(50) NULL DEFAULT 'PENDIENTE';

-- Fecha en que se finalizó la revisión documental
ALTER TABLE aocr_tbinspeccion
    ADD COLUMN IF NOT EXISTS fecha_fin_revision_documental TIMESTAMP NULL;

-- Inspector que realizó la revisión documental (puede diferir del inspector de inspección)
ALTER TABLE aocr_tbinspeccion
    ADD COLUMN IF NOT EXISTS inspector_revision_documental_id INTEGER NULL;

-- ===================================================================
-- 3. aocr_tbrevision_documental — Asegurar columnas de observación
-- ===================================================================

-- Observación detallada por documento
ALTER TABLE aocr_tbrevision_documental
    ADD COLUMN IF NOT EXISTS observacion_detallada TEXT NULL;

-- Estado BPMN del documento: PENDIENTE_REVISION / APROBADO / OBSERVADO / SUBSANADO
ALTER TABLE aocr_tbrevision_documental
    ADD COLUMN IF NOT EXISTS estado_bpmn VARCHAR(50) NULL DEFAULT 'PENDIENTE_REVISION';

-- Fecha de la última revisión del documento
ALTER TABLE aocr_tbrevision_documental
    ADD COLUMN IF NOT EXISTS fecha_revision TIMESTAMP NULL;

-- Número de versión (incrementa con cada subsanación)
ALTER TABLE aocr_tbrevision_documental
    ADD COLUMN IF NOT EXISTS version_documento INTEGER NULL DEFAULT 1;

-- ===================================================================
-- 4. aocr_tbno_conformidad — Tabla NC si no existe
-- ===================================================================
-- Nota: Si el flujo ya usa aocr_tbhallazgo para las NC, esta tabla
--       es adicional para NCs formales de resultado INSATISFACTORIO.
--       El sistema puede coexistir con hallazgos y NCs formales.

CREATE TABLE IF NOT EXISTS aocr_tbno_conformidad (
    id                     SERIAL PRIMARY KEY,
    inspeccion_id          INTEGER NOT NULL
                               REFERENCES aocr_tbinspeccion(codigo_inspeccion)
                               ON DELETE RESTRICT,
    solicitud_id           INTEGER NOT NULL,
    inspector_id           INTEGER NULL,
    fecha_generacion       TIMESTAMP NOT NULL DEFAULT NOW(),
    descripcion_tecnica    TEXT NOT NULL,
    requisitos_incumplidos TEXT NULL,
    recomendacion          VARCHAR(50) NULL,  -- CORRECCION_DOCUMENTAL / NUEVA_INSPECCION / EAE
    observaciones          TEXT NULL,
    estado                 VARCHAR(50) NOT NULL DEFAULT 'GENERADA',
    -- GENERADA / NC_EN_REVISION / NC_APROBADA / NC_DEVUELTA / NC_SUBSANADA / NC_CERRADA
    aprobado_por           INTEGER NULL,
    fecha_aprobacion       TIMESTAMP NULL,
    devuelto_por           INTEGER NULL,
    fecha_devolucion       TIMESTAMP NULL,
    observacion_coordinador TEXT NULL,
    subsanado_por          INTEGER NULL,
    fecha_subsanacion      TIMESTAMP NULL,
    creado_por             VARCHAR(120) NULL,
    creado_en              TIMESTAMP NOT NULL DEFAULT NOW(),
    actualizado_en         TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_aocr_nc_inspeccion
    ON aocr_tbno_conformidad(inspeccion_id);

CREATE INDEX IF NOT EXISTS ix_aocr_nc_solicitud
    ON aocr_tbno_conformidad(solicitud_id);

-- ===================================================================
-- 5. aocr_tbcertificado — Registro del certificado AOCR generado
-- ===================================================================
-- Complementa el Documento genérico con campos específicos del AOCR.
-- El documento físico se guarda en aocr_tbdocumento tipo AOCR_GENERADO.

CREATE TABLE IF NOT EXISTS aocr_tbcertificado (
    id                  SERIAL PRIMARY KEY,
    solicitud_id        INTEGER NOT NULL,
    documento_id        INTEGER NULL,     -- FK a aocr_tbdocumento cuando se genere
    numero_certificado  VARCHAR(80) NOT NULL,
    version             INTEGER NOT NULL DEFAULT 1,
    estado              VARCHAR(50) NOT NULL DEFAULT 'GENERADO',
    -- GENERADO / FIRMADO / ENVIADO / ANULADO
    generado_por        INTEGER NULL,
    fecha_generacion    TIMESTAMP NULL,
    firmado_por         INTEGER NULL,
    fecha_firma         TIMESTAMP NULL,
    ruta_archivo        TEXT NULL,
    hash_sha256         VARCHAR(64) NULL,
    vigencia_desde      DATE NULL,
    vigencia_hasta      DATE NULL,
    observaciones       TEXT NULL,
    creado_en           TIMESTAMP NOT NULL DEFAULT NOW(),
    actualizado_en      TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_aocr_certificado_solicitud
    ON aocr_tbcertificado(solicitud_id);

-- ===================================================================
-- 6. Índices auxiliares para trazabilidad
-- ===================================================================

-- Índice sobre estado en aocr_tbsolicitud para filtros de bandeja
CREATE INDEX IF NOT EXISTS ix_tbsolicitud_estado
    ON aocr_tbsolicitud(estado);

-- Índice sobre fecha_pago_aprobado para reportes Financiero
CREATE INDEX IF NOT EXISTS ix_tbsolicitud_fecha_pago
    ON aocr_tbsolicitud(fecha_pago_aprobado)
    WHERE fecha_pago_aprobado IS NOT NULL;

COMMIT;

-- ===================================================================
-- VERIFICACIÓN POST-MIGRACIÓN
-- ===================================================================
-- Ejecutar después del COMMIT para confirmar:
/*
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_name IN (
    'aocr_tbsolicitud',
    'aocr_tbinspeccion',
    'aocr_tbrevision_documental',
    'aocr_tbno_conformidad',
    'aocr_tbcertificado'
)
AND table_schema = 'public'
AND column_name IN (
    'fecha_pago_aprobado', 'inspector_asignado_id', 'resultado_tecnico',
    'fecha_aprobacion_dirdac', 'estado_revision_documental',
    'estado_bpmn', 'version_documento'
)
ORDER BY table_name, column_name;
*/
