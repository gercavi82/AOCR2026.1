ALTER TABLE public.aocr_or_orden_detalle ADD COLUMN IF NOT EXISTS lugar_inspeccion VARCHAR(50);
ALTER TABLE public.aocr_or_orden_detalle ADD COLUMN IF NOT EXISTS provincia_inspeccion VARCHAR(150);
