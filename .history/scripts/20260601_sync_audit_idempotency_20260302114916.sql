-- ============================================================================
-- Migración: Sync Log, Idempotency Keys, Audit Trail
-- Fecha: 2026-06-01
-- Propósito: Phase 3-6 Enterprise Hardening
-- IDEMPOTENTE: Puede ejecutarse múltiples veces sin errores
-- ============================================================================

-- ============================================================================
-- 1. TABLA DE SYNC LOG (Registro detallado de operaciones AS400/DB2)
-- ============================================================================
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables 
                   WHERE table_name = 'aocr_sync_log') THEN
        CREATE TABLE aocr_sync_log (
            id                  SERIAL PRIMARY KEY,
            operacion           VARCHAR(50) NOT NULL,           -- FR3_REGISTRO, FR3_RETRY, SYNC_MIRROR, etc.
            sistema_origen      VARCHAR(20) NOT NULL DEFAULT 'AOCR',
            sistema_destino     VARCHAR(20) NOT NULL DEFAULT 'AS400',
            orden_id            INTEGER,
            pago_id             INTEGER,
            idempotency_key     VARCHAR(128),                   -- Clave única para deduplicación
            estado              VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE', -- PENDIENTE, EN_PROCESO, COMPLETADO, ERROR, REINTENTANDO
            detalle_request     TEXT,                            -- JSON del request enviado
            detalle_response    TEXT,                            -- JSON de la respuesta
            error_mensaje       TEXT,
            error_codigo        VARCHAR(50),
            intentos            INTEGER NOT NULL DEFAULT 0,
            max_intentos        INTEGER NOT NULL DEFAULT 3,
            proximo_reintento   TIMESTAMP WITH TIME ZONE,
            inicio_operacion    TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
            fin_operacion       TIMESTAMP WITH TIME ZONE,
            duracion_ms         BIGINT,
            usuario             VARCHAR(100),
            ip_origen           VARCHAR(45),
            correlacion_id      VARCHAR(100),                   -- Para tracking distribuido
            fr3_numero          VARCHAR(50),
            fr3_secuencial      NUMERIC(15,0),
            fr3_aeropuerto      VARCHAR(10),
            fr3_anio            VARCHAR(4),
            metadata            TEXT,                            -- JSON adicional
            fecha_creacion      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
            fecha_actualizacion TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
        );

        -- Índices para queries frecuentes
        CREATE INDEX idx_sync_log_orden ON aocr_sync_log(orden_id);
        CREATE INDEX idx_sync_log_estado ON aocr_sync_log(estado);
        CREATE INDEX idx_sync_log_operacion ON aocr_sync_log(operacion);
        CREATE INDEX idx_sync_log_idempotency ON aocr_sync_log(idempotency_key) WHERE idempotency_key IS NOT NULL;
        CREATE INDEX idx_sync_log_reintento ON aocr_sync_log(proximo_reintento) 
            WHERE estado IN ('ERROR', 'REINTENTANDO') AND intentos < max_intentos;
        CREATE INDEX idx_sync_log_fecha ON aocr_sync_log(fecha_creacion);

        RAISE NOTICE 'Tabla aocr_sync_log creada exitosamente.';
    ELSE
        RAISE NOTICE 'Tabla aocr_sync_log ya existe.';
    END IF;
END $$;

-- ============================================================================
-- 2. TABLA DE IDEMPOTENCY KEYS (Prevención de operaciones duplicadas)
-- ============================================================================
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables 
                   WHERE table_name = 'aocr_idempotency_key') THEN
        CREATE TABLE aocr_idempotency_key (
            id                  SERIAL PRIMARY KEY,
            clave               VARCHAR(128) NOT NULL UNIQUE,   -- Hash único de la operación
            operacion           VARCHAR(50) NOT NULL,           -- Tipo de operación
            orden_id            INTEGER,
            resultado           TEXT,                            -- JSON del resultado almacenado
            estado              VARCHAR(20) NOT NULL DEFAULT 'PROCESANDO', -- PROCESANDO, COMPLETADO, ERROR
            fecha_creacion      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
            fecha_expiracion    TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT (NOW() + INTERVAL '24 hours'),
            usuario             VARCHAR(100)
        );

        CREATE INDEX idx_idempotency_clave ON aocr_idempotency_key(clave);
        CREATE INDEX idx_idempotency_expiracion ON aocr_idempotency_key(fecha_expiracion);
        CREATE INDEX idx_idempotency_orden ON aocr_idempotency_key(orden_id);

        RAISE NOTICE 'Tabla aocr_idempotency_key creada exitosamente.';
    ELSE
        RAISE NOTICE 'Tabla aocr_idempotency_key ya existe.';
    END IF;
END $$;

-- ============================================================================
-- 3. TABLA DE AUDIT TRAIL (Auditoría completa de operaciones críticas)
-- ============================================================================
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables 
                   WHERE table_name = 'aocr_audit_trail') THEN
        CREATE TABLE aocr_audit_trail (
            id                  SERIAL PRIMARY KEY,
            tabla               VARCHAR(100) NOT NULL,          -- Tabla afectada
            registro_id         INTEGER,                         -- ID del registro afectado
            accion              VARCHAR(20) NOT NULL,            -- INSERT, UPDATE, DELETE, CAMBIO_ESTADO
            campo_modificado    VARCHAR(100),                    -- Campo específico modificado
            valor_anterior      TEXT,
            valor_nuevo         TEXT,
            usuario_id          INTEGER,
            usuario_nombre      VARCHAR(100),
            ip_origen           VARCHAR(45),
            user_agent          VARCHAR(500),
            modulo              VARCHAR(50),                     -- OrdenRecaudacion, FR3, Pago, etc.
            correlacion_id      VARCHAR(100),
            metadata            TEXT,                            -- JSON adicional
            fecha_creacion      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
        );

        CREATE INDEX idx_audit_tabla ON aocr_audit_trail(tabla, registro_id);
        CREATE INDEX idx_audit_accion ON aocr_audit_trail(accion);
        CREATE INDEX idx_audit_usuario ON aocr_audit_trail(usuario_id);
        CREATE INDEX idx_audit_fecha ON aocr_audit_trail(fecha_creacion);
        CREATE INDEX idx_audit_modulo ON aocr_audit_trail(modulo);

        RAISE NOTICE 'Tabla aocr_audit_trail creada exitosamente.';
    ELSE
        RAISE NOTICE 'Tabla aocr_audit_trail ya existe.';
    END IF;
END $$;

-- ============================================================================
-- 4. TABLA DE FR3 RETRY QUEUE (Cola de reintentos para facturación AS400)
-- ============================================================================
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables 
                   WHERE table_name = 'aocr_fr3_retry_queue') THEN
        CREATE TABLE aocr_fr3_retry_queue (
            id                  SERIAL PRIMARY KEY,
            orden_id            INTEGER NOT NULL,
            pago_id             INTEGER,
            numero_factura      VARCHAR(50),
            autorizacion        VARCHAR(100),
            estado              VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE', -- PENDIENTE, EN_PROCESO, COMPLETADO, FALLIDO, CANCELADO
            intentos            INTEGER NOT NULL DEFAULT 0,
            max_intentos        INTEGER NOT NULL DEFAULT 5,
            ultimo_error        TEXT,
            proximo_intento     TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
            -- Backoff exponencial: 1min, 5min, 15min, 1h, 4h
            factor_backoff      INTEGER NOT NULL DEFAULT 1,
            prioridad           INTEGER NOT NULL DEFAULT 0,     -- Mayor = más prioritario
            usuario_creacion    VARCHAR(100),
            usuario_ultimo      VARCHAR(100),
            correlacion_id      VARCHAR(100),
            fecha_creacion      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
            fecha_actualizacion TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
            fecha_completado    TIMESTAMP WITH TIME ZONE,
            fr3_numero          VARCHAR(50),                    -- Se llena al completar
            fr3_secuencial      NUMERIC(15,0)
        );

        CREATE INDEX idx_fr3_retry_estado ON aocr_fr3_retry_queue(estado);
        CREATE INDEX idx_fr3_retry_proximo ON aocr_fr3_retry_queue(proximo_intento) 
            WHERE estado IN ('PENDIENTE', 'EN_PROCESO');
        CREATE INDEX idx_fr3_retry_orden ON aocr_fr3_retry_queue(orden_id);

        RAISE NOTICE 'Tabla aocr_fr3_retry_queue creada exitosamente.';
    ELSE
        RAISE NOTICE 'Tabla aocr_fr3_retry_queue ya existe.';
    END IF;
END $$;

-- ============================================================================
-- 5. AGREGAR COLUMNAS FR3 FALTANTES A aocr_or_orden (si no existen)
-- ============================================================================
DO $$
BEGIN
    -- fr3_estado en la orden misma (además de aocr_tb_factura_pago)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'aocr_or_orden' AND column_name = 'fr3_estado') THEN
        ALTER TABLE aocr_or_orden ADD COLUMN fr3_estado VARCHAR(20);
        RAISE NOTICE 'Columna fr3_estado agregada a aocr_or_orden.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'aocr_or_orden' AND column_name = 'fr3_numero') THEN
        ALTER TABLE aocr_or_orden ADD COLUMN fr3_numero VARCHAR(50);
        RAISE NOTICE 'Columna fr3_numero agregada a aocr_or_orden.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'aocr_or_orden' AND column_name = 'fr3_error') THEN
        ALTER TABLE aocr_or_orden ADD COLUMN fr3_error TEXT;
        RAISE NOTICE 'Columna fr3_error agregada a aocr_or_orden.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'aocr_or_orden' AND column_name = 'idempotency_key') THEN
        ALTER TABLE aocr_or_orden ADD COLUMN idempotency_key VARCHAR(128);
        RAISE NOTICE 'Columna idempotency_key agregada a aocr_or_orden.';
    END IF;
END $$;

-- ============================================================================
-- 6. FUNCIÓN DE LIMPIEZA DE DATOS EXPIRADOS
-- ============================================================================
CREATE OR REPLACE FUNCTION aocr_limpiar_datos_expirados()
RETURNS void AS $$
BEGIN
    -- Limpiar idempotency keys expiradas
    DELETE FROM aocr_idempotency_key 
    WHERE fecha_expiracion < NOW() - INTERVAL '7 days';

    -- Limpiar sync logs antiguos (mantener 90 días)
    DELETE FROM aocr_sync_log 
    WHERE fecha_creacion < NOW() - INTERVAL '90 days';

    -- Limpiar audit trail antiguo (mantener 1 año)
    DELETE FROM aocr_audit_trail 
    WHERE fecha_creacion < NOW() - INTERVAL '365 days';

    -- Marcar como FALLIDO los reintentos FR3 que excedieron max_intentos
    UPDATE aocr_fr3_retry_queue 
    SET estado = 'FALLIDO', 
        fecha_actualizacion = NOW()
    WHERE estado IN ('PENDIENTE', 'EN_PROCESO') 
      AND intentos >= max_intentos;
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- 7. VISTA RESUMEN DE SYNC LOG
-- ============================================================================
CREATE OR REPLACE VIEW v_aocr_sync_resumen AS
SELECT 
    operacion,
    estado,
    COUNT(*) as total,
    AVG(duracion_ms) as duracion_promedio_ms,
    MAX(fecha_creacion) as ultimo_registro,
    SUM(CASE WHEN estado = 'ERROR' THEN 1 ELSE 0 END) as total_errores,
    SUM(CASE WHEN estado = 'COMPLETADO' THEN 1 ELSE 0 END) as total_exitosos
FROM aocr_sync_log
WHERE fecha_creacion > NOW() - INTERVAL '30 days'
GROUP BY operacion, estado
ORDER BY operacion, estado;

-- ============================================================================
-- 8. VISTA DE REINTENTOS PENDIENTES FR3
-- ============================================================================
CREATE OR REPLACE VIEW v_aocr_fr3_pendientes AS
SELECT 
    rq.id,
    rq.orden_id,
    rq.numero_factura,
    rq.estado,
    rq.intentos,
    rq.max_intentos,
    rq.proximo_intento,
    rq.ultimo_error,
    rq.fecha_creacion,
    o.numero_orden,
    o.compania,
    o.ruc_cedula,
    o.total as total_orden
FROM aocr_fr3_retry_queue rq
LEFT JOIN aocr_or_orden o ON o.id = rq.orden_id
WHERE rq.estado IN ('PENDIENTE', 'EN_PROCESO')
  AND rq.intentos < rq.max_intentos
ORDER BY rq.prioridad DESC, rq.proximo_intento ASC;

RAISE NOTICE 'Migración 20260601 completada exitosamente.';
