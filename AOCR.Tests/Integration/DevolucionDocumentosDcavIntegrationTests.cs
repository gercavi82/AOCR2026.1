using System;
using System.Configuration;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace AOCR.Tests.Integration
{
    [TestClass]
    public class DevolucionDocumentosDcavIntegrationTests
    {
        [TestMethod]
        public void Deploy009_Y_VerificacionReal()
        {
            var sql=File.ReadAllText(Path.Combine(Root(),"scripts","009_devolucion_selectiva_dcav.sql"));
            using(var cn=new NpgsqlConnection(ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString))
            {cn.Open();using(var cmd=new NpgsqlCommand(sql,cn)){cmd.CommandTimeout=120;cmd.ExecuteNonQuery();}using(var check=new NpgsqlCommand("SELECT COUNT(*) FROM pg_indexes WHERE schemaname='public' AND indexname='idx_aocr_observacion_dcav_documental';",cn))Assert.AreEqual(1,Convert.ToInt32(check.ExecuteScalar()));}
        }

        [TestMethod]
        public void EsquemaReal_NoAgregoColumnasArtificiales()
        {
            using(var cn=new NpgsqlConnection(ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString))using(var cmd=new NpgsqlCommand("SELECT string_agg(column_name,',' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_schema='public' AND table_name='aocr_tbobservacion';",cn)){cn.Open();var cols=Convert.ToString(cmd.ExecuteScalar());Assert.AreEqual("codigo_observacion,codigo_solicitud,codigo_usuario,mensaje,fecha_registro,codigo_usuario_respuesta,compania_id",cols);}
        }

        static string Root(){var d=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"AOCR.sln")))d=d.Parent;return d.FullName;}
    }
}
