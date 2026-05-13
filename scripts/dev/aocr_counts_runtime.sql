DROP TABLE IF EXISTS tmp_aocr_counts;
CREATE TEMP TABLE tmp_aocr_counts(table_name text, row_count bigint);
DO $$
DECLARE
    r record;
    c bigint;
BEGIN
    FOR r IN
        SELECT table_name
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_type = 'BASE TABLE'
        ORDER BY table_name
    LOOP
        EXECUTE format('SELECT count(*) FROM %I.%I', 'public', r.table_name) INTO c;
        INSERT INTO tmp_aocr_counts(table_name, row_count) VALUES (r.table_name, c);
    END LOOP;
END $$;
SELECT string_agg(format('%s|%s', table_name, row_count), E'\n' ORDER BY table_name) AS conteos
FROM tmp_aocr_counts;
