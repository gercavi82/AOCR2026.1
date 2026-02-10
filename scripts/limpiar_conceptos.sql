-- Dejar solo los conceptos requeridos y eliminar el resto
delete from aocr_or_concepto where lower(trim(nombre)) not in (
    lower('Emisión AOCR'),
    lower('Renovación AOCR'),
    lower('Modificación AOCR (Inclusión aeronaves distinto modelo y tipo)'),
    lower('Modificación AOCR (Que no implique incremento de aeronaves)'),
    lower('Inspección requerida por el Operador Aéreo Extranjero'),
    lower('Viáticos a Sres. Inspectores')
);

-- Opcional: puedes ajustar los valores y descripciones con UPDATE si es necesario.