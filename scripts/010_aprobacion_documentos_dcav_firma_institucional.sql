BEGIN;

DO $$
DECLARE v_rol integer;
BEGIN
  SELECT codigorol INTO v_rol FROM public.rol WHERE UPPER(TRIM(descripcion))='DIRECCION' ORDER BY codigorol LIMIT 1;
  IF v_rol IS NULL THEN RAISE EXCEPTION 'Preflight fallido: no existe el rol firmante real Direccion'; END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='aocr_proceso_estado' AND column_name='version') THEN RAISE EXCEPTION 'Preflight fallido: estado central sin version optimista'; END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='aocr_tbdocumento_generado' AND column_name='vigente') THEN RAISE EXCEPTION 'Preflight fallido: documento generado sin control vigente'; END IF;
  RAISE NOTICE 'Rol firmante resuelto: codigorol=%, descripcion=Direccion',v_rol;
END $$;

CREATE INDEX IF NOT EXISTS idx_aocr_estado_firma_institucional
 ON public.aocr_proceso_estado(fecha_estado,solicitud_id,inspeccion_id)
 WHERE activo=TRUE AND estado_actual='PENDIENTE_FIRMA_DIRDAC';

CREATE INDEX IF NOT EXISTS idx_aocr_hist_aprobacion_documentos_dcav
 ON public.aocr_proceso_estado_historial(solicitud_id,inspeccion_id,fecha_creacion DESC,id DESC)
 WHERE accion='APROBAR_DOCUMENTOS_DCAV' AND estado_nuevo='PENDIENTE_FIRMA_DIRDAC';

COMMIT;
