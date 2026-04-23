-- 20260423_actualizar_tarifas_aocr_oficiales.sql
-- Actualiza/crea parametros oficiales de tarifas AOCR
-- Idempotente para PostgreSQL (tabla aocr_tbparametro)

DO $$
BEGIN
    UPDATE aocr_tbparametro
       SET valor = '3300.00',
           descripcion = 'Tarifa para Emision AOCR',
           activo = TRUE,
           updatedat = NOW(),
           updatedby = 0,
           deletedat = NULL,
           deletedby = NULL
     WHERE clave = 'TARIFA_EMI_AOCR';

    IF NOT FOUND THEN
        INSERT INTO aocr_tbparametro (clave, valor, descripcion, activo, createdat, createdby)
        VALUES ('TARIFA_EMI_AOCR', '3300.00', 'Tarifa para Emision AOCR', TRUE, NOW(), 0);
    END IF;
END $$;

DO $$
BEGIN
    UPDATE aocr_tbparametro
       SET valor = '3300.00',
           descripcion = 'Tarifa para Renovacion AOCR',
           activo = TRUE,
           updatedat = NOW(),
           updatedby = 0,
           deletedat = NULL,
           deletedby = NULL
     WHERE clave = 'TARIFA_REN_AOCR';

    IF NOT FOUND THEN
        INSERT INTO aocr_tbparametro (clave, valor, descripcion, activo, createdat, createdby)
        VALUES ('TARIFA_REN_AOCR', '3300.00', 'Tarifa para Renovacion AOCR', TRUE, NOW(), 0);
    END IF;
END $$;

DO $$
BEGIN
    UPDATE aocr_tbparametro
       SET valor = '1600.00',
           descripcion = 'Tarifa para Modificacion AOCR con inclusion de aeronaves distinto modelo y tipo',
           activo = TRUE,
           updatedat = NOW(),
           updatedby = 0,
           deletedat = NULL,
           deletedby = NULL
     WHERE clave = 'TARIFA_MOD_AOCR_INC';

    IF NOT FOUND THEN
        INSERT INTO aocr_tbparametro (clave, valor, descripcion, activo, createdat, createdby)
        VALUES ('TARIFA_MOD_AOCR_INC', '1600.00', 'Tarifa para Modificacion AOCR con inclusion de aeronaves distinto modelo y tipo', TRUE, NOW(), 0);
    END IF;
END $$;

DO $$
BEGIN
    UPDATE aocr_tbparametro
       SET valor = '80.00',
           descripcion = 'Tarifa para Modificacion AOCR que no implique incremento de aeronaves',
           activo = TRUE,
           updatedat = NOW(),
           updatedby = 0,
           deletedat = NULL,
           deletedby = NULL
     WHERE clave = 'TARIFA_MOD_AOCR_SIN_INC';

    IF NOT FOUND THEN
        INSERT INTO aocr_tbparametro (clave, valor, descripcion, activo, createdat, createdby)
        VALUES ('TARIFA_MOD_AOCR_SIN_INC', '80.00', 'Tarifa para Modificacion AOCR que no implique incremento de aeronaves', TRUE, NOW(), 0);
    END IF;
END $$;

DO $$
BEGIN
    UPDATE aocr_tbparametro
       SET valor = '500.00',
           descripcion = 'Tarifa por estacion para inspeccion requerida por Operador Aereo Extranjero',
           activo = TRUE,
           updatedat = NOW(),
           updatedby = 0,
           deletedat = NULL,
           deletedby = NULL
     WHERE clave = 'TARIFA_INSPECCION_EXT';

    IF NOT FOUND THEN
        INSERT INTO aocr_tbparametro (clave, valor, descripcion, activo, createdat, createdby)
        VALUES ('TARIFA_INSPECCION_EXT', '500.00', 'Tarifa por estacion para inspeccion requerida por Operador Aereo Extranjero', TRUE, NOW(), 0);
    END IF;
END $$;

DO $$
BEGIN
    UPDATE aocr_tbparametro
       SET valor = '80.00',
           descripcion = 'Tarifa de viaticos por dia para inspectores',
           activo = TRUE,
           updatedat = NOW(),
           updatedby = 0,
           deletedat = NULL,
           deletedby = NULL
     WHERE clave = 'TARIFA_VIATICOS_INSPECTOR';

    IF NOT FOUND THEN
        INSERT INTO aocr_tbparametro (clave, valor, descripcion, activo, createdat, createdby)
        VALUES ('TARIFA_VIATICOS_INSPECTOR', '80.00', 'Tarifa de viaticos por dia para inspectores', TRUE, NOW(), 0);
    END IF;
END $$;

DO $$
BEGIN
    UPDATE aocr_tbparametro
       SET valor = '8.00',
           descripcion = 'Porcentaje de gastos administrativos sobre viaticos',
           activo = TRUE,
           updatedat = NOW(),
           updatedby = 0,
           deletedat = NULL,
           deletedby = NULL
     WHERE clave = 'PORCENTAJE_ADMIN_VIATICOS';

    IF NOT FOUND THEN
        INSERT INTO aocr_tbparametro (clave, valor, descripcion, activo, createdat, createdby)
        VALUES ('PORCENTAJE_ADMIN_VIATICOS', '8.00', 'Porcentaje de gastos administrativos sobre viaticos', TRUE, NOW(), 0);
    END IF;
END $$;

-- Reflejar en catalogo de conceptos para que el combo muestre valores vigentes de inmediato.
UPDATE aocr_or_concepto SET valor_base = 3300.00 WHERE codigo = 'EMI_AOCR';
UPDATE aocr_or_concepto SET valor_base = 3300.00 WHERE codigo = 'REN_AOCR';
UPDATE aocr_or_concepto SET valor_base = 1600.00 WHERE codigo = 'MOD_AOCR_INC';
UPDATE aocr_or_concepto SET valor_base = 80.00 WHERE codigo = 'MOD_AOCR_SIN_INC';
UPDATE aocr_or_concepto SET valor_base = 500.00 WHERE codigo = 'INSPECCION_EXT';
UPDATE aocr_or_concepto SET valor_base = 80.00, porcentaje_admin = 8.00 WHERE codigo = 'VIATICOS_INSPECTOR';
