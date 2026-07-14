using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class GateFSubsanacionDocumentalTests
    {
        [TestMethod]
        public void Rt_ValidaPropiedadEstadoTamanoFirmaPdfYAlmacenaFueraDePublico()
        {
            var source = Read("CapaPresentacion/Controllers/RTController.cs");
            StringAssert.Contains(source, "EsPropietarioSolicitud");
            StringAssert.Contains(source, "MAX_SUBSANACION_BYTES");
            StringAssert.Contains(source, "%PDF-");
            StringAssert.Contains(source, "~/App_Data/SubsanacionesNC/");
            StringAssert.Contains(source, "RegistrarSubsanacionRt");
        }

        [TestMethod]
        public void TransicionesNc_SonOptimistasYDevolucionCreaVersionSiguiente()
        {
            var source = Read("CapaDatos/DAOs/NoConformidadDAO.cs");
            StringAssert.Contains(source, "estado='SUBSANADA_RT'");
            StringAssert.Contains(source, "estado='CERRADA'");
            StringAssert.Contains(source, "estado='SUBSANACION_DEVUELTA'");
            StringAssert.Contains(source, "version+1");
            StringAssert.Contains(source, "BeginTransaction");
        }

        [TestMethod]
        public void Inspector_ValidaRelacionAsignacionYDescargaProtegida()
        {
            var source = Read("CapaPresentacion/Controllers/InspeccionController.cs");
            StringAssert.Contains(source, "nc.CodigoInspeccion!=codigoInspeccion");
            StringAssert.Contains(source, "PuedeAccederInspeccion");
            StringAssert.Contains(source, "DescargarSubsanacionNc");
        }

        [TestMethod]
        public void MigracionExistenteEsIdempotenteYTieneRollback()
        {
            var migration = Read("scripts/sql/011_no_conformidades.sql");
            StringAssert.Contains(migration, "ADD COLUMN IF NOT EXISTS ruta_pdf_subsanacion_rt");
            StringAssert.Contains(migration, "ADD COLUMN IF NOT EXISTS fecha_subsanacion_rt");
            Assert.IsTrue(File.Exists(Path.Combine(Root(), "scripts/sql/011_gate_f_subsanacion_rollback.sql")));
        }

        private static string Read(string path) { return File.ReadAllText(Path.Combine(Root(), path.Replace('/', Path.DirectorySeparatorChar))); }
        private static string Root() { return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")); }
    }
}
