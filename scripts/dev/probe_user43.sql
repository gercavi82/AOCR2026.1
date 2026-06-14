SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='aocr_tbsolicitud' AND column_name IN ('deleted_at','tecnico_responsable_cedula','codigo_tecnico');
SELECT idusuario, codigousuario, nombreusuario, rol FROM usuario WHERE idusuario=43 OR codigousuario LIKE '%inspect%' OR nombreusuario ILIKE '%inspect%' LIMIT 15;
SELECT table_name FROM information_schema.tables WHERE table_schema='public' AND table_name LIKE '%usuario%interno%' OR table_name LIKE '%rt%';
