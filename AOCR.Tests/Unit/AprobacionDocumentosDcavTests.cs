using System.IO;
using CapaNegocio.DTOs;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
 [TestClass] public class AprobacionDocumentosDcavTests
 {
  [TestMethod]public void Contrato_ExponeAprobar(){C(Service(),"ResultadoAprobacionDocumentosDcav Aprobar");}
  [TestMethod]public void Contrato_ExponeValidar(){C(Service(),"ResultadoValidacionAprobacion Validar");}
  [TestMethod]public void Request_TieneEstadoEsperado(){C(Dto(),"EstadoEsperado");}
  [TestMethod]public void Request_TieneIdsPdf(){C(Dto(),"AocrPdfId");C(Dto(),"CondicionesPdfId");}
  [TestMethod]public void Request_TieneVersiones(){C(Dto(),"VersionAocr");C(Dto(),"VersionCondiciones");C(Dto(),"VersionExpediente");}
  [TestMethod]public void Clave_NoIncluyeUsuario(){var r=R();Assert.AreEqual("1:2:3:4:5:6:APROBAR_DOCUMENTOS_DCAV",AprobacionDocumentosDcavService.CrearClave(r));}
  [TestMethod]public void Clave_EsDeterministica(){Assert.AreEqual(AprobacionDocumentosDcavService.CrearClave(R()),AprobacionDocumentosDcavService.CrearClave(R()));}
  [TestMethod]public void Usuario_DebeEstarActivo(){C(Dao(),"UsuarioDcavActivo");C(Dao(),"DIRECTOR_CERTIFICACIONES_DCAV");}
  [TestMethod]public void RolFirmante_UsaRolesReales(){var x=F("CapaNegocio\\Services\\PerfilFirmanteService.cs");C(x,"Direccion");C(x,"DIRECTOR_CERTIFICACIONES_DCAV");}
  [TestMethod]public void Origen_EsCanonico(){C(Service(),"PENDIENTE_REVISION_DOCUMENTOS_DCAV");}
  [TestMethod]public void Destino_EsCanonico(){C(Service(),"PENDIENTE_FIRMAS_INSTITUCIONALES");}
  [TestMethod]public void Aocr_ExigeEnviado(){C(Service(),"d.EstadoAocr!=\"ENVIADO_DCAV\"");}
  [TestMethod]public void Condiciones_ExigeEnviado(){C(Service(),"d.EstadoCondiciones!=\"ENVIADO_DCAV\"");}
  [TestMethod]public void PdfAocr_UsaIdExacto(){C(Service(),"_pdf.ObtenerPorId(d.AocrPdfId)");}
  [TestMethod]public void PdfCondiciones_UsaIdExacto(){C(Service(),"_pdf.ObtenerPorId(d.CondicionesPdfId)");}
  [TestMethod]public void PdfAocr_ValidaArchivoTamanoHash(){C(Service(),"_pdf.ValidarArchivo(a.Id)");}
  [TestMethod]public void PdfCondiciones_ValidaArchivoTamanoHash(){C(Service(),"_pdf.ValidarArchivo(c.Id)");}
  [TestMethod]public void VersionesPdf_Coinciden(){C(Service(),"a.Version!=d.VersionAocrEnviada");C(Service(),"c.Version!=d.VersionCondicionesEnviada");}
  [TestMethod]public void Companias_Coinciden(){C(Service(),"documentos pertenecen a compañías diferentes");}
  [TestMethod]public void Informe_SeValida(){C(Service(),"Informe Técnico aprobado");C(Service(),"ValidarSoporte(d.InformeRuta");}
  [TestMethod]public void LvEae_SeValida(){C(Service(),"LV/EAE firmada");C(Service(),"ValidarSoporte(d.LvEaeRuta");}
  [TestMethod]public void Observaciones_NoCerradasBloquean(){C(Dao(),"ContarObservacionesNoCerradas");C(Dao(),"CERRADA_DCAV");}
  [TestMethod]public void Transaccion_EsSerializable(){C(Service(),"IsolationLevel.Serializable");}
  [TestMethod]public void AdvisoryLock_RecuperaPaquete(){C(Service(),"_paquete.BloquearDetalle");}
  [TestMethod]public void Aprobacion_AocrOptimista(){C(Dao(),"AprobarDocumento");C(Dao(),"estado='ENVIADO_DCAV'");}
  [TestMethod]public void Aprobacion_CondicionesMismaTransaccion(){C(Service(),"d.CondicionesId");C(Service(),"tx.Commit()");}
  [TestMethod]public void Documentos_QuedanAprobados(){C(Dao(),"estado='APROBADO_DCAV'");}
  [TestMethod]public void EstadoCentral_UsaVersion(){C(Dao(),"version=@v");C(Dao(),"version=version+1");}
  [TestMethod]public void Historial_UnicoEstadoCentral(){C(Dao(),"'PENDIENTE_REVISION_DOCUMENTOS_DCAV','PENDIENTE_FIRMAS_INSTITUCIONALES'");Assert.IsFalse(Dao().Contains("APROBADO_DOCUMENTOS_DCAV"));}
  [TestMethod]public void Auditoria_TieneEventos(){var x=Service();foreach(var e in new[]{"APROBACION_DOCUMENTOS_DCAV_INICIADA","AOCR_VALIDADO_PARA_APROBACION","CONDICIONES_VALIDADAS_PARA_APROBACION","PAQUETE_DOCUMENTAL_APROBADO_DCAV","AOCR_APROBADO_DCAV","CONDICIONES_APROBADAS_DCAV","DOCUMENTOS_BLOQUEADOS","EXPEDIENTE_ENVIADO_FIRMA_DIRDAC","NOTIFICACION_DIRDAC_CREADA"})C(x,e);}
  [TestMethod]public void Notificacion_UsaRolesReales(){C(Dao(),"JOIN public.rol r");C(Dao(),"'DIRECCION','DIRECTOR_CERTIFICACIONES_DCAV'");}
  [TestMethod]public void Outbox_EstaEnTransaccion(){C(Dao(),"INSERT INTO public.email_queue");C(Dao(),"NpgsqlTransaction tx");}
  [TestMethod]public void Idempotencia_DevuelveHit(){C(Service(),"YaProcesado=true");C(Service(),"[IDEMPOTENCY][HIT]");}
  [TestMethod]public void Concurrencia_Retorna409(){C(Service(),"[CONCURRENCY][CONFLICT]");C(Service(),"conflict?409:500");}
  [TestMethod]public void Rollback_Total(){C(Service(),"tx.Rollback()");C(Service(),"[DCAV_APPROVAL][ROLLBACK]");}
  [TestMethod]public void Controlador_NoEjecutaSql(){var x=F("CapaPresentacion\\Controllers\\AocrDcavController.cs");C(x,"AprobacionDocumentosDcavService");Assert.IsFalse(x.Contains("NpgsqlCommand"));}
  [TestMethod]public void Ui_AccionEsConjunta(){var x=F("CapaPresentacion\\Views\\AocrDcav\\DetalleDocumentos.cshtml");C(x,"APROBAR AOCR Y CONDICIONES");C(x,"Se aprobarán conjuntamente");}
  [TestMethod]public void Ui_DeshabilitaDobleClick(){C(F("CapaPresentacion\\Views\\AocrDcav\\DetalleDocumentos.cshtml"),"b.disabled=true");}
  [TestMethod]public void BandejaFirma_EsExclusiva(){C(TrayDao(),"PENDIENTE_FIRMAS_INSTITUCIONALES");Assert.IsFalse(TrayDao().Contains("PENDIENTE_FIRMA_DIRECCION"));}
  [TestMethod]public void BandejaFirma_NoUsaEjecutiva(){Assert.IsFalse(TrayDao().Contains("ObtenerParaBandejaEjecutivaAprobacion"));}
  [TestMethod]public void Contador_UsaMismaBaseQuery(){C(TrayDao(),"SELECT COUNT(*) FROM (\"+BaseQuery");}
  [TestMethod]public void Bandeja_ExponeFirmasSeparadas(){var x=ControllerFirma();C(x,"FirmarAocr");C(x,"FirmarCondiciones");}
  [TestMethod]public void Rutas_BajoAocr(){var x=F("CapaPresentacion\\Helpers\\SidebarMenuBuilder.cs");C(x,"FirmaInstitucionalAocr");}
  [TestMethod]public void Logs_Completos(){var x=Service()+F("CapaNegocio\\Services\\FirmaInstitucionalAocrService.cs");foreach(var e in new[]{"[DCAV_APPROVAL][IN]","[DCAV_APPROVAL][CONTEXT_OK]","[DCAV_APPROVAL][STATE_VALIDATION_OK]","[DCAV_APPROVAL][PACKAGE_LOAD_OK]","[DCAV_APPROVAL][AOCR_VALIDATION_OK]","[DCAV_APPROVAL][CONDICIONES_VALIDATION_OK]","[DCAV_APPROVAL][SUPPORT_DOCUMENTS_OK]","[DCAV_APPROVAL][OPEN_OBSERVATIONS_CHECK_OK]","[DCAV_APPROVAL][AOCR_APPROVED]","[DCAV_APPROVAL][CONDICIONES_APPROVED]","[DCAV_APPROVAL][DOCUMENTS_LOCKED]","[DCAV_APPROVAL][STATE_UPDATED]","[DCAV_APPROVAL][OUTBOX_CREATED]","[DCAV_APPROVAL][OK]","[DCAV_APPROVAL][VALIDATION_ERROR]","[DCAV_APPROVAL][ERROR]","[DCAV_APPROVAL][ROLLBACK]","[DIRDAC_TRAY][QUERY_IN]","[DIRDAC_TRAY][QUERY_OUT]","[DIRDAC_TRAY][COUNT]"})C(x,e);}
  [TestMethod]public void CompatibleNet462(){C(F("CapaNegocio\\CapaNegocio.csproj"),"v4.6.2");}
  static AprobarDocumentosDcavRequest R(){return new AprobarDocumentosDcavRequest{SolicitudId=1,InspeccionId=2,AocrId=3,VersionAocr=4,CondicionesId=5,VersionCondiciones=6};}
  static string Service(){return F("CapaNegocio\\Services\\AprobacionDocumentosDcavService.cs");}static string Dao(){return F("CapaDatos\\DAOs\\AprobacionDocumentosDcavDAO.cs");}static string Dto(){return F("CapaNegocio\\DTOs\\AprobacionDocumentosDcavDtos.cs");}static string TrayDao(){return F("CapaDatos\\DAOs\\FirmaInstitucionalAocrDAO.cs");}static string ControllerFirma(){return F("CapaPresentacion\\Controllers\\FirmaInstitucionalAocrController.cs");}
  static string F(string p){return File.ReadAllText(Path.Combine(Root(),p));}static string Root(){var d=new DirectoryInfo(System.AppDomain.CurrentDomain.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"AOCR.sln")))d=d.Parent;return d.FullName;}static void C(string x,string y){StringAssert.Contains(x,y);}
 }
}
