"""Inspección de metadatos; usa AOCRConnection sin imprimir credenciales ni datos personales."""
import json
from pathlib import Path
import xml.etree.ElementTree as ET
import psycopg2

ROOT = Path(__file__).resolve().parents[2]
config = ET.parse(ROOT / "CapaPresentacion/Web.config")
node = config.find(".//connectionStrings/add[@name='AOCRConnection']")
if node is None:
    raise SystemExit("AOCRConnection no configurada")
parts = dict(part.strip().lower().split("=", 1)
             for part in node.attrib["connectionString"].split(";") if "=" in part)
keys = {"host": "host", "server": "host", "port": "port", "database": "dbname",
        "username": "user", "user id": "user", "password": "password"}
tables = ["aocr_tbsolicitud", "aocr_revision_documental_coordinador",
          "aocr_tbhistorialestado", "aocr_tbhistorial_estado", "aocr_proceso_estado",
          "aocr_evento_workflow", "aocr_tbdocumento_generado", "aocr_tbfirma_documento",
          "aocr_tbcondiciones_limitaciones", "aocr_entrega_final", "aocr_entrega_documento",
          "aocr_entrega_destinatario", "email_queue", "aocr_tbnotificacion", "usuario",
          "usuario_rol", "rol"]
try:
    connection = psycopg2.connect(**{keys[k]: v for k, v in parts.items() if k in keys}, connect_timeout=5)
    connection.set_session(readonly=True)
    with connection.cursor() as cursor:
        cursor.execute("SET LOCAL statement_timeout = '10s'")
        cursor.execute("""SELECT table_name,column_name,data_type,is_nullable
            FROM information_schema.columns WHERE table_schema='public'
            AND table_name=ANY(%s) ORDER BY table_name,ordinal_position""", (tables,))
        columns = cursor.fetchall()
        cursor.execute("""SELECT tablename,indexname,indexdef FROM pg_indexes
            WHERE schemaname='public' AND tablename=ANY(%s) ORDER BY tablename,indexname""", (tables,))
        indexes = cursor.fetchall()
    connection.rollback()
    connection.close()
    report = {"mode": "read-only", "requested_tables": tables, "columns": columns, "indexes": indexes,
              "missing_tables": sorted(set(tables) - {row[0] for row in columns})}
    Path(__file__).with_name("schema.json").write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print("Metadatos guardados; tablas ausentes:", ", ".join(report["missing_tables"]) or "ninguna")
except Exception as error:
    raise SystemExit("Inspección no disponible: " + type(error).__name__)
