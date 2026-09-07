"""Pruebas AC-07 en PostgreSQL desechable. Requiere psycopg2 y pruebas compiladas.

Usa la conexiÃ³n de Web.config solo para crear/eliminar una base aocr_ac07_test_<uuid>.
Nunca aplica migraciones ni escribe en la base de la aplicaciÃ³n.
"""
import os
from pathlib import Path
import re
import subprocess
import uuid
import xml.etree.ElementTree as ET

import psycopg2
from psycopg2 import sql

ROOT = Path(__file__).resolve().parents[1]


def main():
    node = ET.parse(ROOT / "CapaPresentacion/Web.config").getroot().find(
        "connectionStrings/add[@name='AOCRConnection']")
    fields = dict(part.split("=", 1) for part in node.get("connectionString").split(";") if "=" in part)
    fields = {k.strip().lower(): v.strip() for k, v in fields.items()}
    args = dict(host=fields.get("host", fields.get("server")), port=fields.get("port", "5432"),
                dbname=fields.get("database"), user=fields.get("username", fields.get("user id")),
                password=fields.get("password"), connect_timeout=10)
    database = "aocr_ac07_test_" + uuid.uuid4().hex
    admin = psycopg2.connect(**args)
    admin.autocommit = True
    created = False
    try:
        with admin.cursor() as cur:
            cur.execute(sql.SQL("CREATE DATABASE {}").format(sql.Identifier(database)))
        created = True
        args["dbname"] = database
        with psycopg2.connect(**args) as conn:
            with conn.cursor() as cur:
                cur.execute("""
                    CREATE TABLE public.aocr_tbinspeccion (
                        codigo_inspeccion integer PRIMARY KEY, codigo_solicitud integer NOT NULL);
                    CREATE TABLE public.aocr_tbsolicitud_estacion (
                        id integer PRIMARY KEY, solicitud_id integer NOT NULL,
                        estacion_codigo text, estacion_nombre text, activo boolean DEFAULT true,
                        inspeccion_id integer, inspector_id integer);
                    INSERT INTO public.aocr_tbinspeccion VALUES (800,700),(801,700),(802,701),(803,702);
                    INSERT INTO public.aocr_tbsolicitud_estacion(id,solicitud_id,estacion_codigo,estacion_nombre)
                    VALUES (1,700,'A','EstaciÃ³n A'),(2,700,'B','EstaciÃ³n B'),(3,700,'C','EstaciÃ³n C'),
                           (4,701,'D','EstaciÃ³n D'),(5,701,'E','EstaciÃ³n E'),(6,702,'F','EstaciÃ³n F');
                """)
                source = (ROOT / "CapaDatos/DAOs/ListaVerificacionOperacionalEaeDAO.cs").read_text(encoding="utf-8-sig")
                ddl = re.search(r"CREATE TABLE IF NOT EXISTS public.aocr_tblv_operacional_eae\s*\(.*?\);", source, re.S).group()
                for col in ("solicitud_id", "estacion_id", "tipo_lista", "vigente"):
                    ddl = re.sub(r"^\s*" + col + r" [^\n]*\n", "", ddl, flags=re.M)
                cur.execute(ddl)
                cur.execute("""INSERT INTO public.aocr_tblv_operacional_eae
                    (codigo_inspeccion,version,items_json,firmado_tecnico,finalizado)
                    VALUES (802,1,'[{"Codigo":"historico","PruebasNotasComentarios":"Conservar"}]',true,true),
                           (803,1,'[]',false,false),(803,2,'[]',false,false);""")
        migration = (ROOT / "scripts/sql/20260907_ac07_aislamiento_lv.sql").read_text(encoding="utf-8-sig")
        with psycopg2.connect(**args) as conn:
            with conn.cursor() as cur:
                cur.execute(migration)
                cur.execute("SELECT codigo_lv,estacion_id,vigente,items_json,firmado_tecnico FROM public.aocr_tblv_operacional_eae ORDER BY codigo_lv")
                first = cur.fetchall()
                assert first[0][1] is None and first[0][4] and "Conservar" in first[0][3]
                assert first[1][1] == 6 and not first[1][2] and first[2][2]
                cur.execute(migration)
                cur.execute("SELECT codigo_lv,estacion_id,vigente,items_json,firmado_tecnico FROM public.aocr_tblv_operacional_eae ORDER BY codigo_lv")
                assert cur.fetchall() == first, "La migraciÃ³n debe ser idempotente"
        print("MigraciÃ³n: histÃ³rico ambiguo conservado, asociaciÃ³n Ãºnica, versiones e idempotencia OK", flush=True)
        # Npgsql quoting preserves special characters; never print credentials.
        def quote(value):
            return '"' + str(value).replace('"', '""') + '"'
        env = dict(os.environ)
        env["AOCR_AC07_TEST_CONNECTION"] = ";".join(
            key + "=" + quote(value) for key, value in {
                "Host": args["host"], "Port": args["port"], "Database": database,
                "Username": args["user"], "Password": args["password"], "Pooling": "false"}.items())
        runner = os.environ.get("VSTEST_CONSOLE")
        if not runner:
            candidates = list(Path(os.environ["ProgramFiles"]).glob(
                "Microsoft Visual Studio/*/*/Common7/IDE/Extensions/TestPlatform/vstest.console.exe"))
            if not candidates:
                raise RuntimeError("Defina VSTEST_CONSOLE con la ruta de vstest.console.exe")
            runner = str(candidates[0])
        result = subprocess.run([runner, str(ROOT / "AOCR.Tests/bin/Debug/AOCR.Tests.dll"),
            "/TestCaseFilter:FullyQualifiedName~Ac07|FullyQualifiedName~Ac08|FullyQualifiedName~Ac09", "/Logger:trx;LogFileName=ac07.trx",
            "/ResultsDirectory:" + str(ROOT / "TestResults")], env=env, cwd=ROOT)
        return result.returncode
    finally:
        if created:
            with admin.cursor() as cur:
                cur.execute("SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname=%s AND pid<>pg_backend_pid()", (database,))
                cur.execute(sql.SQL("DROP DATABASE {}").format(sql.Identifier(database)))
        admin.close()
        print("Base desechable AC-07 eliminada.", flush=True)


if __name__ == "__main__":
    raise SystemExit(main())
