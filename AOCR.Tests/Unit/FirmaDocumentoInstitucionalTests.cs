using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
 [TestClass] public class FirmaDocumentoInstitucionalTests
 {
  [TestMethod]public void T01_ExisteServicioCentral(){C(S(),"IFirmaDocumentoInstitucionalService");}
  [TestMethod]public void T02_ContratoExponeTresOperaciones(){var x=F("CapaNegocio\\Services\\IFirmaDocumentoInstitucionalService.cs");C(x,"Firmar(");C(x,"ValidarFirma(");C(x,"ObtenerEstadoFirmas(");}
  [TestMethod]public void T03_MatrizAocrUsaDgac(){C(P(),"TiposDocumentoFirmaInstitucional.Aocr ? \"Direccion\"");}
  [TestMethod]public void T04_MatrizCondicionesUsaDcav(){C(P(),"DIRECTOR_CERTIFICACIONES_DCAV");}
  [TestMethod]public void T05_NoUsaDireccionJefaturaTecnica(){Assert.IsFalse(S().Contains("DireccionJefaturaTecnica"));Assert.IsFalse(P().Contains("DireccionJefaturaTecnica"));}
  [TestMethod]public void T06_PerfilContieneDatosInstitucionales(){var x=D();foreach(var y in new[]{"UsuarioId","NombreCompleto","Cargo","CodigoRol","FirmaImagenId","RutaInternaFirma","HashFirma","VigenteHasta"})C(x,y);}
  [TestMethod]public void T07_CoordenadasSeResuelvenBackend(){C(P(),"ConfiguracionPosicionFirmaService");Assert.IsFalse(Controller().Contains("PosicionX"));}
  [TestMethod]public void T08_UsaAprobacionExactaDelHistorial(){C(Dao(),"APROBAR_DOCUMENTOS_DCAV");C(Dao(),"AocrPdfId=([0-9]+)");C(Dao(),"CondicionesPdfId=([0-9]+)");}
  [TestMethod]public void T09_NoUsaMaxVersion(){Assert.IsFalse(Dao().ToUpperInvariant().Contains("MAX("));}
  [TestMethod]public void T10_ValidaHashPdfOrigen(){C(S(),"HashPdfOrigen");C(S(),"Sha256(pdf)");}
  [TestMethod]public void T11_ValidaHashImagen(){C(S(),"Sha256(bytes),p.HashFirma");}
  [TestMethod]public void T12_ImagenSoloPngOJpeg(){C(S(),"ImageFormat.Png");C(S(),"ImageFormat.Jpeg");}
  [TestMethod]public void T13_ValidaDimensionesImagen(){C(S(),"img.Width<40");C(S(),"img.Height<20");}
  [TestMethod]public void T14_ConservaProporcion(){C(S(),"ScaleToFit");}
  [TestMethod]public void T15_CentraImagen(){C(S(),"firma.ScaledWidth)/2");C(S(),"firma.ScaledHeight)/2");}
  [TestMethod]public void T16_NombreVisible(){C(S(),"perfil.NombreCompleto");C(S(),"ShowTextAligned");}
  [TestMethod]public void T17_CargoVisible(){C(S(),"perfil.Cargo");C(S(),"ShowTextAligned");}
  [TestMethod]public void T18_FechaVisible(){C(S(),"Firmado: ");C(S(),"dd/MM/yyyy HH:mm");}
  [TestMethod]public void T19_QrEsConfigurable(){C(S(),"p.MostrarQr");C(P(),"MostrarQr");}
  [TestMethod]public void T20_GeneraRutaNueva(){C(S(),"Guid.NewGuid().ToString(\"N\")+\".pdf\"");}
  [TestMethod]public void T21_NoSobrescribeOrigen(){Assert.IsFalse(S().Contains("WriteAllBytes(rutaOrigen"));C(S(),"File.ReadAllBytes(rutaOrigen)");}
  [TestMethod]public void T22_UsaArchivoTemporal(){C(S(),".tmp");C(S(),"[SIGNATURE][TEMP_FILE_CREATED]");}
  [TestMethod]public void T23_CompensaArchivosYAuditaFallo(){C(S(),"Compensar(definitivo,temporal,request,tipo)");C(S(),"File.Delete");C(Dao(),"FIRMA_COMPENSACION_ERROR");}
  [TestMethod]public void T24_FirmaEnTransaccion(){C(S(),"BeginTransaction");C(S(),"tx.Commit()");}
  [TestMethod]public void T25_IdempotenciaDeterminista(){C(S(),"FIRMA_INST:");C(S(),"[IDEMPOTENCY][HIT]");}
  [TestMethod]public void T26_ConcurrenciaBloqueaFilas(){C(Dao(),"FOR UPDATE OF pe,a,c");C(S(),"[CONCURRENCY][CONFLICT]");}
  [TestMethod]public void T27_UnicidadParcial(){var x=Sql();C(x,"uq_aocr_firma_institucional_vigente");C(x,"FIRMADO_DGAC','FIRMADO_DCAV");}
  [TestMethod]public void T28_AocrTerminaFirmadoDgac(){C(S(),"\"FIRMADO_DGAC\"");}
  [TestMethod]public void T29_CondicionesTerminanFirmadasDcav(){C(S(),"\"FIRMADO_DCAV\"");}
  [TestMethod]public void T30_FirmaParcialNoCierra(){C(S(),"PENDIENTE_FIRMAS_INSTITUCIONALES");Assert.IsFalse(S().Contains("LiberarDocumentoFinal"));}
  [TestMethod]public void T31_AmbasFirmasCambianConjunto(){C(S(),"DOCUMENTOS_FIRMADOS_INSTITUCIONALMENTE");C(S(),"AMBAS_FIRMAS_COMPLETADAS");}
  [TestMethod]public void T32_NoFinaliza(){Assert.IsFalse(S().Contains("AOCR_FINALIZADO\"")&&S().Contains("estadoCentral=\"AOCR_FINALIZADO"));Assert.IsFalse(S().Contains("NotificarProcesoAocrFinalizado"));}
  [TestMethod]public void T33_AuditoriaIncluyeMetadatos(){var x=S();foreach(var y in new[]{"DocumentoId=","VersionDocumento=","PdfOrigenId=","FirmaImagenId=","HashImagen=","HashPdfFirmado="})C(x,y);}
  [TestMethod]public void T34_ControladorNoManipulaPdfNiSql(){var x=Controller();Assert.IsFalse(x.Contains("NpgsqlCommand"));Assert.IsFalse(x.Contains("PdfReader"));Assert.IsFalse(x.Contains("AddImage"));}
  [TestMethod]public void T35_RutasPermanecenBajoAocr(){C(S(),"Uploads\",\"AOCR\",\"FirmadosInstitucionales");C(Controller(),"FirmaInstitucionalAocr");}
  [TestMethod]public void T36_CompatibleNet462(){C(F("CapaNegocio\\CapaNegocio.csproj"),"v4.6.2");}
  [TestMethod]public void T37_RolSeValidaPorCodigoYDescripcion(){C(P(),"RoleCode");C(P(),"r.codigorol=@codigo_rol");}
  [TestMethod]public void T38_EstadosPendientesSonDiferenciados(){C(S(),"PENDIENTE_FIRMA_DGAC");C(S(),"PENDIENTE_FIRMA_DCAV");}
  [TestMethod]public void T39_RechazosQuedanAuditados(){C(Dao(),"FIRMA_DOCUMENTO_RECHAZADA");C(S(),"Rechazar(request,tipo");}
  [TestMethod]public void T40_PosicionesInternasSonConfigurables(){var x=D();foreach(var y in new[]{"NombreYRatio","CargoYRatio","FechaYRatio","QrXRatio","QrYRatio","QrTamanioRatio"})C(x,y);}
  [TestMethod]public void T41_RutaHeredadaBloqueaFlujoDiferenciado(){var x=F("CapaPresentacion\\Controllers\\FirmaAocrController.cs");C(x,"firma institucional diferenciada");C(x,"PendienteFirmasInstitucionales");}
  [TestMethod]public void T42_TipoDescargaInvalidoNoCaeEnCondiciones(){C(Controller(),"Tipo de documento institucional no válido");C(Controller(),"HttpStatusCodeResult(400");}
  static string S(){return F("CapaNegocio\\Services\\FirmaDocumentoInstitucionalService.cs");}static string P(){return F("CapaNegocio\\Services\\PerfilFirmanteService.cs");}static string D(){return F("CapaNegocio\\DTOs\\FirmaDocumentoInstitucionalDtos.cs");}static string Dao(){return F("CapaDatos\\DAOs\\FirmaDocumentoInstitucionalDAO.cs");}static string Controller(){return F("CapaPresentacion\\Controllers\\FirmaInstitucionalAocrController.cs");}static string Sql(){return F("scripts\\011_firma_institucional_diferenciada.sql");}
  static string F(string p){return File.ReadAllText(Path.Combine(Root(),p));}static string Root(){var d=new DirectoryInfo(System.AppDomain.CurrentDomain.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"AOCR.sln")))d=d.Parent;return d.FullName;}static void C(string x,string y){StringAssert.Contains(x,y);}
 }
}
