-- Solo para una base de pruebas vacía y desechable. Nunca ejecutar en AOCR.
CREATE TABLE public.aocr_or_orden (id integer PRIMARY KEY, codigo_solicitud integer, estado text,
    subtotal numeric(18,2), admin numeric(18,2), total numeric(18,2));
CREATE TABLE public.aocr_or_orden_detalle (id serial PRIMARY KEY, orden_id integer REFERENCES aocr_or_orden(id),
    concepto_id integer, concepto_codigo text, concepto_nombre text, descripcion text, cantidad integer,
    valor_unitario numeric(18,2), porcentaje_admin numeric(18,2), subtotal numeric(18,2), admin numeric(18,2),
    total_linea numeric(18,2), estado text, lugar_inspeccion text, provincia_inspeccion text);
CREATE TABLE public.aocr_tbsolicitud_estacion (id serial PRIMARY KEY, solicitud_id integer,
    estacion_codigo text, estacion_nombre text, fecha_inicio date NOT NULL, fecha_fin date NOT NULL,
    inspector_id integer, inspector_nombre text, inspeccion_id integer, estado text, version integer,
    activo boolean, observacion text, creado_en timestamp, creado_por integer, actualizado_en timestamp, actualizado_por integer);
INSERT INTO aocr_or_orden VALUES (1, 101, 'PAGADA', 100, 10, 110);
INSERT INTO aocr_or_orden_detalle VALUES (11, 1, 7, 'INSPECCION_EXT', 'Inspección', '', 1, 100, 10, 100, 10, 110, 'PENDIENTE', 'QUITO', NULL);
