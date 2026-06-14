SELECT COUNT(*) AS docs_revision FROM aocr_tbdocumento d WHERE d.codigo_solicitud=12 AND d.deleted_at IS NULL;
SELECT COUNT(*) AS revisiones FROM aocr_tbrevision_documental WHERE codigo_solicitud=12;
SELECT decision, COUNT(*) FROM aocr_tbrevision_documental WHERE codigo_solicitud=12 GROUP BY decision;
