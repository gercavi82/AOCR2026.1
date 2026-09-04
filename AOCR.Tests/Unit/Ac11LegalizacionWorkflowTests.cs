using System;
using System.Collections.Generic;
using System.IO;
using CapaDatos.Constants;
using CapaDatos.Interfaces;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class Ac11LegalizacionWorkflowTests
    {
        private FakeRepository _repo;
        private AocrFinalWorkflowService _service;

        [TestInitialize]
        public void Setup() { _repo = new FakeRepository(); _service = new AocrFinalWorkflowService(_repo); }

        [TestMethod] public void Dircav_RemiteAocr_InvocaRepositorioCentral()
        {
            var r=_service.RemitirAocrDirdac(Remision(Actor("DIRCAV",true)));
            Assert.IsTrue(r.Exito); Assert.AreEqual(1,_repo.Remisiones);
        }

        [DataTestMethod]
        [DataRow("INSPECTOR")][DataRow("COORDINADOR")][DataRow("DIRDAC")][DataRow("ADMINISTRADOR")]
        public void RolesDistintosDircav_NoPuedenRemitirDirectoDirdac(string rol)
        {
            var r=_service.RemitirAocrDirdac(Remision(Actor(rol,true)));
            Assert.AreEqual(403,r.HttpStatusCode); Assert.AreEqual(0,_repo.Remisiones);
        }

        [TestMethod] public void Dircav_SinPermisoGranular_Recibe403()
        { Assert.AreEqual(403,_service.RemitirAocrDirdac(Remision(Actor("DIRCAV",false))).HttpStatusCode); }

        [TestMethod] public void UsuarioInvalido_NoUsaUsuarioIdCero()
        { Assert.AreEqual(401,_service.RemitirAocrDirdac(Remision(Actor("DIRCAV",true,0))).HttpStatusCode); }

        [TestMethod] public void Dirdac_DevuelveConObservacionValida()
        {
            var r=_service.DevolverAocrDircav(new DevolverAocrDircavRequest{SolicitudId=7,VersionEsperada=3,Observacion="Corregir numeración del AOCR.",Actor=Actor("DIRDAC",true)});
            Assert.IsTrue(r.Exito); Assert.AreEqual(1,_repo.Devoluciones);
        }

        [DataTestMethod][DataRow(null)][DataRow("")][DataRow("corta")]
        public void Dirdac_NoDevuelveSinObservacionSuficiente(string observacion)
        {
            var r=_service.DevolverAocrDircav(new DevolverAocrDircavRequest{SolicitudId=7,VersionEsperada=3,Observacion=observacion,Actor=Actor("DIRDAC",true)});
            Assert.AreEqual(400,r.HttpStatusCode); Assert.AreEqual(0,_repo.Devoluciones);
        }

        [TestMethod] public void Dircav_NoPuedeFirmarAocr()
        { Assert.AreEqual(403,_service.FirmarLegalizarAocr(Firma(Actor("DIRCAV",true))).HttpStatusCode); }

        [TestMethod] public void Administrador_NoPuedeFirmarAocr()
        { Assert.AreEqual(403,_service.FirmarLegalizarAocr(Firma(Actor("ADMINISTRADOR",true))).HttpStatusCode); }

        [TestMethod] public void Dirdac_ConEvidenciaValida_FirmaAocr()
        { var r=_service.FirmarLegalizarAocr(Firma(Actor("DIRDAC",true)));Assert.IsTrue(r.Exito);Assert.AreEqual(1,_repo.Firmas); }

        [TestMethod] public void Firma_SinHashSha256_NoLlegaAPersistencia()
        { var q=Firma(Actor("DIRDAC",true));q.HashPdfFirmado="abc";Assert.AreEqual(400,_service.FirmarLegalizarAocr(q).HttpStatusCode);Assert.AreEqual(0,_repo.Firmas); }

        [TestMethod] public void RequestIncompleto_Devuelve400()
        { Assert.AreEqual(400,_service.RemitirAocrDirdac(new RemitirAocrDirdacRequest{Actor=Actor("DIRCAV",true)}).HttpStatusCode); }

        [DataTestMethod][DataRow(404)][DataRow(409)] public void Repositorio_ConservaRespuestaHttpFuncional(int status)
        { _repo.Siguiente=AocrWorkflowResult.Error(status,"CONTROL","resultado controlado");Assert.AreEqual(status,_service.RemitirAocrDirdac(Remision(Actor("DIRCAV",true))).HttpStatusCode); }

        [TestMethod] public void ErrorInesperado_Devuelve500Controlado()
        { _repo.Lanzar=true;Assert.AreEqual(500,_service.RemitirAocrDirdac(Remision(Actor("DIRCAV",true))).HttpStatusCode); }

        [TestMethod] public void BandejaYContador_ProvienenDeLaMismaColeccion()
        { _repo.Items.Add(new BandejaAocrDirdacItemViewModel());Assert.AreEqual(1,_service.ObtenerBandejaDirdac().TotalPendientes);Assert.AreEqual(1,_repo.Listados); }

        [TestMethod] public void Dao_UsaTransaccionLockIdempotenciaHistorialAuditoriaYOutbox()
        {
            var s=Read("CapaDatos/DAOs/AocrFinalWorkflowDAO.cs");
            StringAssert.Contains(s,"cn.BeginTransaction()");StringAssert.Contains(s,"pg_advisory_xact_lock");StringAssert.Contains(s,"aocr_evento_workflow");
            StringAssert.Contains(s,"aocr_tbhistorial_estado");StringAssert.Contains(s,"email_queue");StringAssert.Contains(s,"ON CONFLICT(event_key)");
        }

        [TestMethod] public void Endpoints_Post_TienenAntiforgeryYPermiso()
        {
            var d=Read("CapaPresentacion/Controllers/DirdacController.cs");var c=Read("CapaPresentacion/Controllers/DircavController.cs");
            StringAssert.Contains(d,"DevolverAocrDircav");StringAssert.Contains(d,"FirmarLegalizarAocr");StringAssert.Contains(c,"RemitirAocrDirdac");
            StringAssert.Contains(d,"ValidateAntiForgeryToken");StringAssert.Contains(c,"RequirePermission(AocrFinalWorkflowService.PermisoRemitirDirdac)");
        }

        [TestMethod] public void Ac11_NoEntregaNiCierraAlCompletarFirmas()
        {
            var dao=Read("CapaDatos/DAOs/DocumentosFinalesWorkflowDAO.cs");
            StringAssert.Contains(dao,"MarcarFirmasCompletasAc11(cn, tx");
            var call=dao.IndexOf("MarcarFirmasCompletasAc11(cn, tx",StringComparison.Ordinal);
            var legacy=dao.IndexOf("FinalizarExpedienteYEncolarRt(cn, tx",StringComparison.Ordinal);
            Assert.IsTrue(call>=0);Assert.IsTrue(legacy<0,"AC-11 no debe invocar la entrega AC-12.");
        }

        [TestMethod] public void Migracion_TieneUpValidacionRollbackEIndices()
        {
            Assert.IsTrue(File.Exists(Path.Combine(Root(),"scripts/sql/20260904_ac11_flujo_legalizacion.sql")));
            Assert.IsTrue(File.Exists(Path.Combine(Root(),"scripts/sql/20260904_ac11_flujo_legalizacion_validate.sql")));
            Assert.IsTrue(File.Exists(Path.Combine(Root(),"scripts/sql/20260904_ac11_flujo_legalizacion_rollback.sql")));
            StringAssert.Contains(Read("scripts/sql/20260904_ac11_flujo_legalizacion.sql"),"ix_ac11_bandeja_dirdac");
        }

        [TestMethod] public void Vistas_UsanUrlActionYSonResponsive()
        { var v=Read("CapaPresentacion/Views/Dirdac/Bandeja.cshtml");StringAssert.Contains(v,"Url.Action");StringAssert.Contains(v,"table-responsive");Assert.IsFalse(v.Contains("/aocr/")); }

        [TestMethod, TestCategory("Integration")] public void EsquemaReal_BandejaDirdac_ConsultaSinModificarDatos()
        { Assert.IsNotNull(new AocrFinalWorkflowDAO().ListarBandejaDirdac()); }

        private static AocrWorkflowActor Actor(string rol,bool permiso,int id=9){return new AocrWorkflowActor{UsuarioId=id,UsuarioNombre="usuario.test",RolActivo=rol,TienePermiso=permiso};}
        private static RemitirAocrDirdacRequest Remision(AocrWorkflowActor a){return new RemitirAocrDirdacRequest{SolicitudId=7,DocumentoId=17,VersionEsperada=3,VersionAocrEsperada=2,Actor=a};}
        private static FirmarLegalizarAocrRequest Firma(AocrWorkflowActor a){return new FirmarLegalizarAocrRequest{SolicitudId=7,DocumentoId=17,VersionEsperada=4,VersionAocrEsperada=2,RutaPdfFirmado="~/App_Data/Uploads/AOCR/Firmados/7/aocr.pdf",HashPdfFirmado=new string('a',64),TamanioPdfFirmado=100,Actor=a};}
        private static string Read(string p){return File.ReadAllText(Path.Combine(Root(),p.Replace('/',Path.DirectorySeparatorChar)));}
        private static string Root(){return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","..",".."));}

        private sealed class FakeRepository : IAocrFinalWorkflowRepository
        {
            public int Remisiones,Devoluciones,Firmas,Listados; public bool Lanzar; public AocrWorkflowResult Siguiente; public readonly List<BandejaAocrDirdacItemViewModel> Items=new List<BandejaAocrDirdacItemViewModel>();
            public AocrWorkflowResult RemitirAocrDirdac(RemitirAocrDirdacRequest r){Remisiones++;if(Lanzar)throw new Exception("error simulado");return Siguiente??Ok(AocrEstadosProceso.AocrPendienteDirdac);}
            public AocrWorkflowResult DevolverAocrDircav(DevolverAocrDircavRequest r){Devoluciones++;return Ok(AocrEstadosProceso.DevueltoDircav);}
            public AocrWorkflowResult FirmarLegalizarAocr(FirmarLegalizarAocrRequest r){Firmas++;return Ok(AocrEstadosProceso.FirmasCompletas);}
            public AocrWorkflowResult EvaluarFirmasCompletas(int s,long v,AocrWorkflowActor a){return Ok(AocrEstadosProceso.FirmasCompletas);}
            public IList<BandejaAocrDirdacItemViewModel> ListarBandejaDirdac(){Listados++;return Items;}
            public DetalleAocrDirdacViewModel ObtenerDetalleDirdac(int s){return null;}
            public BandejaAocrDirdacItemViewModel ObtenerContextoRemisionDircav(int s){return null;}
            private static AocrWorkflowResult Ok(string estado){return new AocrWorkflowResult{Exito=true,HttpStatusCode=200,EstadoNuevo=estado};}
        }
    }
}
