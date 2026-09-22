using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Transactions;
using Npgsql;
class Probe {
 static void Main() {
  AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {var p=Path.Combine(@"C:\proyectos\AOCR\CapaPresentacion\bin",new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
  var x=new XmlDocument(); x.Load(@"C:\proyectos\AOCR\CapaPresentacion\connectionStrings.config");
  Check(x.SelectSingleNode("/connectionStrings/add[@name='PostgreSQL']").Attributes["connectionString"].Value);
 }
 static void Check(string cs) {
  var a=Assembly.LoadFrom(@"C:\proyectos\AOCR\CapaDatos\bin\Release\CapaDatos.dll");
  var m=a.GetType("CapaDatos.DAOs.SolicitudEstacionDAO").GetMethod("EjecutarGuardadoTransaccional",BindingFlags.Static|BindingFlags.NonPublic);
  using(var scope=new TransactionScope()) using(var cn=new NpgsqlConnection(cs)) {
   cn.Open();
   Func<System.Data.IDbTransaction,bool> read=tx=>{if(tx!=null)throw new Exception("Transaccion local inesperada");using(var cmd=new NpgsqlCommand("SELECT 1",cn))return Convert.ToInt32(cmd.ExecuteScalar())==1;};
   Console.WriteLine("AMBIENTE_SELECT_OK="+m.Invoke(null,new object[]{cn,read}));
   Console.WriteLine("ESTADO_AMBIENTE="+Transaction.Current.TransactionInformation.Status);
  }
  using(var cn=new NpgsqlConnection(cs)) {
   cn.Open();
   Func<System.Data.IDbTransaction,bool> read=tx=>{if(tx==null)throw new Exception("Falta transaccion local");using(var cmd=new NpgsqlCommand("SELECT 1",cn,(NpgsqlTransaction)tx))return Convert.ToInt32(cmd.ExecuteScalar())==1;};
   Console.WriteLine("INDEPENDIENTE_SELECT_OK="+m.Invoke(null,new object[]{cn,read}));
  }
 }
}
