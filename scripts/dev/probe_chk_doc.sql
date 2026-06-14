SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'chk_estado_documento';
