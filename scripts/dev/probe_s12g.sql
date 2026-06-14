SELECT COUNT(*) AS total_docs FROM aocr_tbdocumento WHERE codigo_solicitud=12;
SELECT COUNT(*) AS revisiones FROM aocr_tbrevision_documental WHERE codigo_solicitud=12;
SELECT column_name, data_type FROM information_schema.columns WHERE table_name='aocr_tbrevision_documental' ORDER BY ordinal_position;
