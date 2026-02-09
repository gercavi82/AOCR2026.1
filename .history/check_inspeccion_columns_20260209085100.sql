-- Consultar estructura real de aocr_tbinspeccion
SELECT 
    column_name, 
    data_type, 
    character_maximum_length,
    is_nullable
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'aocr_tbinspeccion'
ORDER BY ordinal_position;
