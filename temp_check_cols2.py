import psycopg2

conn = psycopg2.connect(host='172.20.16.55', port=5432, dbname='dgac_des', user='root', password='control')
cur = conn.cursor()

cur.execute("SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'usuario' AND column_name IN ('idusuario','codigousuario') ORDER BY ordinal_position")
print('=== usuario key columns ===')
for r in cur.fetchall():
    print(f'  {r[0]}: {r[1]}')

cur.execute("SELECT idusuario, codigousuario FROM usuario WHERE codigousuario = '1709565459'")
rows = cur.fetchall()
print(f'\nUser 1709565459: {rows}')

# Check what codigo_inspector values look like
cur.execute("SELECT DISTINCT codigo_inspector FROM aocr_tbinspeccion LIMIT 5")
print('\naocr_tbinspeccion.codigo_inspector samples:')
for r in cur.fetchall():
    print(f'  {r[0]} (type: {type(r[0]).__name__})')

# Check aocr_tbnotificacion columns
cur.execute("SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'aocr_tbnotificacion' ORDER BY ordinal_position")
print('\n=== aocr_tbnotificacion ALL columns ===')
for r in cur.fetchall():
    print(f'  {r[0]}: {r[1]}')

conn.close()
