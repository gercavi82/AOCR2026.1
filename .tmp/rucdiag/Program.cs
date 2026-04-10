using System;
using Npgsql;
var cs = "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=20;Timeout=15;CommandTimeout=60;";
using var conn = new NpgsqlConnection(cs);
conn.Open();

void Q(string title,string sql){
  Console.WriteLine("==== "+title+" ====");
  try{
    using var cmd=new NpgsqlCommand(sql,conn);
    using var rd=cmd.ExecuteReader();
    int c=0;
    while(rd.Read()){
      c++;
      for(int i=0;i<rd.FieldCount;i++) Console.Write($"{rd.GetName(i)}={rd[i]} ");
      Console.WriteLine();
    }
    if(c==0) Console.WriteLine("(sin filas)");
  } catch(Exception ex){ Console.WriteLine("ERR: "+ex.Message); }
}

Q("aocr_tbpago solicitud 123", "select codigo_pago,codigo_solicitud,numero_factura,monto,estado,fecha_pago from aocr_tbpago where codigo_solicitud=123 order by codigo_pago desc limit 20");
Q("aocr_tbpago numero 423423", "select codigo_pago,codigo_solicitud,numero_factura,monto,estado,fecha_pago from aocr_tbpago where numero_factura='423423' order by codigo_pago desc limit 20");
Q("aocr_tb_factura_pago recientes", "select id,orden_id,pago_id,numero_factura,fr3_estado,fr3_numero,creado_en from aocr_tb_factura_pago order by id desc limit 10");
