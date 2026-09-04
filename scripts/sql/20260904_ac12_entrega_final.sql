BEGIN;

ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS message_id VARCHAR(255);
ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS sent_at TIMESTAMP;
ALTER TABLE public.email_attachment ADD COLUMN IF NOT EXISTS sha256 VARCHAR(64);

CREATE TABLE IF NOT EXISTS public.aocr_entrega_final (
    id BIGSERIAL PRIMARY KEY,
    solicitud_id INTEGER NOT NULL REFERENCES public.aocr_tbsolicitud(codigo_solicitud),
    inspeccion_id INTEGER NOT NULL REFERENCES public.aocr_tbinspeccion(codigo_inspeccion),
    codigo_compania VARCHAR(100) NOT NULL,
    compania VARCHAR(300) NOT NULL,
    version_aocr INTEGER NOT NULL,
    version_cl INTEGER NOT NULL,
    estado VARCHAR(50) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL,
    event_key VARCHAR(200) NOT NULL,
    fecha_completada TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by INTEGER NOT NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_ac12_entrega_estado CHECK (estado IN (
        'ENTREGA_NO_SOLICITADA','ENTREGA_ENCOLADA','ENTREGA_EN_PROCESO','ENTREGA_PARCIAL',
        'ENTREGA_COMPLETA','ENTREGA_FALLIDA_REINTENTABLE','ENTREGA_FALLIDA_DEFINITIVA')),
    CONSTRAINT ck_ac12_versiones_positivas CHECK(version_aocr > 0 AND version_cl > 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ac12_entrega_version
    ON public.aocr_entrega_final(solicitud_id,version_aocr,version_cl);
CREATE UNIQUE INDEX IF NOT EXISTS ux_ac12_entrega_event_key
    ON public.aocr_entrega_final(event_key);
CREATE INDEX IF NOT EXISTS ix_ac12_entrega_estado
    ON public.aocr_entrega_final(estado,updated_at);

CREATE TABLE IF NOT EXISTS public.aocr_entrega_documento (
    id BIGSERIAL PRIMARY KEY,
    entrega_id BIGINT NOT NULL REFERENCES public.aocr_entrega_final(id),
    documento_id INTEGER NOT NULL REFERENCES public.aocr_tbdocumento_generado(codigo_documento),
    tipo_documento VARCHAR(60) NOT NULL,
    version_documento INTEGER NOT NULL,
    nombre_archivo VARCHAR(255) NOT NULL,
    ruta_fisica TEXT NOT NULL,
    hash_sha256 VARCHAR(64) NOT NULL,
    tamanio BIGINT NOT NULL,
    mime_type VARCHAR(100) NOT NULL DEFAULT 'application/pdf',
    nombre_firmante VARCHAR(250),
    rol_firma VARCHAR(50) NOT NULL,
    fecha_firma TIMESTAMP,
    vigente BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_ac12_documento_tipo CHECK(tipo_documento IN ('RECONOCIMIENTO','CONDICIONES_LIMITACIONES')),
    CONSTRAINT ck_ac12_documento_pdf CHECK(mime_type='application/pdf' AND tamanio > 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_ac12_entrega_documento
    ON public.aocr_entrega_documento(entrega_id,tipo_documento,version_documento);
CREATE INDEX IF NOT EXISTS ix_ac12_documento_lookup
    ON public.aocr_entrega_documento(documento_id,entrega_id) WHERE vigente=TRUE;

CREATE TABLE IF NOT EXISTS public.aocr_entrega_destinatario (
    id BIGSERIAL PRIMARY KEY,
    entrega_id BIGINT NOT NULL REFERENCES public.aocr_entrega_final(id),
    tipo_destinatario VARCHAR(20) NOT NULL,
    usuario_id INTEGER NOT NULL REFERENCES public.usuario(idusuario),
    correo VARCHAR(255) NOT NULL,
    email_queue_id INTEGER NOT NULL REFERENCES public.email_queue(id),
    estado_bandeja VARCHAR(30) NOT NULL DEFAULT 'DISPONIBLE',
    estado_correo VARCHAR(40) NOT NULL DEFAULT 'ENCOLADO',
    message_id VARCHAR(255),
    ultimo_error TEXT,
    fecha_envio TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_ac12_destinatario_tipo CHECK(tipo_destinatario IN ('RT','INSPECTOR'))
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_ac12_destinatario_logico
    ON public.aocr_entrega_destinatario(entrega_id,tipo_destinatario,usuario_id);
CREATE INDEX IF NOT EXISTS ix_ac12_destinatario_bandeja
    ON public.aocr_entrega_destinatario(usuario_id,tipo_destinatario,estado_bandeja);
CREATE INDEX IF NOT EXISTS ix_ac12_destinatario_queue
    ON public.aocr_entrega_destinatario(email_queue_id);

CREATE TABLE IF NOT EXISTS public.aocr_entrega_intento (
    id BIGSERIAL PRIMARY KEY,
    entrega_id BIGINT NOT NULL REFERENCES public.aocr_entrega_final(id),
    email_queue_id INTEGER NOT NULL REFERENCES public.email_queue(id),
    estado VARCHAR(40) NOT NULL,
    detalle_error TEXT,
    message_id VARCHAR(255),
    fecha TIMESTAMP NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_ac12_intento_entrega
    ON public.aocr_entrega_intento(entrega_id,email_queue_id,fecha);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ac12_email_fisico
    ON public.email_queue(event_key) WHERE event_key LIKE 'ENTREGA_FINAL:%';

INSERT INTO public.seguridad_permiso(codigo,nombre,modulo,activo,creado_en,creado_por)
SELECT v.codigo,v.nombre,'ENTREGA_FINAL',TRUE,NOW(),'AC12'
FROM (VALUES
 ('ENTREGA_FINAL_SOLICITAR','Solicitar entrega final'),
 ('ENTREGA_FINAL_CONSULTAR','Consultar entrega institucional'),
 ('ENTREGA_FINAL_AUDITAR','Auditar entrega final')) v(codigo,nombre)
WHERE NOT EXISTS(SELECT 1 FROM public.seguridad_permiso p WHERE p.codigo=v.codigo);
UPDATE public.seguridad_permiso SET activo=TRUE,modulo='ENTREGA_FINAL',actualizado_en=NOW(),actualizado_por='AC12'
WHERE codigo IN ('ENTREGA_FINAL_SOLICITAR','ENTREGA_FINAL_CONSULTAR','ENTREGA_FINAL_AUDITAR');

INSERT INTO public.seguridad_rol_permiso(codigorol,id_permiso,activo,creado_en,creado_por)
SELECT r.codigorol,p.id_permiso,TRUE,NOW(),'AC12' FROM public.rol r CROSS JOIN public.seguridad_permiso p
WHERE regexp_replace(UPPER(TRIM(COALESCE(r.descripcion,''))),'[^A-Z0-9]+','_','g') IN ('DIRDAC','DIRECTOR_DIRDAC','DIRECTORDIRDAC')
  AND p.codigo='ENTREGA_FINAL_SOLICITAR'
  AND NOT EXISTS(SELECT 1 FROM public.seguridad_rol_permiso rp WHERE rp.codigorol=r.codigorol AND rp.id_permiso=p.id_permiso);

INSERT INTO public.seguridad_rol_permiso(codigorol,id_permiso,activo,creado_en,creado_por)
SELECT r.codigorol,p.id_permiso,TRUE,NOW(),'AC12' FROM public.rol r CROSS JOIN public.seguridad_permiso p
WHERE regexp_replace(UPPER(TRIM(COALESCE(r.descripcion,''))),'[^A-Z0-9]+','_','g') IN ('DIRDAC','DIRCAV','DCAV','COORDINADOR','COORDINACION')
  AND p.codigo='ENTREGA_FINAL_CONSULTAR'
  AND NOT EXISTS(SELECT 1 FROM public.seguridad_rol_permiso rp WHERE rp.codigorol=r.codigorol AND rp.id_permiso=p.id_permiso);

INSERT INTO public.seguridad_rol_permiso(codigorol,id_permiso,activo,creado_en,creado_por)
SELECT r.codigorol,p.id_permiso,TRUE,NOW(),'AC12' FROM public.rol r CROSS JOIN public.seguridad_permiso p
WHERE regexp_replace(UPPER(TRIM(COALESCE(r.descripcion,''))),'[^A-Z0-9]+','_','g') IN ('ADMINISTRADOR','ADMIN')
  AND p.codigo='ENTREGA_FINAL_AUDITAR'
  AND NOT EXISTS(SELECT 1 FROM public.seguridad_rol_permiso rp WHERE rp.codigorol=r.codigorol AND rp.id_permiso=p.id_permiso);

UPDATE public.seguridad_rol_permiso rp SET activo=TRUE,actualizado_en=NOW(),actualizado_por='AC12'
WHERE rp.id_permiso IN (SELECT id_permiso FROM public.seguridad_permiso WHERE codigo IN ('ENTREGA_FINAL_SOLICITAR','ENTREGA_FINAL_CONSULTAR','ENTREGA_FINAL_AUDITAR'));

COMMIT;
