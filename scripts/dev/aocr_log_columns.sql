SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN ('aocr_tblog','aocr_sync_log','aocr_tb_sync_log','sync_log')
ORDER BY table_name, ordinal_position;
