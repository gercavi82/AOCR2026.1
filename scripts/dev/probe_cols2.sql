SELECT column_name FROM information_schema.columns WHERE table_name='aocr_tbinspeccion' ORDER BY ordinal_position;
SELECT column_name FROM information_schema.columns WHERE table_name='aocr_tborden_recaudacion' ORDER BY ordinal_position LIMIT 20;
SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name LIKE '%historial%' ORDER BY table_name, ordinal_position;
