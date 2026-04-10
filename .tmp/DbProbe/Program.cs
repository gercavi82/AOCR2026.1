using System;
using System.IO;
using Npgsql;
var cs="Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;Timeout=15;CommandTimeout=120;";
var files=new[]{"scripts/mirror_sync/001_create_schemas.sql","scripts/mirror_sync/002_create_sync_tables.sql","scripts/mirror_sync/003_create_mirror_raw_tables.sql"};
await using var conn=new NpgsqlConnection(cs);
await conn.OpenAsync();
await using var tx=await conn.BeginTransactionAsync();
try {
  foreach(var f in files){
    var sql=await File.ReadAllTextAsync(f);
    await using var cmd=new NpgsqlCommand(sql,conn,tx);
    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine($"OK {f}");
  }
  await tx.RollbackAsync();
  Console.WriteLine("Scripts validados en transaccion (rollback). ");
}
catch(Exception ex){
  Console.WriteLine(ex.ToString());
  try { await tx.RollbackAsync(); } catch {}
  Environment.ExitCode=1;
}
