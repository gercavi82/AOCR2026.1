-- =====================================================
-- VALIDACIÓN POST-FIX: Email Queue Schema
-- Ejecuta estas queries para confirmar que todo está OK
-- =====================================================

-- 1️⃣ VERIFICAR COLUMNAS CRÍTICAS
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns 
WHERE table_schema = 'public' AND table_name = 'email_queue'
ORDER BY ordinal_position;

-- 2️⃣ VERIFICAR ÍNDICES
SELECT indexname, indexdef
FROM pg_indexes 
WHERE tablename = 'email_queue'
ORDER BY indexname;

-- 3️⃣ VERIFICAR TABLA DE ADJUNTOS
SELECT column_name, data_type
FROM information_schema.columns 
WHERE table_schema = 'public' AND table_name = 'email_attachment'
ORDER BY ordinal_position;

-- 4️⃣ CONTAR REGISTROS EN COLA
SELECT 
    COUNT(*) as total_registros,
    COUNT(CASE WHEN status = 'PENDIENTE' THEN 1 END) as pendientes,
    COUNT(CASE WHEN status = 'ENVIANDO' THEN 1 END) as enviando,
    COUNT(CASE WHEN status = 'ENVIADO' THEN 1 END) as enviados,
    COUNT(CASE WHEN status = 'ERROR' THEN 1 END) as errores
FROM public.email_queue;

-- 5️⃣ VER ÚLTIMOS EMAILS EN COLA (útil para diagnosticar)
SELECT 
    id, 
    to_address, 
    subject, 
    status, 
    intentos, 
    error_message, 
    created_at, 
    proximo_intento
FROM public.email_queue
ORDER BY created_at DESC
LIMIT 10;

-- 6️⃣ TEST RÁPIDO: Insertar un registro de prueba
-- (comentar si no quieres insertar)
/*
INSERT INTO public.email_queue 
    (to_address, subject, body, status, created_at, proximo_intento, tipo_notificacion)
VALUES 
    ('test@aviacioncivil.gob.ec', 'Test Fix', 'Email queue fix validación', 'PENDIENTE', NOW(), NOW(), 'TEST');
*/

-- 7️⃣ RESULTADO FINAL
SELECT 
    CASE 
        WHEN COUNT(*) = 0 THEN 'Schema OK pero sin datos'
        ELSE 'Schema OK con ' || COUNT(*) || ' registros'
    END as estado
FROM information_schema.tables 
WHERE table_schema = 'public' AND table_name = 'email_queue';
