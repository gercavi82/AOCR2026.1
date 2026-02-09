$env:PGPASSWORD = 'control'
& 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -h 172.20.16.55 -p 5432 -U root -d dgac_des -c 'SELECT * FROM rol ORDER BY codigorol;' -P pager=off
