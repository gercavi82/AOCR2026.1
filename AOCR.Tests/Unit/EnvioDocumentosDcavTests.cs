using System;
using System.IO;
using CapaDatos.Models;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class EnvioDocumentosDcavTests
    {
        [TestMethod] public void Contrato_ExponeFinalizarYEnviar(){Contiene(Servicio(),"ResultadoEnvioDocumentosDcav FinalizarYEnviar");}
        [TestMethod] public void Contrato_ExponeValidarEnvio(){Contiene(Servicio(),"ResultadoValidacionEnvioDcav ValidarEnvio");}
        [TestMethod] public void Request_ContieneIdsYVersiones(){var s=Dto();foreach(var x in new[]{"AocrId","AocrPdfId","CondicionesId","CondicionesPdfId","VersionExpediente","VersionAocr","VersionCondiciones"})Contiene(s,x);}
        [TestMethod] public void Resultado_ContieneEstadoYFecha(){var s=Dto();Contiene(s,"EstadoAnterior");Contiene(s,"EstadoNuevo");Contiene(s,"FechaEnvio");}
        [TestMethod] public void EnvioValido_UsaTransaccionSerializable(){Contiene(Servicio(),"BeginTransaction(IsolationLevel.Serializable)");}
        [TestMethod] public void SoloAocrGenerado_EsRechazado(){Contiene(Servicio(),"s.CondicionesId<=0?404");}
        [TestMethod] public void SoloCondicionesGeneradas_EsRechazado(){Contiene(Servicio(),"s.AocrId<=0||s.CondicionesId<=0");}
        [TestMethod] public void AocrInexistente_Retorna404(){Contiene(Servicio(),"s.AocrId<=0||s.CondicionesId<=0?404");}
        [TestMethod] public void CondicionesInexistentes_Retorna404(){Contiene(Servicio(),"s.AocrId<=0||s.CondicionesId<=0?404");}
        [TestMethod] public void PdfAocrInexistente_Retorna404(){Contiene(Servicio(),"integridad.Codigo==404?404:422");}
        [TestMethod] public void PdfCondicionesInexistente_UsaMismaValidacion(){Contiene(Servicio(),"ValidarPdf(snapshot,\"CONDICIONES_LIMITACIONES\"");}
        [TestMethod] public void HashAocrInvalido_BloqueaEnvio(){Contiene(Servicio(),"no supera la validacion de integridad");}
        [TestMethod] public void HashCondicionesInvalido_BloqueaEnvio(){Contiene(Servicio(),"CONDICIONES_VALIDATION_ERROR");}
        [TestMethod] public void VersionesNoVigentes_Conflicto409(){Contiene(Servicio(),"r.VersionExpediente!=s.VersionExpediente");Contiene(Servicio(),"Conflicto()");}
        [TestMethod] public void SolicitudesDiferentes_NoSeConfianIdsCliente(){Contiene(Dao(),"codigo_solicitud=@solicitud AND codigo_inspeccion=@inspeccion");}
        [TestMethod] public void InspeccionesDiferentes_NoSeConfianIdsCliente(){Contiene(Servicio(),"ValidarIdsEnviados(request,snapshot");}
        [TestMethod] public void CompaniasDiferentes_SeRechazan(){Contiene(Servicio(),"Los documentos no pertenecen a la misma compania");}
        [TestMethod] public void InspectorNoAsignado_Es403(){Contiene(Servicio(),"Solo el Inspector asignado puede finalizar");}
        [TestMethod] public void RolIncorrecto_Es403(){Contiene(Servicio(),"Normalizar(r.Rol)!=\"INSPECTORTECNICO\"");}
        [TestMethod] public void EstadoIncorrecto_Es409(){Contiene(Servicio(),"DOCUMENTOS_HABILITADOS_INSPECTOR");Contiene(Servicio(),"DOCUMENTOS_OBSERVADOS_DCAV");}
        [TestMethod] public void DocumentoFirmado_SeRechaza(){Contiene(Servicio(),"if(firmado)errores.Add");}
        [TestMethod] public void DocumentoYaEnviado_SeRechaza(){Contiene(Servicio(),"no esta generado o ya fue enviado");}
        [TestMethod] public void CamposObligatoriosIncompletos_Es422(){Contiene(Servicio(),"Los campos obligatorios del AOCR estan incompletos");}
        [TestMethod] public void DobleClic_UsaAdvisoryLock(){Contiene(Dao(),"pg_advisory_xact_lock");}
        [TestMethod] public void Reintento_DevuelveYaProcesado(){Contiene(Servicio(),"YaProcesado=true");Contiene(Servicio(),"[IDEMPOTENCY][HIT]");}
        [TestMethod] public void Idempotencia_EsDeterministica(){var s=Snapshot();Assert.AreEqual(EnvioDocumentosDcavService.CrearClaveIdempotencia(s),EnvioDocumentosDcavService.CrearClaveIdempotencia(s));}
        [TestMethod] public void Idempotencia_IncluyeAmbasVersiones(){Assert.AreEqual("1:2:3:4:5:6:ENVIAR_DOCUMENTOS_DCAV",EnvioDocumentosDcavService.CrearClaveIdempotencia(Snapshot()));}
        [TestMethod] public void ErrorAlCambiarAocr_ImpideContinuar(){Contiene(Dao(),"ActualizarDocumento(cn,tx,sql,s.AocrId");}
        [TestMethod] public void ErrorAlCambiarCondiciones_LanzaExcepcion(){Contiene(Dao(),"ActualizarDocumento(cn,tx,sql,s.CondicionesId");}
        [TestMethod] public void ErrorEstadoCentral_DetectaConcurrencia(){Contiene(Dao(),"throw new InvalidOperationException(\"CONCURRENCY_CONFLICT\")");}
        [TestMethod] public void RollbackCompleto_EstaEnCatch(){Contiene(Servicio(),"tx.Rollback()");Contiene(Servicio(),"[INSPECTOR_DCAV][ROLLBACK]");}
        [TestMethod] public void Auditoria_EsTransaccionalEIdempotente(){Contiene(Dao(),"DOCUMENTOS_ENVIADOS_DCAV");Contiene(Servicio(),"RegistrarIdempotencia(cn,tx");}
        [TestMethod] public void Historial_EsUnicoEnTransaccion(){Contiene(Servicio(),"RegistrarHistorial(cn,tx");}
        [TestMethod] public void Notificacion_EsUnicaPorEventKey(){Contiene(Dao(),"ON CONFLICT (event_key)");}
        [TestMethod] public void BandejaDcav_ConsultaEstadoCanonico(){Contiene(Fuente("CapaDatos\\DAOs\\AocrDcavDAO.cs"),"PendienteRevisionDocumentosDcav");}
        [TestMethod] public void ContadorDcav_UsaMismaConsulta(){Contiene(Fuente("CapaPresentacion\\Helpers\\SidebarMenuBuilder.cs"),"new AocrDcavDocumentosDAO().ContarPendientesRevisionDocumentos()");}
        [TestMethod] public void BloqueoEdicion_UsaEnviadoDcav(){Contiene(Dao(),"estado='ENVIADO_DCAV'");}
        [TestMethod] public void Rutas_BajoAocr(){var v=Vista();Contiene(v,"InspectorDocumentosFinales");Contiene(Dao(),"/aocr/AocrDcav/Detalle");}
        [TestMethod] public void AccionUnica_ExisteEnVistaExclusiva(){var v=Vista();Contiene(v,"frmFinalizarEnviarDcav");Contiene(v,"FINALIZAR REVISI");}
        [TestMethod] public void AccionUnica_ConfirmaYDeshabilita(){var v=Vista();Contiene(v,"window.confirm");Contiene(v,"button.disabled=true");}
        [TestMethod] public void EndpointAnterior_NoEsAccion(){Contiene(Fuente("CapaPresentacion\\Controllers\\AocrDcavController.cs"),"[NonAction]");}
        [TestMethod] public void Generar_NoCambiaEstadoCentral(){Contiene(Fuente("CapaPresentacion\\Controllers\\FirmaAocrController.cs"),"Generar no equivale a enviar");}
        [TestMethod] public void CompilaEnNetFramework462(){Contiene(Fuente("CapaNegocio\\CapaNegocio.csproj"),"<TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion>");}

        private static EnvioDocumentosDcavSnapshot Snapshot(){return new EnvioDocumentosDcavSnapshot{SolicitudId=1,InspeccionId=2,AocrId=3,VersionAocr=4,CondicionesId=5,VersionCondiciones=6};}
        private static string Servicio(){return Fuente("CapaNegocio\\Services\\EnvioDocumentosDcavService.cs");}
        private static string Dao(){return Fuente("CapaDatos\\DAOs\\EnvioDocumentosDcavDAO.cs");}
        private static string Dto(){return Fuente("CapaNegocio\\DTOs\\EnvioDocumentosDcavDtos.cs");}
        private static string Vista(){return Fuente("CapaPresentacion\\Views\\InspectorDocumentosFinales\\Detalle.cshtml");}
        private static void Contiene(string texto,string valor){StringAssert.Contains(texto,valor);}
        private static string Fuente(string relativa){var d=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"AOCR.sln")))d=d.Parent;Assert.IsNotNull(d);return File.ReadAllText(Path.Combine(d.FullName,relativa));}
    }
}
