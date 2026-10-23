-- Contactos confirmados para las copias finales. No inventa ni precarga correos.
BEGIN;
SET LOCAL lock_timeout='5s';
CREATE TABLE IF NOT EXISTS public.aocr_jefatura_aeropuerto_correo (
    estacion_codigo VARCHAR(10) NOT NULL,
    correo VARCHAR(254) NOT NULL,
    nombre VARCHAR(200) NOT NULL DEFAULT '',
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    PRIMARY KEY(estacion_codigo,correo),
    CHECK(estacion_codigo=UPPER(TRIM(estacion_codigo))),
    CHECK(POSITION('@' IN correo)>1)
);
-- Cargar únicamente correos institucionales confirmados de las jefaturas.
-- INSERT INTO public.aocr_jefatura_aeropuerto_correo(estacion_codigo,correo,nombre)
-- VALUES ('UIO','correo_confirmado','Jefatura UIO') ON CONFLICT DO NOTHING;
COMMIT;
