import psycopg2

conn = psycopg2.connect(host='172.20.16.55', port=5432, dbname='dgac_des', user='root', password='control')
cur = conn.cursor()

# All columns in rol table
cur.execute("SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'rol' ORDER BY ordinal_position")
print('=== rol columns ===')
for r in cur.fetchall():
    print(f'  {r[0]}: {r[1]}')

# All roles
cur.execute("SELECT * FROM rol ORDER BY 1")
cols = [d[0] for d in cur.description]
print(f'\n=== All roles ({cols}) ===')
for r in cur.fetchall():
    print(f'  {dict(zip(cols, r))}')

# Check usuariorol table
cur.execute("SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'usuariorol' ORDER BY ordinal_position")
print('\n=== usuariorol columns ===')
for r in cur.fetchall():
    print(f'  {r[0]}: {r[1]}')

# Also check usuario_rol
cur.execute("SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'usuario_rol' ORDER BY ordinal_position")
rows = cur.fetchall()
if rows:
    print('\n=== usuario_rol columns ===')
    for r in rows:
        print(f'  {r[0]}: {r[1]}')

conn.close()
