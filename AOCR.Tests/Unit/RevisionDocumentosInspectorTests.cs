using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class RevisionDocumentosInspectorTests
    {
        [TestMethod] public void InspectorAsignado_AbreBandeja(){Contiene(Servicio(),"FirmaAocrInspectorQueueService().Obtener(usuarioId,false)");}
        [TestMethod] public void OtroInspector_Recibe403(){Contiene(Servicio(),"throw new RevisionDocumentosHttpException(403");}
        [TestMethod] public void Detalle_MuestraAmbosBloques(){var v=Fuente("CapaPresentacion\\Views\\InspectorDocumentosFinales\\Detalle.cshtml");Contiene(v,"id=\"aocr\"");Contiene(v,"id=\"condiciones\"");}
        [TestMethod] public void AbrirAocr_TieneRutaDetalle(){Contiene(Controlador(),"ActionResult Detalle(int solicitudId)");}
        [TestMethod] public void ModificarAocr_UsaViewModelTipado(){Contiene(Fuente("CapaPresentacion\\Models\\RevisionDocumentosInspectorViewModels.cs"),"class AocrInspectorViewModel");}
        [TestMethod] public void GuardarAocr_UsaServicio(){Contiene(Controlador(),"Servicio.GuardarAocr(");}
        [TestMethod] public void PrevisualizarAocr_NoPersiste(){var s=Servicio();Contiene(s,"PrevisualizarAocr");Contiene(s,"MarcaBorrador(bytes)");}
        [TestMethod] public void GenerarPdfAocr_ActualizaDocumentoExacto(){Contiene(Servicio(),"_documentoPdf.Generar(new GenerarPdfRequest");}
        [TestMethod] public void AbrirCondiciones_ExisteBloqueIndependiente(){Contiene(Fuente("CapaPresentacion\\Views\\InspectorDocumentosFinales\\Detalle.cshtml"),"Condiciones y Limitaciones</h2>");}
        [TestMethod] public void ModificarCondiciones_UsaCamposExistentes(){var s=Servicio();Contiene(s,"AprobacionesEspecialesOtros=r.Limitaciones");Contiene(s,"AprobacionesEspeciales=r.Condiciones");}
        [TestMethod] public void GuardarCondiciones_UsaServicio(){Contiene(Controlador(),"Servicio.GuardarCondiciones(");}
        [TestMethod] public void PrevisualizarCondiciones_NoFirma(){var s=Servicio();Contiene(s,"PrevisualizarCondiciones");Assert.IsFalse(Seccion(s,"private RevisionDocumentosOperacionResult Previsualizar", "private RevisionDocumentosOperacionResult Generar").Contains("FirmaDocumento"));}
        [TestMethod] public void GenerarPdfCondiciones_SeparadoDeAocr(){Contiene(Controlador(),"ActionResult GenerarPdfCondiciones");}
        [TestMethod] public void CamposObligatorios_Retornan422(){Contiene(Servicio(),"return Error(422");}
        [TestMethod] public void ConflictoVersion_Retorna409(){Contiene(Servicio(),"throw new RevisionDocumentosHttpException(409,\"Conflicto de version documental.");}
        [TestMethod] public void DocumentoFirmado_NoEditable(){Contiene(Servicio(),"string.IsNullOrWhiteSpace(f.RutaDocumento)");}
        [TestMethod] public void ObservacionDcav_EsVisible(){var v=Fuente("CapaPresentacion\\Views\\InspectorDocumentosFinales\\Detalle.cshtml");Contiene(v,"Observaciones recibidas");Contiene(v,"@o.Observacion");}
        [TestMethod] public void SoloDocumentoObservado_EsEditable(){Contiene(Servicio(),"DocumentoObservado(observaciones,\"RECONOCIMIENTO\")");Contiene(Servicio(),"DocumentoObservado(observaciones,\"CONDICIONES_LIMITACIONES\")");}
        [TestMethod] public void ContadorYBandeja_UsanMismaQueue(){var b=Fuente("CapaPresentacion\\Helpers\\SidebarMenuBuilder.cs");Contiene(b,"FirmaAocrInspectorQueueService().Obtener(context.UserId");Contiene(Servicio(),"FirmaAocrInspectorQueueService().Obtener(usuarioId,false)");}
        [TestMethod] public void SeccionFinal_NoUsaDocumentoLista(){var total=Controlador()+Servicio()+Fuente("CapaPresentacion\\Views\\InspectorDocumentosFinales\\Revision.cshtml")+Fuente("CapaPresentacion\\Views\\InspectorDocumentosFinales\\Detalle.cshtml");Assert.IsFalse(total.Contains("Documento/Lista"));Assert.IsFalse(total.Contains("\"Documento\", \"Lista\""));}
        [TestMethod] public void Rutas_BajoAocr_UsanUrlAction(){var v=Fuente("CapaPresentacion\\Views\\InspectorDocumentosFinales\\Detalle.cshtml");Contiene(v,"@Url.Action(");Contiene(Fuente("CapaDatos\\DAOs\\HabilitacionDocumentosFinalesDAO.cs"),"/aocr/InspectorDocumentosFinales/Detalle");}
        [TestMethod] public void CompilaEnNetFramework462(){Contiene(Fuente("CapaPresentacion\\CapaPresentacion.csproj"),"<TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion>");}

        private static string Servicio(){return Fuente("CapaPresentacion\\Services\\RevisionDocumentosInspectorService.cs");}
        private static string Controlador(){return Fuente("CapaPresentacion\\Controllers\\InspectorDocumentosFinalesController.cs");}
        private static void Contiene(string texto,string valor){StringAssert.Contains(texto,valor);}
        private static string Seccion(string texto,string inicio,string fin){var a=texto.IndexOf(inicio,StringComparison.Ordinal);var b=texto.IndexOf(fin,a+inicio.Length,StringComparison.Ordinal);return a>=0&&b>a?texto.Substring(a,b-a):string.Empty;}
        private static string Fuente(string relativa){var d=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"AOCR.sln")))d=d.Parent;Assert.IsNotNull(d);return File.ReadAllText(Path.Combine(d.FullName,relativa));}
    }
}
