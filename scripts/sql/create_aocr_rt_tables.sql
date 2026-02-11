-- SQL: Crear tablas para flujo RT (FASE 1)
-- Crea tablas solo si no existen para no duplicar en entornos existentes

CREATE TABLE IF NOT EXISTS aocr_compania (
    id SERIAL PRIMARY KEY,
    razon_social VARCHAR(200) NOT NULL,
    ruc VARCHAR(20) NOT NULL,
    telefono VARCHAR(30) NOT NULL,
    email_contacto VARCHAR(120) NOT NULL,
    area_contable_json JSONB NULL,
    created_at TIMESTAMP NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_compania_ruc ON aocr_compania(ruc);


CREATE TABLE IF NOT EXISTS aocr_solicitud_rt (
    id SERIAL PRIMARY KEY,
    usuario_rt_id INT NOT NULL,
    compania_id INT NOT NULL REFERENCES aocr_compania(id) ON DELETE CASCADE,
    estado VARCHAR(20) NOT NULL CHECK (estado IN ('BORRADOR','ENVIADA','DEVUELTA','APROBADA')),
    declaracion_aceptada BOOLEAN NOT NULL DEFAULT FALSE,
    declaracion_texto TEXT NOT NULL,
    fecha_envio TIMESTAMP NULL,
    observacion_coordinador TEXT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT now(),
    updated_at TIMESTAMP NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_solicitud_rt_estado ON aocr_solicitud_rt(estado);
CREATE INDEX IF NOT EXISTS idx_solicitud_rt_usuario ON aocr_solicitud_rt(usuario_rt_id);


CREATE TABLE IF NOT EXISTS aocr_documento (
    id SERIAL PRIMARY KEY,
    solicitud_rt_id INT NOT NULL REFERENCES aocr_solicitud_rt(id) ON DELETE CASCADE,
    tipo VARCHAR(40) NOT NULL CHECK (tipo IN ('DESIGNACION_RT')),
    nombre_archivo VARCHAR(255) NOT NULL,
    ruta_storage VARCHAR(500) NOT NULL,
    tamano_bytes BIGINT NOT NULL,
    hash_sha256 VARCHAR(64) NULL,
    created_at TIMESTAMP NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_doc_solicitud_tipo ON aocr_documento(solicitud_rt_id, tipo);

-- Tabla de historial de estados (trazabilidad)
CREATE TABLE IF NOT EXISTS aocr_solicitud_rt_historial (
    id SERIAL PRIMARY KEY,
    solicitud_rt_id INT NOT NULL REFERENCES aocr_solicitud_rt(id) ON DELETE CASCADE,
    estado VARCHAR(20) NOT NULL,
    motivo TEXT NULL,
    usuario_id INT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_historial_solicitud ON aocr_solicitud_rt_historial(solicitud_rt_id);

-- Nota: Existe tabla usuario; asumimos columna idusuario como PK y es referenciada desde la lógica de la aplicación.
-- Si en algún entorno prefieres usar otra columna/tabla, ajusta las foreign keys correspondientes.
