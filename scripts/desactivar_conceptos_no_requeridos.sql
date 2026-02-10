-- Marcar como inactivos los conceptos que no son requeridos
UPDATE aocr_or_concepto SET activo = false
WHERE lower(trim(nombre)) NOT IN (
    lower('Emisión AOCR'),
    lower('Renovación AOCR'),
    lower('Modificación AOCR (Inclusión aeronaves distinto modelo y tipo)'),
    lower('Modificación AOCR (Que no implique incremento de aeronaves)'),
    lower('Inspección requerida por el Operador Aéreo Extranjero'),
    lower('Viáticos a Sres. Inspectores')
);

-- Opcional: puedes ajustar los valores y descripciones con UPDATE si es necesario.