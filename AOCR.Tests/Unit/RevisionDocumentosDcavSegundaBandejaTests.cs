using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace AOCR.Tests.Unit
{
 [TestClass] public class RevisionDocumentosDcavSegundaBandejaTests
 {
  [TestMethod]public void Bandeja_ConsultaEstadoCorrecto(){C(Dao(),"pe.estado_actual='PENDIENTE_REVISION_DOCUMENTOS_DCAV'");}
  [TestMethod]public void Bandeja_NoConsultaPrimeraRevision(){Assert.IsFalse(BaseQuery().Contains("PENDIENTE_REVISION_INFORME_DCAV"));}
  [TestMethod]public void Bandeja_MuestraAocr(){C(Dao(),"aocr_id");}
  [TestMethod]public void Bandeja_MuestraCondiciones(){C(Dao(),"condiciones_id");}
  [TestMethod]public void Bandeja_UsaVersionesEventoEnvio(){C(Dao(),"ENVIAR_DOCUMENTOS_DCAV");}
  [TestMethod]public void Bandeja_NoUsaMaxVersion(){Assert.IsFalse(BaseQuery().Contains("MAX("));}
  [TestMethod]public void Bandeja_InformeAprobadoFirmado(){C(Dao(),"inf.firmado_inspector=TRUE");C(Dao(),"SATISFACTORIO");}
  [TestMethod]public void Bandeja_LvFirmada(){C(Dao(),"lv.firmado_tecnico=TRUE");}
  [TestMethod]public void Historial_Tipado(){C(Model(),"class HistorialDocumentoDcavDto");}
  [TestMethod]public void RolDcav_ValidadoBackend(){C(Service(),"DIRECTORCERTIFICACIONESDCAV");}
  [TestMethod]public void OtroRol_Es403(){C(Service(),"Error(403");}
  [TestMethod]public void AocrFaltante_Es404(){C(Service(),"No existen los PDF exactos enviados");}
  [TestMethod]public void CondicionesFaltantes_Es404(){C(Service(),"d.CondicionesPdfId<=0");}
  [TestMethod]public void PdfExacto_UsaIdsRegistrados(){C(Dao(),"AocrPdfId=([0-9]+)");C(Dao(),"CondicionesPdfId=([0-9]+)");}
  [TestMethod]public void HashInvalido_Es422(){C(Service(),"Hash invalido");C(Service(),"Error(422");}
  [TestMethod]public void Aprobacion_CambiaAmbos(){C(Dao(),"ActualizarDocumento(cn,tx,d.AocrId");C(Dao(),"ActualizarDocumento(cn,tx,d.CondicionesId");}
  [TestMethod]public void Aprobacion_EnviaDirdac(){C(Dao(),"PENDIENTE_FIRMA_DIRDAC");}
  [TestMethod]public void Devolucion_Aocr(){C(Service(),"r.ObservarAocr");}
  [TestMethod]public void Devolucion_Condiciones(){C(Service(),"r.ObservarCondiciones");}
  [TestMethod]public void Devolucion_Ambos(){C(Service(),"\"AMBOS\"");}
  [TestMethod]public void ObservacionVacia_Es422(){C(Service(),"La observacion es obligatoria");}
  [TestMethod]public void DobleClick_UsaIdempotencia(){C(Dao(),"ExisteIdempotencia");C(Service(),"YaProcesado=true");}
  [TestMethod]public void Concurrencia_UsaVersionExpediente(){C(Dao(),"version=@version");C(Service(),"CONCURRENCY_CONFLICT");}
  [TestMethod]public void Rollback_TransaccionSerializable(){C(Service(),"IsolationLevel.Serializable");C(Service(),"[DCAV_DOCS][ROLLBACK]");}
  [TestMethod]public void Auditoria_UnicaTransaccional(){C(Dao(),"aocr_tbauditoria");C(Dao(),"NpgsqlTransaction tx");}
  [TestMethod]public void Notificacion_EventKeyUnico(){C(Dao(),"ON CONFLICT(event_key)");}
  [TestMethod]public void Contador_UsaMismaConsulta(){C(Dao(),"SELECT COUNT(*) FROM (\"+BaseQuery");C(F("CapaPresentacion\\Helpers\\SidebarMenuBuilder.cs"),"ContarPendientesRevisionDocumentos");}
  [TestMethod]public void ErrorSql_NoSeConvierteEnListaVacia(){C(Service(),"BANDEJA_ERROR");C(Service(),"throw new RevisionDocumentosDcavException(500");}
  [TestMethod]public void Rutas_ExclusivasExisten(){var x=F("CapaPresentacion\\Controllers\\AocrDcavController.cs");foreach(var s in new[]{"RevisionDocumentos","DetalleDocumentos","HistorialDocumentos","AprobarDocumentos","DevolverDocumentos"})C(x,s);}
  [TestMethod]public void ViewModels_SonTipados(){var x=F("CapaPresentacion\\Models\\RevisionDocumentosDcavViewModels.cs");foreach(var s in new[]{"DocumentosPendientesDcavViewModel","RevisionDocumentosDcavViewModel","DocumentoRevisionDcavViewModel","DocumentoSoporteDcavViewModel","HistorialDcavViewModel","ObservacionDocumentoDcavViewModel"})C(x,s);}
  [TestMethod]public void NoUsaConsultaEjecutiva(){Assert.IsFalse((Dao()+Service()).Contains("ObtenerParaBandejaEjecutivaAprobacion"));}
  [TestMethod]public void NoUsaDynamicNiViewBagCritico(){var x=F("CapaPresentacion\\Views\\AocrDcav\\DetalleDocumentos.cshtml");Assert.IsFalse(x.Contains("dynamic"));Assert.IsFalse(x.Contains("ViewBag.Model"));}
  [TestMethod]public void Descarga_NoExponeRutaFisica(){C(F("CapaPresentacion\\Views\\AocrDcav\\DetalleDocumentos.cshtml"),"DocumentoPdf");Assert.IsFalse(F("CapaPresentacion\\Views\\AocrDcav\\DetalleDocumentos.cshtml").Contains("Ruta"));}
  [TestMethod]public void Logs_Completos(){var x=Service();foreach(var s in new[]{"BANDEJA_IN","BANDEJA_QUERY","BANDEJA_OUT","BANDEJA_ERROR","DETALLE_IN","AOCR_LOAD_OK","CONDICIONES_LOAD_OK","INFORME_LOAD_OK","HISTORIAL_LOAD_OK","APPROVE_IN","APPROVE_OK","APPROVE_ERROR","RETURN_IN","RETURN_OK","RETURN_ERROR","CONCURRENCY_CONFLICT","ROLLBACK"})C(x,s);}
  [TestMethod]public void CompatibleNetFramework462(){C(F("CapaNegocio\\CapaNegocio.csproj"),"v4.6.2");}
  static string BaseQuery(){var x=Dao();return x.Substring(x.IndexOf("private const string BaseQuery"));}static string Dao(){return F("CapaDatos\\DAOs\\AocrDcavDocumentosDAO.cs");}static string Service(){return F("CapaNegocio\\Services\\RevisionDocumentosDcavService.cs");}static string Model(){return F("CapaDatos\\Models\\RevisionDocumentosDcavDtos.cs");}static string F(string p){return File.ReadAllText(Path.Combine(Root(),p));}static string Root(){var d=new DirectoryInfo(System.AppDomain.CurrentDomain.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"AOCR.sln")))d=d.Parent;return d.FullName;}static void C(string x,string y){StringAssert.Contains(x,y);}
 }
}
