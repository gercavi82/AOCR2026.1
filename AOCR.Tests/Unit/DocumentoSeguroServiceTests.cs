using System;
using System.IO;
using System.Text;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class DocumentoSeguroServiceTests
    {
        private string _root;
        [TestInitialize] public void Init() { _root = Path.Combine(Path.GetTempPath(), "aocr-gate7-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_root); }
        [TestCleanup] public void Clean() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        [TestMethod] public void RtPropietario_EndpointValidaPropiedadAntesDeServir() { var s=Read("CapaPresentacion/Controllers/RTController.cs"); StringAssert.Contains(s,"EsPropietarioSolicitud"); StringAssert.Contains(s,"DescargarNcRt"); }
        [TestMethod] public void RtAjeno_EndpointDevuelve403() { var s=Slice(Read("CapaPresentacion/Controllers/RTController.cs"),"DescargarNcRt","DescargarSubsanacionNc"); StringAssert.Contains(s,"HttpStatusCodeResult(403"); }
        [TestMethod] public void InspectorAsignado_UsaControlDeAsignacion() { var s=Read("CapaPresentacion/Controllers/InspeccionController.cs"); StringAssert.Contains(s,"DescargarNcInspector"); StringAssert.Contains(s,"PuedeAccederInspeccion"); }
        [TestMethod] public void InspectorNoAsignado_Devuelve403() { var s=Slice(Read("CapaPresentacion/Controllers/InspeccionController.cs"),"DescargarNcSegura","DescargarSubsanacionNc"); StringAssert.Contains(s,"HttpStatusCodeResult(403"); }

        [TestMethod] public void RutaTraversal_Rechazada() { Assert.AreEqual(DocumentoSeguroError.Prohibido, Resolve("../secreto.pdf").Error); }
        [TestMethod] public void RutaFueraAppData_Rechazada() { var f=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".pdf"); File.WriteAllBytes(f,Pdf()); try{Assert.AreEqual(DocumentoSeguroError.Prohibido,Resolve(f).Error);}finally{File.Delete(f);} }
        [TestMethod] public void ArchivoInexistente_DevuelveNoEncontrado() { Assert.AreEqual(DocumentoSeguroError.NoEncontrado,Resolve("no.pdf").Error); }
        [TestMethod] public void ArchivoVacio_DevuelveVacio() { File.WriteAllBytes(Path.Combine(_root,"vacio.pdf"),new byte[0]); Assert.AreEqual(DocumentoSeguroError.Vacio,Resolve("vacio.pdf").Error); }
        [TestMethod] public void ExtensionIncorrecta_Rechazada() { File.WriteAllText(Path.Combine(_root,"mal.exe"),"MZ"); Assert.AreEqual(DocumentoSeguroError.Extension,Resolve("mal.exe").Error); }
        [TestMethod] public void PdfContenidoIncompatible_Rechazado() { File.WriteAllText(Path.Combine(_root,"falso.pdf"),"no-pdf"); Assert.AreEqual(DocumentoSeguroError.Contenido,Resolve("falso.pdf").Error); }
        [TestMethod] public void NombreMalicioso_SeNormaliza() { var n=DocumentoSeguroService.NormalizarNombreDescarga("x\r\n\"../../evil.pdf",".pdf"); Assert.IsFalse(n.Contains("\r")||n.Contains("\n")||n.Contains("\"")||n.Contains("/")||n.Contains("\\")); }
        [TestMethod] public void DocumentoNoRelacionado_RecibeProhibido() { Assert.AreEqual(DocumentoSeguroError.Prohibido,new DocumentoSeguroService(new[]{_root}).Resolver(1,7,8,"x.pdf","x.pdf",Map).Error); }
        [TestMethod] public void DocumentoHistorico_ValidoSigueDisponible() { File.WriteAllBytes(Path.Combine(_root,"hist.pdf"),Pdf()); Assert.IsTrue(Resolve("hist.pdf").EsValido); }
        [TestMethod] public void Auditoria_RegistraDescargaSinRuta() { string audit=null;File.WriteAllBytes(Path.Combine(_root,"a.pdf"),Pdf());new DocumentoSeguroService(new[]{_root},x=>audit=x).Resolver(1,7,7,"a.pdf","a.pdf",Map);StringAssert.Contains(audit,"DESCARGA_DOCUMENTO_OK");Assert.IsFalse(audit.Contains(_root)); }
        [TestMethod] public void MensajePublico_NoFiltraRutaFisica() { var r=Resolve(Path.Combine(Path.GetTempPath(),"secreto.pdf"));Assert.IsFalse((r.MensajePublico??"").Contains(Path.GetTempPath())); }

        private DocumentoSeguroResultado Resolve(string path){return new DocumentoSeguroService(new[]{_root}).Resolver(1,7,7,path,"documento.pdf",Map);}
        private string Map(string path){return Path.Combine(_root,path.TrimStart('~','/','\\'));}
        private static byte[] Pdf(){return Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF");}
        private static string Read(string p){return File.ReadAllText(Path.Combine(Root(),p.Replace('/',Path.DirectorySeparatorChar)));}
        private static string Root(){return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","..",".."));}
        private static string Slice(string s,string a,string b){var i=s.IndexOf(a,StringComparison.Ordinal);var j=s.IndexOf(b,i+a.Length,StringComparison.Ordinal);Assert.IsTrue(i>=0&&j>i);return s.Substring(i,j-i);}
    }
}
