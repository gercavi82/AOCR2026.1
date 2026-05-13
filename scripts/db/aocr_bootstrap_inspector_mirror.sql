-- AOCR - Bootstrap del espejo local de inspectores
-- Uso: recupera la tabla public.aocr_tbinspectores si no existe y la siembra
--      con inspectores activos preservados en aocr_usuario_interno_rt.
-- Luego puede ejecutarse SyncAdmin/RunInspectoresRt para sincronizar desde DB2.

ROLLBACK;
BEGIN;

CREATE TABLE IF NOT EXISTS public.aocr_tbinspectores (
    id              SERIAL PRIMARY KEY,
    cedula          VARCHAR(20)  NOT NULL,
    nombre_completo VARCHAR(200),
    estado          VARCHAR(5),
    tipo            VARCHAR(10),
    created_at      TIMESTAMP    NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMP    NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_aocr_tbinspectores_cedula
    ON public.aocr_tbinspectores (LOWER(TRIM(cedula)));

CREATE INDEX IF NOT EXISTS idx_aocr_tbinspectores_cedula
    ON public.aocr_tbinspectores (cedula);

CREATE INDEX IF NOT EXISTS idx_aocr_tbinspectores_estado_tipo
    ON public.aocr_tbinspectores (estado, tipo);

COMMENT ON TABLE public.aocr_tbinspectores IS
    'Espejo local de DGACDAT.OPIAR2 (AS400). Se sincroniza vía SyncAdmin/RunInspectoresRt.';

INSERT INTO public.aocr_tbinspectores (
    cedula,
    nombre_completo,
    estado,
    tipo,
    created_at,
    updated_at
)
SELECT src.cedula,
       src.nombre_completo,
       src.estado,
       src.tipo,
       NOW(),
       NOW()
FROM (
    SELECT DISTINCT
           TRIM(COALESCE(NULLIF(identificacion, ''), NULLIF(codigo_usuario, ''))) AS cedula,
           TRIM(COALESCE(NULLIF(nombre_completo, ''), NULLIF(CONCAT_WS(' ', nombres, apellidos), ''), codigo_usuario)) AS nombre_completo,
           'AC' AS estado,
           UPPER(TRIM(COALESCE(NULLIF(tipo, ''), 'OPS'))) AS tipo
    FROM public.aocr_usuario_interno_rt
    WHERE UPPER(TRIM(COALESCE(rol_interno, ''))) LIKE '%INSPECTOR%'
      AND UPPER(TRIM(COALESCE(estado_as400, 'AC'))) = 'AC'
      AND TRIM(COALESCE(NULLIF(identificacion, ''), NULLIF(codigo_usuario, ''))) <> ''
) AS src
WHERE NOT EXISTS (
    SELECT 1
    FROM public.aocr_tbinspectores t
    WHERE LOWER(TRIM(t.cedula)) = LOWER(src.cedula)
);

SELECT COUNT(*) AS total_mirror,
       COUNT(*) FILTER (WHERE UPPER(TRIM(COALESCE(estado, ''))) = 'AC') AS activos,
       COUNT(*) FILTER (WHERE UPPER(TRIM(COALESCE(tipo, ''))) = 'OPS') AS ops
FROM public.aocr_tbinspectores;

SELECT cedula, nombre_completo, estado, tipo
FROM public.aocr_tbinspectores
ORDER BY nombre_completo;

COMMIT;