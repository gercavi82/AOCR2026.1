SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN ('usuario','usuario_as400','usuario_as400_adicional','aocr_usuario_compania_rt','contribuyentes')
ORDER BY table_name, ordinal_position;
