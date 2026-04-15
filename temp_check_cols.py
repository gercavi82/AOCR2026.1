import psycopg2

conn = psycopg2.connect(host='172.20.16.55', port=5432, dbname='dgac_des', user='root', password='control')
cur = conn.cursor()

# Check inspector columns in aocr_tbinforme_inspeccion
cur.execute("""
    SELECT column_name, data_type 
    FROM information_schema.columns 
    WHERE table_name = 'aocr_tbinforme_inspeccion'
    ORDER BY ordinal_position
""")
print('=== aocr_tbinforme_inspeccion (ALL columns) ===')
for r in cur.fetchall():
    print(f'  {r[0]}: {r[1]}')

# Check inspector columns in aocr_tbinspeccion
cur.execute("""
    SELECT column_name, data_type 
    FROM information_schema.columns 
    WHERE table_name = 'aocr_tbinspeccion'
    AND column_name LIKE '%inspector%'
    ORDER BY ordinal_position
""")
print('\n=== aocr_tbinspeccion (inspector columns) ===')
for r in cur.fetchall():
    print(f'  {r[0]}: {r[1]}')

# Check usuario columns in related tables
for tbl in ['aocr_tbsolicitud', 'aocr_tbhistorialestado', 'aocr_tbnotificacion']:
    cur.execute("""
        SELECT column_name, data_type 
        FROM information_schema.columns 
        WHERE table_name = %s AND column_name LIKE '%%usuario%%'
        ORDER BY ordinal_position
    """, (tbl,))
    rows = cur.fetchall()
    if rows:
        print(f'\n=== {tbl} (usuario columns) ===')
        for r in rows:
            print(f'  {r[0]}: {r[1]}')
    else:
        print(f'\n=== {tbl}: NO usuario columns ===')

conn.close()
