WITH roles_normalizados AS
(
    SELECT
        codigorol,
        activo,
        CASE
            WHEN UPPER(TRIM(descripcion)) = 'ADMINISTRADOR' THEN 'Administrador'
            WHEN UPPER(TRIM(descripcion)) IN ('RT', 'SOLICITANTE', 'OPERADOR') THEN 'RT'
            WHEN UPPER(TRIM(descripcion)) IN ('INSPECTOR', 'TECNICO', 'EVALUADORTECNICO') THEN 'Inspector'
            WHEN UPPER(TRIM(descripcion)) IN ('COORDINADORINSPECCIONES', 'COORDINADOR', 'COORDINACION') THEN 'Coordinador de Inspecciones'
            WHEN UPPER(TRIM(descripcion)) IN ('COORDINACIONLEGAL', 'COORDINADORLEGAL') THEN 'Coordinación Legal'
            WHEN UPPER(TRIM(descripcion)) = 'COORDINADORFINANCIERO' THEN 'Coordinación Financiera'
            WHEN UPPER(TRIM(descripcion)) IN ('FINANCIERO', 'DIRECTORFINANCIERO') THEN 'Financiero'
            WHEN UPPER(TRIM(descripcion)) IN ('JEFATURATECNICA', 'DIRECCION', 'DIRECTORGENERAL', 'DIRECCIONJEFATURA', 'DIRECCIONJEFATURATECNICA') THEN 'Dirección / Jefatura técnica'
            WHEN UPPER(TRIM(descripcion)) = 'DIRDAC' THEN 'DIRDAC'
            WHEN UPPER(TRIM(descripcion)) IN ('DCAV', 'DIRECTOR_CERTIFICACIONES_DCAV', 'DIRECTORCERTIFICACIONESDCAV') THEN 'DCAV'
            ELSE NULL
        END AS nombre_canonico,
        ROW_NUMBER() OVER
        (
            PARTITION BY CASE
                WHEN UPPER(TRIM(descripcion)) = 'ADMINISTRADOR' THEN 'Administrador'
                WHEN UPPER(TRIM(descripcion)) IN ('RT', 'SOLICITANTE', 'OPERADOR') THEN 'RT'
                WHEN UPPER(TRIM(descripcion)) IN ('INSPECTOR', 'TECNICO', 'EVALUADORTECNICO') THEN 'Inspector'
                WHEN UPPER(TRIM(descripcion)) IN ('COORDINADORINSPECCIONES', 'COORDINADOR', 'COORDINACION') THEN 'Coordinador de Inspecciones'
                WHEN UPPER(TRIM(descripcion)) IN ('COORDINACIONLEGAL', 'COORDINADORLEGAL') THEN 'Coordinación Legal'
                WHEN UPPER(TRIM(descripcion)) = 'COORDINADORFINANCIERO' THEN 'Coordinación Financiera'
                WHEN UPPER(TRIM(descripcion)) IN ('FINANCIERO', 'DIRECTORFINANCIERO') THEN 'Financiero'
                WHEN UPPER(TRIM(descripcion)) IN ('JEFATURATECNICA', 'DIRECCION', 'DIRECTORGENERAL', 'DIRECCIONJEFATURA', 'DIRECCIONJEFATURATECNICA') THEN 'Dirección / Jefatura técnica'
                WHEN UPPER(TRIM(descripcion)) = 'DIRDAC' THEN 'DIRDAC'
                WHEN UPPER(TRIM(descripcion)) IN ('DCAV', 'DIRECTOR_CERTIFICACIONES_DCAV', 'DIRECTORCERTIFICACIONESDCAV') THEN 'DCAV'
                ELSE NULL
            END
            ORDER BY CASE
                WHEN UPPER(TRIM(descripcion)) IN ('ADMINISTRADOR', 'RT', 'INSPECTOR', 'COORDINADORINSPECCIONES', 'COORDINACIONLEGAL', 'COORDINADORFINANCIERO', 'FINANCIERO', 'JEFATURATECNICA', 'DIRDAC', 'DCAV') THEN 0
                ELSE 1
            END, codigorol
        ) AS orden
    FROM rol
    WHERE activo = TRUE
)
SELECT
    codigorol AS "CodigoRol",
    nombre_canonico AS "Descripcion",
    activo AS "Activo"
FROM roles_normalizados
WHERE nombre_canonico IS NOT NULL
  AND orden = 1
ORDER BY CASE nombre_canonico
    WHEN 'RT' THEN 1
    WHEN 'Inspector' THEN 2
    WHEN 'Coordinador de Inspecciones' THEN 3
    WHEN 'Coordinación Legal' THEN 4
    WHEN 'Coordinación Financiera' THEN 5
    WHEN 'Financiero' THEN 6
    WHEN 'Dirección / Jefatura técnica' THEN 7
    WHEN 'DIRDAC' THEN 8
    WHEN 'DCAV' THEN 9
    WHEN 'Administrador' THEN 10
    ELSE 99
END;
