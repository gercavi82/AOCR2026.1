SELECT table_name, column_name FROM information_schema.columns WHERE table_schema='public' AND table_name LIKE '%revision%' ORDER BY table_name, ordinal_position LIMIT 40;
