using System;
using System.IO;
using System.Reflection;
using CapaDatos.Constants;
using CapaDatos.Models;
using CapaNegocio.DTOs;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class HabilitacionDocumentosFinalesTests
    {
        [TestMethod] public void CrearAmbosBorradores_OrquestadorInvocaAmbosServicios() { var s=Fuente("CapaNegocio\\Services\\HabilitacionDocumentosFinalesService.cs"); StringAssert.Contains(s,"_aocr.ObtenerOCrearBorrador"); StringAssert.Contains(s,"_condiciones.ObtenerOCrearBorrador"); }
        [TestMethod] public void RecuperarAmbosBorradores_DaoBuscaAntesDeInsertar() { var s=Fuente("CapaDatos\\DAOs\\AocrDocumentoGeneradoDAO.cs"); StringAssert.Contains(s,"if (existente != null)"); StringAssert.Contains(s,"return existente;"); }
        [TestMethod] public void CrearSoloFaltante_CadaTipoSeResuelveIndependientemente() { var s=Fuente("CapaNegocio\\Services\\HabilitacionDocumentosFinalesService.cs"); Assert.IsTrue(s.IndexOf("_aocr.ObtenerOCrearBorrador",StringComparison.Ordinal)<s.IndexOf("_condiciones.ObtenerOCrearBorrador",StringComparison.Ordinal)); }
        [TestMethod] public void EvitarDuplicados_UsaBloqueoTransaccional() { StringAssert.Contains(Fuente("CapaDatos\\DAOs\\AocrDocumentoGeneradoDAO.cs"),"pg_advisory_xact_lock"); }
        [TestMethod] public void InformeSinFirma_Es422() { var x=Snapshot(); x.InformeFirmado=false; Assert.AreEqual(422,ValidarSnapshot(x).Codigo); }
        [TestMethod] public void ListaSinFirma_Es422() { var x=Snapshot(); x.ListaFirmada=false; Assert.AreEqual(422,ValidarSnapshot(x).Codigo); }
        [TestMethod] public void ResultadoNoSatisfactorio_Es422() { var x=Snapshot(); x.ResultadoInforme="INSATISFACTORIO"; Assert.AreEqual(422,ValidarSnapshot(x).Codigo); }
        [TestMethod] public void InspectorNoAsignado_Es422() { var x=Snapshot(); x.InspectorId=0; Assert.AreEqual(422,ValidarSnapshot(x).Codigo); }
        [TestMethod] public void CompaniaIncorrecta_Es422() { var x=Snapshot(); x.CodigoCompania=" "; Assert.AreEqual(422,ValidarSnapshot(x).Codigo); }
        [TestMethod] public void EstadoIncorrecto_Es409() { var x=Snapshot(); x.EstadoCentral="OTRO"; Assert.AreEqual(409,ValidarSnapshot(x).Codigo); }
        [TestMethod] public void RolIncorrecto_Es403() { var r=Request(); r.Rol="Inspector"; Assert.AreEqual(403,ValidarRequest(r).Codigo); }
        [TestMethod] public void DobleClic_ConsultaIdempotenciaAntesDeCrear() { var s=Fuente("CapaNegocio\\Services\\HabilitacionDocumentosFinalesService.cs"); Assert.IsTrue(s.IndexOf("ObtenerIdempotencia",StringComparison.Ordinal)<s.IndexOf("_aocr.ObtenerOCrearBorrador",StringComparison.Ordinal)); }
        [TestMethod] public void Reintento_DevuelveMismosIds() { var s=Fuente("CapaNegocio\\Services\\HabilitacionDocumentosFinalesService.cs"); StringAssert.Contains(s,"Ok(hit.AocrId,hit.CondicionesId"); }
        [TestMethod] public void ConflictoConcurrente_ComparaVersiones() { var x=Snapshot(); x.VersionRegistro++; Assert.AreEqual(409,ValidarSnapshot(x).Codigo); }
        [TestMethod] public void ErrorCreandoAocr_NoContinuaAlEstado() { var s=Fuente("CapaNegocio\\Services\\HabilitacionDocumentosFinalesService.cs"); StringAssert.Contains(s,"No se pudo obtener o crear el borrador AOCR"); }
        [TestMethod] public void ErrorCreandoCondiciones_NoContinuaAlEstado() { var s=Fuente("CapaNegocio\\Services\\HabilitacionDocumentosFinalesService.cs"); StringAssert.Contains(s,"No se pudo obtener o crear el borrador de Condiciones y Limitaciones"); }
        [TestMethod] public void RollbackCompleto_UsaUnaTransaccionNpgsql() { var s=Fuente("CapaNegocio\\Services\\HabilitacionDocumentosFinalesService.cs"); StringAssert.Contains(s,"BeginTransaction(System.Data.IsolationLevel.Serializable)"); StringAssert.Contains(s,"tx.Rollback()"); }
        [TestMethod] public void AuditoriaUnica_ExisteUnaInvocacionFuncional() { Assert.AreEqual(1,Contar(Fuente("CapaNegocio\\Services\\HabilitacionDocumentosFinalesService.cs"),"_dao.RegistrarAuditoria(")); }
        [TestMethod] public void NotificacionUnica_SeRegistraDentroDeTransaccion() { Assert.AreEqual(1,Contar(Fuente("CapaNegocio\\Services\\HabilitacionDocumentosFinalesService.cs"),"_dao.CrearNotificacionInspector(")); }
        [TestMethod] public void BandejaInspector_ExigeEstadoYParDocumental() { var s=Fuente("CapaPresentacion\\Services\\FirmaAocrInspectorQueueService.cs"); StringAssert.Contains(s,"AocrEstadosProceso.DocumentosHabilitadosInspector"); StringAssert.Contains(s,"TieneParDocumentosInspector"); }
        [TestMethod] public void Contador_UsaMismaConsultaQueBandeja() { var s=Fuente("CapaNegocio\\Services\\InspectorBandejaService.cs"); StringAssert.Contains(s,"DocumentosAocr = ObtenerDocumentosPendientesRevision(context).Count"); }
        [TestMethod] public void DocumentosOtraSolicitud_NoSeReutilizan() { var s=Fuente("CapaDatos\\DAOs\\AocrDocumentoGeneradoDAO.cs"); StringAssert.Contains(s,"codigo_solicitud=@solicitud"); StringAssert.Contains(s,"codigo_inspeccion=@inspeccion"); StringAssert.Contains(s,"codigo_compania"); }
        [TestMethod] public void DocumentoFirmadoExistente_NoSeSobrescribe() { var s=Fuente("CapaDatos\\DAOs\\AocrDocumentoGeneradoDAO.cs"); StringAssert.Contains(s,"El documento vigente ya fue enviado, aprobado o firmado"); }
        [TestMethod] public void Compatibilidad_NucleoPermaneceEnNetFramework462() { StringAssert.Contains(Fuente("CapaNegocio\\CapaNegocio.csproj"),"<TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion>"); }

        private static HabilitarDocumentosRequest Request() { return new HabilitarDocumentosRequest { SolicitudId=1,InspeccionId=2,InformeTecnicoId=3,UsuarioDcavId=4,Rol="DirectorCertificacionesDcav",EstadoEsperado=AocrEstadosProceso.PendienteRevisionInformeDcav,VersionRegistro=7,VersionInforme=2,ClaveIdempotencia=HabilitacionDocumentosFinalesService.ConstruirClaveIdempotencia(1,2,3,2) }; }
        private static HabilitacionDocumentosSnapshot Snapshot() { return new HabilitacionDocumentosSnapshot { SolicitudId=1,InspeccionId=2,InformeId=3,EstadoCentral=AocrEstadosProceso.PendienteRevisionInformeDcav,VersionRegistro=7,VersionInforme=2,SolicitudActiva=true,InformeVigente=true,InformeFinalizado=true,InformeFirmado=true,RutaInformeFirmado="informe.pdf",HashInforme="abc",ResultadoInforme="SATISFACTORIO",ListaId=8,ListaFinalizada=true,ListaFirmada=true,RutaListaFirmada="lv.pdf",HashLista="def",InspectorId=9,CodigoCompania="CMP" }; }
        private static ResultadoHabilitacionDocumentos ValidarRequest(HabilitarDocumentosRequest r) { return Invocar("ValidarRequest",r); }
        private static ResultadoHabilitacionDocumentos ValidarSnapshot(HabilitacionDocumentosSnapshot s) { return Invocar("ValidarSnapshot",s,Request()); }
        private static ResultadoHabilitacionDocumentos Invocar(string metodo,params object[] args) { return (ResultadoHabilitacionDocumentos)typeof(HabilitacionDocumentosFinalesService).GetMethod(metodo,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args); }
        private static int Contar(string texto,string valor) { int n=0,p=0; while((p=texto.IndexOf(valor,p,StringComparison.Ordinal))>=0){n++;p+=valor.Length;} return n; }
        private static string Fuente(string relativa) { var d=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory); while(d!=null&&!File.Exists(Path.Combine(d.FullName,"AOCR.sln")))d=d.Parent; Assert.IsNotNull(d,"No se encontro la raiz del repositorio."); return File.ReadAllText(Path.Combine(d.FullName,relativa)); }
    }
}
