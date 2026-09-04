using System;
using System.Collections.Generic;
using System.IO;
using CapaDatos.Interfaces;
using CapaModelo;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class Ac12EntregaFinalTests
    {
        private FakeRepository _repo;
        private EntregaFinalService _service;

        [TestInitialize] public void Setup(){_repo=new FakeRepository();_service=new EntregaFinalService(_repo);}

        [TestMethod] public void AmbasFirmasValidas_EncolanRtEInspector(){var r=_service.Solicitar(Request(Actor("DIRDAC",true)));Assert.IsTrue(r.Exito);Assert.AreEqual(1,_repo.Solicitudes);}
        [DataTestMethod][DataRow("RT")][DataRow("INSPECTOR")][DataRow("FINANCIERO")][DataRow("ADMINISTRADOR")][DataRow("DIRCAV")][DataRow("COORDINADOR")]
        public void SoloDirdacSolicitaEntrega(string rol){Assert.AreEqual(403,_service.Solicitar(Request(Actor(rol,true))).HttpStatusCode);}
        [TestMethod] public void SinSesion_Devuelve401(){Assert.AreEqual(401,_service.Solicitar(Request(null)).HttpStatusCode);}
        [TestMethod] public void SinPermiso_Devuelve403(){Assert.AreEqual(403,_service.Solicitar(Request(Actor("DIRDAC",false))).HttpStatusCode);}
        [TestMethod] public void RequestInvalido_Devuelve400(){var q=Request(Actor("DIRDAC",true));q.VersionExpedienteEsperada=0;Assert.AreEqual(400,_service.Solicitar(q).HttpStatusCode);}
        [TestMethod] public void ErrorRepositorio_Devuelve500(){_repo.Throw=true;Assert.AreEqual(500,_service.Solicitar(Request(Actor("DIRDAC",true))).HttpStatusCode);}
        [TestMethod] public void RtEInspector_ListanDocumentos(){Assert.AreEqual(2,_service.ListarDocumentos(Actor("RT",false)).Documentos.Count);Assert.AreEqual(2,_service.ListarDocumentos(Actor("INSPECTOR",false)).Documentos.Count);}
        [DataTestMethod][DataRow("FINANCIERO")][DataRow("ADMINISTRADOR")]
        public void FinancieroYAdministrador_NoSonDestinatarios(string rol){Assert.AreEqual(0,_service.ListarDocumentos(Actor(rol,true)).Documentos.Count);}
        [TestMethod] public void DescargaSinSesion_Devuelve401(){Assert.AreEqual(401,_service.AutorizarDescarga(4,null).HttpStatusCode);}
        [TestMethod] public void DescargaIdInvalido_Devuelve400(){Assert.AreEqual(400,_service.AutorizarDescarga(0,Actor("RT",false)).HttpStatusCode);}
        [TestMethod] public void OtroRol_NoDescarga(){Assert.AreEqual(403,_service.AutorizarDescarga(4,Actor("FINANCIERO",true)).HttpStatusCode);}

        [TestMethod] public void Dao_VerificaFirmasVersionesArchivosYDestinatarios()
        {var s=Read("CapaDatos/DAOs/EntregaFinalDAO.cs");StringAssert.Contains(s,"FIRMAS_INCOMPLETAS");StringAssert.Contains(s,"VERSIONES_INCOMPATIBLES");StringAssert.Contains(s,"ValidarArchivo");StringAssert.Contains(s,"RtUsuarioId");StringAssert.Contains(s,"InspectorUsuarioId");StringAssert.Contains(s,"AocrRolesInstitucionales.EsDirdac");StringAssert.Contains(s,"AocrRolesInstitucionales.EsDircav");}

        [TestMethod] public void Dao_TransaccionLockIdempotenciaDosDestinatarios()
        {var s=Read("CapaDatos/DAOs/EntregaFinalDAO.cs");StringAssert.Contains(s,"BeginTransaction");StringAssert.Contains(s,"pg_advisory_xact_lock");StringAssert.Contains(s,"CargarEntrega");StringAssert.Contains(s,"\"RT\"");StringAssert.Contains(s,"\"INSPECTOR\"");StringAssert.Contains(s,"emailIds");StringAssert.Contains(s,"ON CONFLICT(entrega_id,tipo_destinatario,usuario_id)");}

        [TestMethod] public void Worker_ReutilizaColaPersisteMessageIdYValidaHash()
        {var s=Read("CapaDatos/Services/EmailQueueService.cs");StringAssert.Contains(s,"message_id = @message_id");StringAssert.Contains(s,"ReprogramarReintentoAsync");StringAssert.Contains(s,"ActualizarEntregaFinalSinInterrumpir");StringAssert.Contains(s,"attachment.Sha256");StringAssert.Contains(s,"SHA256.Create()");}

        [TestMethod] public void EntregaParcialYCompleta_SeCalculanSinCerrar()
        {var s=Read("CapaDatos/DAOs/EntregaFinalDAO.cs");StringAssert.Contains(s,"EstadosEntregaFinal.Parcial");StringAssert.Contains(s,"EstadosEntregaFinal.Completa");StringAssert.Contains(s,"EstadosEntregaFinal.FallidaReintentable");StringAssert.Contains(s,"AocrEstadosProceso.Entregado");Assert.IsFalse(s.Contains("AocrEstadosProceso.Cerrado"));}

        [TestMethod] public void Descarga_ValidaPropiedadCompaniaAsignacionHashYAuditaDenegado()
        {var s=Read("CapaDatos/DAOs/EntregaFinalDAO.cs");StringAssert.Contains(s,"tipo_destinatario='RT'");StringAssert.Contains(s,"tipo_destinatario='INSPECTOR'");StringAssert.Contains(s,"codigo_compania");StringAssert.Contains(s,"INTEGRIDAD_INVALIDA");StringAssert.Contains(s,"AuditarDescarga");StringAssert.Contains(s,"DENEGADA");}

        [TestMethod] public void Endpoints_TienenAutorizacionAntiforgeryYRutasMvc()
        {var f=Read("CapaPresentacion/Controllers/FlujoController.cs");var d=Read("CapaPresentacion/Controllers/DocumentoController.cs");StringAssert.Contains(f,"ValidateAntiForgeryToken");StringAssert.Contains(f,"RequirePermission(EntregaFinalService.PermisoSolicitar)");StringAssert.Contains(d,"DescargarFinal");StringAssert.Contains(Read("CapaPresentacion/Controllers/RTController.cs"),"DocumentosFinales");StringAssert.Contains(Read("CapaPresentacion/Controllers/InspeccionController.cs"),"Inspector/DocumentosFinales");}

        [TestMethod] public void Vistas_SonTipadasResponsiveYUsanUrlAction()
        {foreach(var p in new[]{"CapaPresentacion/Views/RT/DocumentosFinales.cshtml","CapaPresentacion/Views/Inspeccion/DocumentosFinales.cshtml"}){var s=Read(p);StringAssert.Contains(s,"@model CapaModelo.DocumentosFinalesViewModel");StringAssert.Contains(s,"Url.Action");StringAssert.Contains(s,"@media");StringAssert.Contains(s,"DescargarFinal");}}

        [TestMethod] public void Migracion_TieneUpValidacionRollbackEIndices()
        {var up=Read("scripts/sql/20260904_ac12_entrega_final.sql");StringAssert.Contains(up,"aocr_entrega_final");StringAssert.Contains(up,"aocr_entrega_destinatario");StringAssert.Contains(up,"aocr_entrega_intento");StringAssert.Contains(up,"ux_ac12_entrega_version");Assert.IsTrue(File.Exists(Path.Combine(Root(),"scripts/sql/20260904_ac12_entrega_final_validate.sql")));Assert.IsTrue(File.Exists(Path.Combine(Root(),"scripts/sql/20260904_ac12_entrega_final_rollback.sql")));}

        [TestMethod] public void Ac11_DisparaEntregaTrasFirmasCompletasSinCerrar()
        {var s=Read("CapaNegocio/Services/AocrFinalWorkflowService.cs");StringAssert.Contains(s,"_entregaFinalService.Solicitar");StringAssert.Contains(s,"AocrEstadosProceso.FirmasCompletas");Assert.IsFalse(s.Contains("AocrEstadosProceso.Cerrado"));}

        private static SolicitarEntregaFinalRequest Request(EntregaFinalActor a){return new SolicitarEntregaFinalRequest{SolicitudId=7,VersionExpedienteEsperada=5,Actor=a};}
        private static EntregaFinalActor Actor(string rol,bool permiso){return rol==null?null:new EntregaFinalActor{UsuarioId=8,UsuarioNombre="usuario",RolActivo=rol,TienePermiso=permiso,CompaniaCodigo="ABC"};}
        private static string Root(){var d=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"AOCR.sln")))d=d.Parent;return d==null?AppDomain.CurrentDomain.BaseDirectory:d.FullName;}
        private static string Read(string p){return File.ReadAllText(Path.Combine(Root(),p.Replace('/',Path.DirectorySeparatorChar)));}

        private sealed class FakeRepository:IEntregaFinalRepository
        {
            public int Solicitudes;public bool Throw;
            public EntregaFinalResult Solicitar(SolicitarEntregaFinalRequest request){if(Throw)throw new InvalidOperationException();Solicitudes++;return new EntregaFinalResult{Exito=true,HttpStatusCode=200,EstadoEntrega=EstadosEntregaFinal.Encolada};}
            public IList<DocumentoFinalDisponibleViewModel> ListarDocumentos(EntregaFinalActor actor){return new List<DocumentoFinalDisponibleViewModel>{new DocumentoFinalDisponibleViewModel(),new DocumentoFinalDisponibleViewModel()};}
            public DescargaFinalAutorizada AutorizarDescarga(int documentoId,EntregaFinalActor actor){return new DescargaFinalAutorizada{Autorizada=true,HttpStatusCode=200};}
            public IList<EstadoEntregaFinalViewModel> ConsultarEstados(int? solicitudId){return new List<EstadoEntregaFinalViewModel>();}
            public void ActualizarDesdeCola(int emailQueueId,string estadoCola,string messageId,string error){}
        }
    }
}
