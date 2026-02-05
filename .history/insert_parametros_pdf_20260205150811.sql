-- Script para insertar parámetros de configuración de PDFs
-- Elimina textos hardcodeados en PdfGeneratorService

-- Títulos y textos del PDF de Orden de Recaudación
INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('PDF_TITULO_ORDEN', 'ORDEN DE RECAUDACIÓN', 'Título principal del PDF de Orden de Recaudación', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('PDF_SUBTITULO', 'Sistema AOCR - Autoridad de Aviación Civil', 'Subtítulo del PDF con nombre de la institución', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('PDF_TEXTO_GENERADO', 'Documento generado automáticamente el {0:dd/MM/yyyy HH:mm:ss}', 'Texto del footer indicando fecha de generación automática', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('PDF_TEXTO_VALIDEZ', 'Este documento es válido sin firma ni sello.', 'Texto del footer sobre validez del documento', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

-- Configuraciones adicionales de PDF
INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('PDF_COLOR_TITULO', '#1B4F72', 'Color hexadecimal para títulos en PDFs', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('PDF_FUENTE_FAMILIA', 'Arial, sans-serif', 'Familia de fuentes para PDFs', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('PDF_TAMAÑO_FUENTE', '12px', 'Tamaño base de fuente para PDFs', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

-- Configuración de moneda por defecto (que estaba hardcodeada como "USD")
INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('MONEDA_DEFECTO', 'USD', 'Moneda por defecto para pagos y órdenes', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('SIMBOLO_MONEDA', '$', 'Símbolo de la moneda para mostrar en documentos', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

-- Verificar que se insertaron correctamente
SELECT clave, valor, descripcion, activo
FROM parametros 
WHERE clave IN (
    'PDF_TITULO_ORDEN', 
    'PDF_SUBTITULO', 
    'PDF_TEXTO_GENERADO', 
    'PDF_TEXTO_VALIDEZ',
    'PDF_COLOR_TITULO',
    'PDF_FUENTE_FAMILIA',
    'PDF_TAMAÑO_FUENTE',
    'MONEDA_DEFECTO',
    'SIMBOLO_MONEDA'
)
ORDER BY clave;