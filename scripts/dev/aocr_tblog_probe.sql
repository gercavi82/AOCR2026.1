SELECT column_name
FROM information_schema.columns
WHERE table_schema='public' AND table_name='aocr_tblog'
ORDER BY ordinal_position;

SELECT *
FROM public.aocr_tblog
ORDER BY 1 DESC
LIMIT 5;
