SELECT table_schema, table_name
FROM information_schema.tables
WHERE table_schema = 'mirror_raw'
  AND table_name IN ('usuarc','opcarc','ciaarc')
ORDER BY table_name;

SELECT NULLIF(BTRIM(usucod), '') AS usucod,
       NULLIF(BTRIM(usunum), '') AS usunum,
       NULLIF(BTRIM(usuced), '') AS usuced,
       _mirror_synced_at
FROM mirror_raw.usuarc
WHERE UPPER(BTRIM(COALESCE(usucod, ''))) IN ('GACAJAS','GERMAN ALBERTO')
ORDER BY _mirror_synced_at DESC
LIMIT 10;

SELECT NULLIF(BTRIM(opccod), '') AS opccod,
       NULLIF(BTRIM(opcco1), '') AS opcco1,
       NULLIF(BTRIM(opcnom), '') AS opcnom,
       NULLIF(BTRIM(opcruc), '') AS opcruc
FROM mirror_raw.opcarc
WHERE UPPER(BTRIM(COALESCE(opccod, ''))) = 'ONTA'
   OR UPPER(BTRIM(COALESCE(opcco1, ''))) = 'ONTA'
   OR UPPER(BTRIM(COALESCE(opcnom, ''))) LIKE '%ONTARIO%'
LIMIT 20;

SELECT NULLIF(BTRIM(ciacod), '') AS ciacod,
       NULLIF(BTRIM(cianom), '') AS cianom,
       NULLIF(BTRIM(ciano1), '') AS ciano1,
       NULLIF(BTRIM(ciaruc), '') AS ciaruc
FROM mirror_raw.ciaarc
WHERE UPPER(BTRIM(COALESCE(ciacod, ''))) = 'ONTA'
   OR UPPER(BTRIM(COALESCE(cianom, ''))) LIKE '%ONTARIO%'
   OR UPPER(BTRIM(COALESCE(ciano1, ''))) LIKE '%ONTARIO%'
LIMIT 20;
