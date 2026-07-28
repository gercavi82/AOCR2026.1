BEGIN;

CREATE TABLE IF NOT EXISTS aocr_tbparametro_descripcion_bak_20260727
(
    clave                VARCHAR(150) PRIMARY KEY,
    descripcion_anterior TEXT,
    respaldado_en        TIMESTAMP NOT NULL DEFAULT NOW()
);

INSERT INTO aocr_tbparametro_descripcion_bak_20260727 (clave, descripcion_anterior)
SELECT clave, descripcion
FROM aocr_tbparametro
WHERE clave IN
(
    'CALCULO_PORCENTAJE_GASTOS_ADMIN',
    'CALCULO_VALOR_POR_DIA_VIATICO',
    'CALCULO_VALOR_POR_ESTACION',
    'DIAS_VENCIMIENTO_CERTIFICADO',
    'EMAIL_NOTIFICACIONES',
    'PORCENTAJE_ADMIN_VIATICOS',
    'SISTEMA_NOMBRE',
    'TAMANO_MAX_ARCHIVO_MB',
    'TARIFA_EMI_AOCR',
    'TARIFA_INSPECCION_EXT',
    'TARIFA_MOD_AOCR_INC',
    'TARIFA_MOD_AOCR_SIN_INC',
    'TARIFA_REN_AOCR',
    'TARIFA_VIATICOS_INSPECTOR',
    'VIGENCIA_CERTIFICADO_ANIOS'
)
ON CONFLICT (clave) DO NOTHING;

WITH correcciones(clave, descripcion) AS
(
    VALUES
        ('CALCULO_PORCENTAJE_GASTOS_ADMIN', 'Porcentaje de gastos administrativos.'),
        ('CALCULO_VALOR_POR_DIA_VIATICO',   'Valor diario de viáticos para inspectores.'),
        ('CALCULO_VALOR_POR_ESTACION',      'Valor por estación para el cálculo de inspecciones.'),
        ('DIAS_VENCIMIENTO_CERTIFICADO',    'Días de vigencia del certificado.'),
        ('EMAIL_NOTIFICACIONES',            'Correo electrónico utilizado para las notificaciones del sistema.'),
        ('PORCENTAJE_ADMIN_VIATICOS',       'Porcentaje de gastos administrativos aplicado a viáticos.'),
        ('SISTEMA_NOMBRE',                  'Nombre oficial del sistema.'),
        ('TAMANO_MAX_ARCHIVO_MB',           'Tamaño máximo permitido por archivo, en MB.'),
        ('TARIFA_EMI_AOCR',                 'Tarifa para la emisión de una AOCR.'),
        ('TARIFA_INSPECCION_EXT',           'Tarifa por estación para inspecciones de operadores aéreos extranjeros.'),
        ('TARIFA_MOD_AOCR_INC',             'Tarifa para modificar una AOCR con inclusión de aeronaves de distinto modelo o tipo.'),
        ('TARIFA_MOD_AOCR_SIN_INC',         'Tarifa para modificar una AOCR sin incremento de aeronaves.'),
        ('TARIFA_REN_AOCR',                 'Tarifa para la renovación de una AOCR.'),
        ('TARIFA_VIATICOS_INSPECTOR',       'Tarifa diaria de viáticos para inspectores.'),
        ('VIGENCIA_CERTIFICADO_ANIOS',      'Años de vigencia del certificado.')
)
UPDATE aocr_tbparametro p
SET descripcion = c.descripcion,
    updatedat = NOW(),
    updatedby = 'SYSTEM_CORRECCION_TEXTO'
FROM correcciones c
WHERE p.clave = c.clave;

COMMIT;
