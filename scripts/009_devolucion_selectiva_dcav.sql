BEGIN;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='aocr_tbobservacion' AND column_name='mensaje' AND data_type='text') THEN
    RAISE EXCEPTION 'Preflight fallido: public.aocr_tbobservacion.mensaje TEXT no existe';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='aocr_tbdocumento_generado' AND column_name='version') THEN
    RAISE EXCEPTION 'Preflight fallido: versionado documental no disponible';
  END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_aocr_observacion_dcav_documental
  ON public.aocr_tbobservacion(codigo_solicitud,fecha_registro DESC,codigo_observacion DESC)
  WHERE mensaje LIKE '{%DCAV_DOCUMENTAL_V1%';

COMMENT ON INDEX public.idx_aocr_observacion_dcav_documental IS
  'Lectura selectiva de observaciones documentales DCAV almacenadas en mensaje JSON; no altera datos ni esquema funcional.';

COMMIT;
