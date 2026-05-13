SELECT column_name
FROM information_schema.columns
WHERE table_schema='mirror_raw' AND table_name='usuarc'
ORDER BY ordinal_position;
