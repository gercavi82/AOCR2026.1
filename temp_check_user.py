import psycopg2

conn = psycopg2.connect(host='172.20.16.55', port=5432, dbname='dgac_des', user='root', password='control')
cur = conn.cursor()

uid = 17  # 1709565459 - LUIS CATOTA

# Check all tables that reference this user
tables_to_check = [
    ("aocr_or_orden", "codigo_usuario"),
    ("aocr_tbdocumento_subsanacion", "codigo_usuario_carga"),
    ("aocr_tbsubsanacion", "codigo_usuario_solicitante"),
    ("aocr_tbsubsanacion", "codigo_usuario_respuesta"),
    ("aocr_usuario_interno_rt", "usuario_id"),
    ("aocr_usuario_compania_rt", "usuario_id"),
    ("aocr_tbsolicitud", "idusuario"),
    ("aocr_tbinspeccion", "idusuario"),
    ("aocr_tbnotificacion", "idusuario"),
    ("aocr_solicitud_rt", "usuario_id"),
    ("usuario_rol", "codigousuario"),
    ("aocr_tbhistorialestado", "idusuario"),
    ("aocr_tblog", "idusuario"),
]

print(f"=== Relaciones del usuario ID={uid} (1709565459) ===\n")

for table, col in tables_to_check:
    try:
        # Check if table exists
        cur.execute("""
            SELECT EXISTS (
                SELECT 1 FROM information_schema.tables 
                WHERE table_schema='public' AND table_name=%s
            )
        """, (table,))
        exists = cur.fetchone()[0]
        if not exists:
            continue
        
        # Check column type
        cur.execute("""
            SELECT data_type FROM information_schema.columns 
            WHERE table_schema='public' AND table_name=%s AND column_name=%s
        """, (table, col))
        row = cur.fetchone()
        if not row:
            continue
        dtype = row[0]
        
        if 'int' in dtype:
            cur.execute(f"SELECT COUNT(*) FROM {table} WHERE {col} = %s", (uid,))
        else:
            cur.execute(f"SELECT COUNT(*) FROM {table} WHERE {col} = %s", ('1709565459',))
        
        cnt = cur.fetchone()[0]
        if cnt > 0:
            print(f"  BLOQUEANTE: {table}.{col} = {cnt} registros")
        else:
            print(f"  OK: {table}.{col} = 0")
    except Exception as e:
        print(f"  ERROR: {table}.{col} -> {e}")

# Also check inspector assignment
print("\n=== Inspecciones como inspector ===")
cur.execute("""
    SELECT table_name, column_name 
    FROM information_schema.columns 
    WHERE table_schema='public' 
      AND table_name LIKE '%inspeccion%' 
      AND (column_name LIKE '%inspector%' OR column_name LIKE '%cedula%' OR column_name LIKE '%asignado%')
""")
for r in cur.fetchall():
    print(f"  {r[0]}.{r[1]}")
    try:
        cur.execute(f"SELECT COUNT(*) FROM {r[0]} WHERE {r[1]}::text = '1709565459'")
        cnt = cur.fetchone()[0]
        if cnt > 0:
            print(f"    -> {cnt} registros!")
    except:
        pass

conn.close()
