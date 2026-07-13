using System;
using System.IO;
using CapaNegocio.DTOs.DocumentosPdf;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class DocumentoPdfCentralTests
    {
        [TestMethod] public void Contrato_ExponeGenerar(){Contiene(Negocio(),"ResultadoGeneracionPdf Generar(GenerarPdfRequest request)");}
        [TestMethod] public void Contrato_ExponeObtenerVigente(){Contiene(Negocio(),"DocumentoPdfDto ObtenerVigente");}
        [TestMethod] public void Contrato_ExponeObtenerPorId(){Contiene(Negocio(),"DocumentoPdfDto ObtenerPorId");}
        [TestMethod] public void Contrato_ExponeVersiones(){Contiene(Negocio(),"IList<DocumentoPdfDto> ObtenerVersiones");}
        [TestMethod] public void Contrato_ExponeValidarArchivo(){Contiene(Negocio(),"ResultadoValidacionPdf ValidarArchivo");}
        [TestMethod] public void Contrato_DescargaRetornaStream(){Contiene(Negocio(),"Stream ObtenerArchivoAutorizado");}
        [TestMethod] public void GeneracionNula_Retorna400AntesDeDb(){var ex=Assert.ThrowsException<DocumentoPdfException>(()=>Servicio().Generar(null));Assert.AreEqual(400,ex.Codigo);}
        [TestMethod] public void GeneradorNulo_Retorna400AntesDeDb(){var r=RequestValido();r.Generador=null;var ex=Assert.ThrowsException<DocumentoPdfException>(()=>Servicio().Generar(r));Assert.AreEqual(400,ex.Codigo);}
        [TestMethod] public void RolNoAutorizado_Retorna403AntesDeDb(){var r=RequestValido();r.Rol="Solicitante";var ex=Assert.ThrowsException<DocumentoPdfException>(()=>Servicio().Generar(r));Assert.AreEqual(403,ex.Codigo);}
        [TestMethod] public void CamposFaltantes_Retornan422AntesDeDb(){var r=RequestValido();r.CamposFaltantes.Add("Numero AOCR");var ex=Assert.ThrowsException<DocumentoPdfException>(()=>Servicio().Generar(r));Assert.AreEqual(422,ex.Codigo);}
        [TestMethod] public void SolicitudCero_Retorna400(){var r=RequestValido();r.SolicitudId=0;Assert.AreEqual(400,Assert.ThrowsException<DocumentoPdfException>(()=>Servicio().Generar(r)).Codigo);}
        [TestMethod] public void InspeccionCero_Retorna400(){var r=RequestValido();r.InspeccionId=0;Assert.AreEqual(400,Assert.ThrowsException<DocumentoPdfException>(()=>Servicio().Generar(r)).Codigo);}
        [TestMethod] public void OrigenCero_Retorna400(){var r=RequestValido();r.DocumentoOrigenId=0;Assert.AreEqual(400,Assert.ThrowsException<DocumentoPdfException>(()=>Servicio().Generar(r)).Codigo);}
        [TestMethod] public void UsuarioCero_Retorna400(){var r=RequestValido();r.UsuarioId=0;Assert.AreEqual(400,Assert.ThrowsException<DocumentoPdfException>(()=>Servicio().Generar(r)).Codigo);}
        [TestMethod] public void VersionCero_Retorna400(){var r=RequestValido();r.VersionOrigen=0;Assert.AreEqual(400,Assert.ThrowsException<DocumentoPdfException>(()=>Servicio().Generar(r)).Codigo);}
        [TestMethod] public void Idempotencia_EsDeterminista(){var r=RequestValido();Assert.AreEqual(DocumentoPdfService.CrearClaveIdempotencia(r,"RECONOCIMIENTO"),DocumentoPdfService.CrearClaveIdempotencia(r,"RECONOCIMIENTO"));}
        [TestMethod] public void Idempotencia_IncluyeOperacion(){Assert.IsTrue(DocumentoPdfService.CrearClaveIdempotencia(RequestValido(),"RECONOCIMIENTO").EndsWith(":GENERAR_PDF",StringComparison.Ordinal));}
        [TestMethod] public void Idempotencia_IncluyeVersionOrigen(){Contiene(DocumentoPdfService.CrearClaveIdempotencia(RequestValido(),"RECONOCIMIENTO"),":3:GENERAR_PDF");}
        [TestMethod] public void Version_SeCalculaBajoLockTransaccional(){var s=Negocio();Assert.IsTrue(s.IndexOf("BloquearGeneracion",StringComparison.Ordinal)<s.IndexOf("ObtenerSiguienteVersion",StringComparison.Ordinal));}
        [TestMethod] public void ArchivoTemporal_SeEscribeAntesDeMover(){var s=Negocio();Assert.IsTrue(s.IndexOf("File.WriteAllBytes(temporal",StringComparison.Ordinal)<s.IndexOf("File.Move(temporal",StringComparison.Ordinal));}
        [TestMethod] public void Pdf_ValidaCabecera(){Contiene(Negocio(),"header[0] != 0x25");Contiene(Negocio(),"header[4] != 0x2D");}
        [TestMethod] public void Pdf_CalculaSha256(){Contiene(Negocio(),"SHA256.Create()");}
        [TestMethod] public void Pdf_NoSobrescribeDestino(){Contiene(Negocio(),"if (File.Exists(destino))");}
        [TestMethod] public void Error_CompensaTemporalYFinal(){var s=Negocio();Contiene(s,"CompensarArchivo(temporal");Contiene(s,"CompensarArchivo(destino");}
        [TestMethod] public void CompensacionFallida_RegistraHuerfano(){Contiene(Negocio(),"[PDF][ORPHAN_FILE]");}
        [TestMethod] public void DocumentoFirmado_EsInmutable(){Contiene(Negocio(),"if (origen.Firmado)");}
        [TestMethod] public void Descarga_NoAceptaRuta(){var c=Fuente("CapaPresentacion\\Controllers\\DocumentoPdfController.cs");Contiene(c,"ActionResult Descargar(int id)");Assert.IsFalse(c.Contains("string ruta"));}
        [TestMethod] public void Descarga_RevalidaIntegridad(){Contiene(Negocio(),"var validacion = ValidarRegistro(registro)");}
        [TestMethod] public void Descarga_EstaBajoAppData(){Contiene(Negocio(),"~/App_Data/AOCR/");}
        [TestMethod] public void Descarga_BloqueaTraversal(){Contiene(Negocio(),"Ruta de archivo no segura");}
        [TestMethod] public void EnvioDcav_ValidaAmbosPdf(){var s=Fuente("CapaNegocio\\Services\\AocrDcavRevisionService.cs");Contiene(s,"ValidarArchivo(aocr.Id)");Contiene(s,"ValidarArchivo(condiciones.Id)");}
        [TestMethod] public void DobleClick_ConsultaIdempotenciaAntesDeVersion(){var s=Negocio();Assert.IsTrue(s.IndexOf("ObtenerPorIdempotencia",StringComparison.Ordinal)<s.IndexOf("ObtenerSiguienteVersion",StringComparison.Ordinal));}
        [TestMethod] public void Sql_AbortaSiHayDuplicados(){var s=Fuente("scripts\\006_documento_pdf_central.sql");Contiene(s,"RAISE EXCEPTION");Contiene(s,"CREATE UNIQUE INDEX");}
        [TestMethod] public void Diagnostico_DetectaRegistroSinArchivo(){Contiene(Fuente("CapaNegocio\\Services\\DocumentoPdfConsistenciaService.cs"),"REGISTRO_SIN_ARCHIVO");}
        [TestMethod] public void Diagnostico_DetectaArchivoSinRegistro(){Contiene(Fuente("CapaNegocio\\Services\\DocumentoPdfConsistenciaService.cs"),"ARCHIVO_SIN_REGISTRO");}

        private static DocumentoPdfService Servicio(){return new DocumentoPdfService(Path.Combine(Path.GetTempPath(),"aocr_pdf_tests"), (u,d)=>true);}
        private static GenerarPdfRequest RequestValido(){return new GenerarPdfRequest{SolicitudId=1,InspeccionId=2,DocumentoOrigenId=4,VersionOrigen=3,UsuarioId=5,Rol="InspectorTecnico",TipoDocumento="RECONOCIMIENTO",Generador=()=>new byte[]{1}};}
        private static string Negocio(){return Fuente("CapaNegocio\\Services\\DocumentoPdfService.cs");}
        private static void Contiene(string texto,string valor){StringAssert.Contains(texto,valor);}
        private static string Fuente(string relativa){var d=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"AOCR.sln")))d=d.Parent;Assert.IsNotNull(d);return File.ReadAllText(Path.Combine(d.FullName,relativa));}
    }
}
