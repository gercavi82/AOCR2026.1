using System;
using Npgsql;

var cs = "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;Timeout=15;CommandTimeout=120;";
var userId = args.Length > 0 ? int.Parse(args[0]) : 32;
await using var cn = new NpgsqlConnection(cs);
await cn.OpenAsync();

await using (var cmd = new NpgsqlCommand(@"SELECT idusuario, codigousuario, nombreusuario, empresa_codigo, estado_designacion_rt FROM usuario WHERE idusuario=@id;", cn))
{
    cmd.Parameters.AddWithValue("id", userId);
    await using var rd = await cmd.ExecuteReaderAsync();
    Console.WriteLine("== usuario ==");
    while (await rd.ReadAsync())
    {
        Console.WriteLine($"id={rd[0]}, codigo={rd[1]}, nombre={rd[2]}, empresa_codigo={rd[3]}, estado_rt={rd[4]}");
    }
}

await using (var cmd = new NpgsqlCommand(@"SELECT to_regclass('public.aocr_usuario_compania_rt')::text;", cn))
{
    var v = await cmd.ExecuteScalarAsync();
    Console.WriteLine($"== tabla relacional == {v}");
}

await using (var cmd = new NpgsqlCommand(@"SELECT usuario_id, compania_codigo, COALESCE(compania_nombre,''), COALESCE(activo,true) FROM aocr_usuario_compania_rt WHERE usuario_id=@id ORDER BY compania_codigo;", cn))
{
    cmd.Parameters.AddWithValue("id", userId);
    await using var rd = await cmd.ExecuteReaderAsync();
    Console.WriteLine("== companias relacionales ==");
    var any = false;
    while (await rd.ReadAsync())
    {
        any = true;
        Console.WriteLine($"usuario_id={rd[0]}, codigo={rd[1]}, nombre={rd[2]}, activo={rd[3]}");
    }
    if (!any) Console.WriteLine("(sin filas)");
}
