-- =========================================================================
-- Script SQL: 011_no_conformidades.sql
-- Descripción: Creación de la tabla aocr_tbnoconformidad para el Módulo 7 y 8
-- Idempotencia garantizada con IF NOT EXISTS
-- =========================================================================

CREATE TABLE IF NOT EXISTS public.aocr_tbnoconformidad (
    codigo_no_conformidad SERIAL PRIMARY KEY,
    codigo_inspeccion INT NOT NULL,
    codigo_informe INT NOT NULL,
    codigo_solicitud INT NOT NULL,
    tipo_ruta VARCHAR(50) NOT NULL,
    estado VARCHAR(50) NOT NULL,
    numero_no_conformidad VARCHAR(100) NULL,
    resumen TEXT,
    detalle TEXT,
    fundamento_tecnico TEXT,
    acciones_requeridas TEXT,
    plazo_subsanacion INT NULL,
    requiere_nueva_inspeccion BOOLEAN DEFAULT FALSE,
    version INT DEFAULT 1,
    ruta_pdf VARCHAR(500) NULL,
    ruta_pdf_firmado_inspector VARCHAR(500) NULL,
    ruta_pdf_firmado_coordinador VARCHAR(500) NULL,
    hash_documento VARCHAR(256) NULL,
    fecha_generacion TIMESTAMP NULL,
    fecha_firma_inspector TIMESTAMP NULL,
    fecha_envio_coordinador TIMESTAMP NULL,
    fecha_devolucion TIMESTAMP NULL,
    fecha_firma_coordinador TIMESTAMP NULL,
    fecha_notificacion_rt TIMESTAMP NULL,
    usuario_creacion INT NULL,
    usuario_firma_inspector INT NULL,
    usuario_firma_coordinador INT NULL,
    observacion_devolucion TEXT NULL,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP NULL
);

-- Crear índices para acelerar búsquedas
CREATE INDEX IF NOT EXISTS idx_noconf_informe ON public.aocr_tbnoconformidad(codigo_informe);
CREATE INDEX IF NOT EXISTS idx_noconf_inspeccion ON public.aocr_tbnoconformidad(codigo_inspeccion);
CREATE INDEX IF NOT EXISTS idx_noconf_solicitud ON public.aocr_tbnoconformidad(codigo_solicitud);
CREATE INDEX IF NOT EXISTS idx_noconf_estado ON public.aocr_tbnoconformidad(estado);
