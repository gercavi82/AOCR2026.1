SELECT NULLIF(BTRIM(usucod), '') AS codigo_usuario,
       NULLIF(BTRIM(usunum), '') AS ruc,
       NULLIF(BTRIM(usuced), '') AS cedula,
       _mirror_synced_at
FROM mirror_raw.usuarc u
WHERE COALESCE(u._is_deleted, false) = false
  AND UPPER(BTRIM(COALESCE(u.usucod, ''))) = ANY(ARRAY['GACAJAS','GERMAN ALBERTO'])
  AND (
         NULLIF(BTRIM(u.usunum), '') IS NOT NULL
      OR NULLIF(BTRIM(u.usuced), '') IS NOT NULL
  )
ORDER BY u._mirror_synced_at DESC
LIMIT 1;
