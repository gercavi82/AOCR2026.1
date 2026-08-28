using System;
using System.IO;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaModelo.Common;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class DocumentosFinalesWorkflowTests
    {
        [TestMethod] public void EstadosInstitucionales_SonExactosEIndependientes()
        {
            Assert.AreEqual("DOCUMENTOS_FINALES_POR_GENERAR", AocrEstadosProceso.DocumentosFinalesPorGenerar);
            Assert.AreEqual("PENDIENTE_FIRMA_AOCR_DIRDAC", AocrEstadosProceso.PendienteFirmaAocrDirdac);
            Assert.AreEqual("PENDIENTE_FIRMA_CONDICIONES_DCAV", AocrEstadosProceso.PendienteFirmaCondicionesDcav);
            Assert.AreNotEqual(AocrEstadosProceso.AocrFirmadoDirdac, AocrEstadosProceso.CondicionesFirmadasDcav);
        }

        [TestMethod] public void Dirdac_FirmaSoloAocr()
        {
            var service = new DocumentoFirmaService();
            Assert.IsTrue(service.Firmar(R("AOCR", "DIRDAC")).Exitoso);
            Assert.IsFalse(service.Firmar(R("CONDICIONES_LIMITACIONES", "DIRDAC")).Exitoso);
        }

        [TestMethod] public void Dcav_FirmaSoloCondiciones()
        {
            var service = new DocumentoFirmaService();
            Assert.IsTrue(service.Firmar(R("CONDICIONES_LIMITACIONES", "DCAV")).Exitoso);
            Assert.IsFalse(service.Firmar(R("AOCR", "DCAV")).Exitoso);
        }

        [TestMethod] public void PantallaFirma_RespetaRolActivoYEstadoDocumentalExacto()
        {
            var controller = Read("CapaPresentacion/Controllers/FirmaAocrController.cs");
            var service = Read("CapaPresentacion/Services/FirmaAocrServices.cs");
            var view = Read("CapaPresentacion/Views/FirmaAocr/Index.cshtml");
            StringAssert.Contains(controller, "FirmaAocrActiveRoleViewPolicy.Aplicar(model, permisos)");
            StringAssert.Contains(service, "if (permisos.EsDirdacRol)");
            StringAssert.Contains(service, "AocrEstadosProceso.PendienteFirmaAocrDirdac");
            StringAssert.Contains(service, "AocrEstadosProceso.PendienteFirmaCondicionesDcav");
            StringAssert.Contains(service, "documento.Bloqueado");
            StringAssert.Contains(view, "Disponible cuando el Inspector finalice y envíe este documento.");
        }

        [TestMethod] public void Inspector_NoFirmaDocumentosInstitucionales()
        {
            var service = new DocumentoFirmaService();
            Assert.IsFalse(service.Firmar(R("AOCR", "Inspector")).Exitoso);
            Assert.IsFalse(service.Firmar(R("CONDICIONES_LIMITACIONES", "Inspector")).Exitoso);
        }

        [TestMethod] public void BandejaDirdac_UsaEstadoDocumentalExacto()
        {
            var row = new AocrBandejaDocumentoRow { EstadoDocumentoAocr = AocrEstadosProceso.PendienteFirmaAocrDirdac, EstadoDocumentoCondiciones = AocrEstadosProceso.PendienteFirmaCondicionesDcav };
            Assert.IsTrue(AocrFirmaPendientePolicy.EsAocrPendienteFirma(row));
            row.EstadoDocumentoAocr = AocrEstadosProceso.AocrFirmadoDirdac;
            Assert.IsFalse(AocrFirmaPendientePolicy.EsAocrPendienteFirma(row));
            row.EstadoDocumentoAocr = null;
            row.EstadoSolicitudRaw = EstadoSolicitud.AOCR_EnElaboracion;
            row.CertificadoId = 1;
            Assert.IsFalse(AocrFirmaPendientePolicy.EsAocrPendienteFirma(row));
        }

        [TestMethod] public void BandejaDcav_UsaEstadoDocumentalExacto()
        {
            var row = new AocrBandejaDocumentoRow { EstadoDocumentoCondiciones = AocrEstadosProceso.PendienteFirmaCondicionesDcav };
            Assert.IsTrue(AocrFirmaPendientePolicy.EsCondicionesPendienteFirma(row));
            row.EstadoDocumentoCondiciones = AocrEstadosProceso.CondicionesFirmadasDcav;
            Assert.IsFalse(AocrFirmaPendientePolicy.EsCondicionesPendienteFirma(row));
            row.EstadoDocumentoCondiciones = null;
            row.EstadoSolicitudRaw = EstadoSolicitud.AOCR_EnElaboracion;
            row.CertificadoId = 1;
            Assert.IsFalse(AocrFirmaPendientePolicy.EsCondicionesPendienteFirma(row));
        }

        [TestMethod] public void EnvioConjunto_UsaUnaTransaccionYLockDeConcurrencia()
        {
            var source = Read("CapaDatos/DAOs/DocumentosFinalesWorkflowDAO.cs");
            StringAssert.Contains(source, "FinalizarYEncolar");
            StringAssert.Contains(source, "cn.BeginTransaction()");
            StringAssert.Contains(source, "pg_advisory_xact_lock");
            StringAssert.Contains(source, "ValidarEvidencia(aocr");
            StringAssert.Contains(source, "ValidarEvidencia(condiciones");
            StringAssert.Contains(source, "CREATE TABLE IF NOT EXISTS public.aocr_evento_workflow");
        }

        [TestMethod] public void RegistroBorrador_UsaInsertSelectValidoConGuardaIdempotente()
        {
            var source = Read("CapaDatos/DAOs/AocrDocumentoGeneradoDAO.cs");
            StringAssert.Contains(source, "INSERT INTO public.aocr_tbdocumento_generado");
            StringAssert.Contains(source, "SELECT\n                        @codigo_solicitud");
            StringAssert.Contains(source, "WHERE NOT EXISTS (SELECT 1 FROM misma_evidencia)");
            Assert.IsFalse(source.Contains("VALUES\n                    (\n                        @codigo_solicitud"));
        }

        [TestMethod] public void EnvioConjunto_CreaDosRamasAntesDelCommit()
        {
            var source = Read("CapaDatos/DAOs/DocumentosFinalesWorkflowDAO.cs");
            var a = source.IndexOf("ActualizarDocumentoEnviado(cn, tx, aocr", StringComparison.Ordinal);
            var c = source.IndexOf("ActualizarDocumentoEnviado(cn, tx, condiciones", StringComparison.Ordinal);
            var n1 = source.IndexOf("EncolarRama(cn, tx, expediente, request, aocr", StringComparison.Ordinal);
            var n2 = source.IndexOf("EncolarRama(cn, tx, expediente, request, condiciones", StringComparison.Ordinal);
            var commit = source.IndexOf("tx.Commit();", Math.Max(n1, n2), StringComparison.Ordinal);
            Assert.IsTrue(a > 0 && c > a && n1 > c && n2 > n1 && commit > n2);
        }

        [TestMethod] public void Borradores_NoCreanNotificaciones()
        {
            var controller = Read("CapaPresentacion/Controllers/FirmaAocrController.cs");
            var start = controller.IndexOf("public ActionResult GenerarPdf", StringComparison.Ordinal);
            var end = controller.IndexOf("public ActionResult FinalizarDocumentosYEnviarParaFirma", start, StringComparison.Ordinal);
            var body = controller.Substring(start, end - start);
            Assert.IsFalse(body.Contains("EncolarRama"));
            Assert.IsFalse(body.Contains("Notificar"));
        }

        [TestMethod] public void OperacionPublica_ExigeUnaSolaAccionParaDosDocumentos()
        {
            var controller = Read("CapaPresentacion/Controllers/FirmaAocrController.cs");
            StringAssert.Contains(controller, "FinalizarDocumentosYEnviarParaFirma");
            var view = Read("CapaPresentacion/Views/FirmaAocr/Index.cshtml");
            StringAssert.Contains(view, "Finalizar documentos y enviar para firma");
            StringAssert.Contains(view, "data-enviar-documentos");
            StringAssert.Contains(controller, "Request.IsAjaxRequest()");
            StringAssert.Contains(controller, "TempData[\"FirmaAocrMensaje\"]");
            StringAssert.Contains(controller, "redirectUrl = Url.Action(\"PendientesEmisionAocr\", \"Inspeccion\")");
            StringAssert.Contains(controller, "expedientePendienteInspector");
            StringAssert.Contains(Read("CapaPresentacion/Scripts/firma-aocr.js"), "window.location.assign(payload.data.redirectUrl)");
        }

        [TestMethod] public void Outbox_NoEjecutaSmtpEnTransaccion()
        {
            var source = Read("CapaDatos/DAOs/DocumentosFinalesWorkflowDAO.cs");
            StringAssert.Contains(source, "EstadoEmail.Pendiente");
            StringAssert.Contains(source, "EncolarConAdjuntosEnTransaccion");
            Assert.IsFalse(source.Contains("SmtpClient"));
            Assert.IsFalse(source.Contains(".Send("));
        }

        [TestMethod] public void Idempotencia_EsPorDocumentoVersionYDestinatario()
        {
            var source = Read("CapaDatos/DAOs/DocumentosFinalesWorkflowDAO.cs");
            StringAssert.Contains(source, "doc.VersionDocumento + \":PENDIENTE_FIRMA:\" + destinatario.UsuarioId");
            StringAssert.Contains(source, "ON CONFLICT (event_key)");
            StringAssert.Contains(source, "EsEnvioYaConfirmado");
        }

        [TestMethod] public void Finalizacion_ExigeTodosLosDocumentosDeLaMatrizVerificados()
        {
            var source = Read("CapaDatos/DAOs/DocumentosFinalesWorkflowDAO.cs");
            StringAssert.Contains(source, "if (!documentosCompletos)");
            StringAssert.Contains(source, "if (!modificacionSoloCondiciones) ValidarOtraFirma");
            StringAssert.Contains(source, "FinalizarExpedienteYEncolarRt");
        }

        [TestMethod] public void CorreoFinalRt_AdjuntaLosDosPdf()
        {
            var source = Read("CapaDatos/DAOs/DocumentosFinalesWorkflowDAO.cs");
            StringAssert.Contains(source, "AOCR_firmado.pdf");
            StringAssert.Contains(source, "Condiciones_y_Limitaciones_firmadas.pdf");
            StringAssert.Contains(source, "DOCUMENTOS_FINALES_RT:");
        }

        [TestMethod] public void Migracion_EsIdempotenteYReversible()
        {
            var migration = Read("scripts/sql/014_flujo_final_documentos_independientes.sql");
            StringAssert.Contains(migration, "ADD COLUMN IF NOT EXISTS");
            StringAssert.Contains(migration, "CREATE UNIQUE INDEX IF NOT EXISTS ux_documento_final_vigente");
            Assert.IsTrue(File.Exists(Path.Combine(Root(), "scripts/sql/014_flujo_final_documentos_independientes_rollback.sql")));
        }

        [TestMethod] public void Rutas_NoCodificanPrefijoAocr()
        {
            var view = Read("CapaPresentacion/Views/FirmaAocr/Index.cshtml");
            var controller = Read("CapaPresentacion/Controllers/FirmaAocrController.cs");
            Assert.IsFalse(view.Contains("/aocr/"));
            StringAssert.Contains(controller, "Request.ApplicationPath");
        }

        [TestMethod] public void Inspector_NoGeneraAntesDeAprobacionDirdac()
        {
            var controller = Read("CapaPresentacion/Controllers/FirmaAocrController.cs");
            StringAssert.Contains(controller, "InformeAprobadoDireccion(contexto.Informe)");
            StringAssert.Contains(controller, "Solo el Inspector asignado puede generar estos documentos");
            StringAssert.Contains(Read("CapaPresentacion/Services/FirmaAocrServices.cs"), "INFORME_TECNICO_APROBADO_DIRDAC");
        }

        [TestMethod] public void Informe_DirdacApruebaEstadoExactoODevuelveConObservacionYVersiona()
        {
            var controller = Read("CapaPresentacion/Controllers/InspeccionController.cs");
            StringAssert.Contains(controller, "AocrEstadosProceso.PendienteRevisionInformeDirdac");
            StringAssert.Contains(controller, "AocrEstadosProceso.InformeTecnicoAprobadoDirdac");
            StringAssert.Contains(controller, "string.IsNullOrWhiteSpace(observacionRechazo)");
            StringAssert.Contains(controller, "AocrEstadosProceso.InformeTecnicoDevueltoInspector");
            StringAssert.Contains(Read("CapaDatos/DAOs/InspeccionInformeDAO.cs"), "codigo_informe_anterior");
        }

        [TestMethod] public void EnvioYFirma_ValidanIntegridadInspeccionRolesYObservaciones()
        {
            var dao = Read("CapaDatos/DAOs/DocumentosFinalesWorkflowDAO.cs");
            var service = Read("CapaNegocio/Services/DocumentosFinalesWorkflowService.cs");
            StringAssert.Contains(dao, "ValidarSinObservacionesPendientes");
            StringAssert.Contains(dao, "actual.CodigoInspeccion.GetValueOrDefault() != enviada.InspeccionId");
            StringAssert.Contains(dao, "ValidarRolFirma(NormalizarTipo(otro.TipoDocumento), otro.RolFirma)");
            StringAssert.Contains(service, "magic[0] != 0x25");
            StringAssert.Contains(service, "SHA256.Create()");
        }

        [TestMethod] public void Notificaciones_PersistenDatosTiposEnlacesYUsuarioReal()
        {
            var source = Read("CapaDatos/DAOs/DocumentosFinalesWorkflowDAO.cs");
            StringAssert.Contains(source, "AOCR_PENDIENTE_FIRMA_DIRDAC");
            StringAssert.Contains(source, "CONDICIONES_PENDIENTES_FIRMA_DCAV");
            StringAssert.Contains(source, "Solicitud: ");
            StringAssert.Contains(source, "Compania: ");
            StringAssert.Contains(source, "Inspeccion: ");
            StringAssert.Contains(source, "Abrir bandeja autenticada");
            StringAssert.Contains(source, "No se puede crear una notificacion interna con UsuarioId=0");
        }

        [TestMethod] public void FalloSmtp_SeReprogramaSinRepetirTransicionFuncional()
        {
            var queue = Read("CapaDatos/Services/EmailQueueService.cs");
            var workflow = Read("CapaDatos/DAOs/DocumentosFinalesWorkflowDAO.cs");
            StringAssert.Contains(queue, "ReprogramarReintentoAsync");
            StringAssert.Contains(queue, "status = 'PENDIENTE'");
            StringAssert.Contains(workflow, "EncolarConAdjuntosEnTransaccion");
            Assert.IsFalse(workflow.Contains("SmtpClient"));
        }

        [TestMethod] public void RolesFinales_SeResuelvenDesdeCatalogoCentral()
        {
            Assert.IsTrue(AocrRolesInstitucionales.EsDirdac("DIRDAC"));
            Assert.IsTrue(AocrRolesInstitucionales.EsDirdac("DireccionJefaturaTecnica"));
            Assert.IsTrue(AocrRolesInstitucionales.EsDcav("DirectorCertificacionesDcav"));
            Assert.IsFalse(AocrRolesInstitucionales.EsDcav("Inspector"));
            CollectionAssert.Contains(AocrRolesInstitucionales.RolesAcceso, AocrRolesInstitucionales.Administrador);
            StringAssert.Contains(AocrRolesInstitucionales.RolesAccesoMvc, AocrRolesInstitucionales.Administrador);
            CollectionAssert.Contains(AocrRolesInstitucionales.RolesAcceso, AocrRolesInstitucionales.Coordinacion);
            StringAssert.Contains(AocrRolesInstitucionales.RolesAccesoMvc, AocrRolesInstitucionales.Coordinacion);
            Assert.IsFalse(AocrRolesInstitucionales.EsDirdac(AocrRolesInstitucionales.Administrador));
            Assert.IsFalse(AocrRolesInstitucionales.EsDcav(AocrRolesInstitucionales.Administrador));
            Assert.IsFalse(AocrRolesInstitucionales.EsDirdac(AocrRolesInstitucionales.Coordinacion));
            Assert.IsFalse(AocrRolesInstitucionales.EsDcav(AocrRolesInstitucionales.Coordinacion));
        }


        [TestMethod, TestCategory("Integration")] public void EsquemaReal_DocumentosVigentesSePuedeConsultar()
        {
            var documento = new DocumentosFinalesWorkflowDAO().ObtenerVigente(1, "RECONOCIMIENTO");
            Assert.IsTrue(documento == null || documento.CodigoSolicitud == 1);
        }

        [TestMethod, TestCategory("Integration")] public void EsquemaReal_BandejasCompartenConsultaEjecutable()
        {
            var filas = new AocrBandejaDAO().ListarGeneradasFirmadas();
            Assert.IsNotNull(filas);
        }

        [TestMethod, TestCategory("Integration")] public void EsquemaReal_TransaccionInvalidaHaceRollbackSinEfectosParciales()
        {
            var dao = new DocumentosFinalesWorkflowDAO();
            Assert.ThrowsException<InvalidOperationException>(() => dao.FinalizarYEncolar(new DocumentoFinalEnvioRequest
            {
                SolicitudId = 2147480000,
                InspeccionId = 2147480000,
                InspectorId = 2147480000,
                InspectorNombre = "TEST_ROLLBACK",
                Aocr = new DocumentoFinalEvidencia(),
                Condiciones = new DocumentoFinalEvidencia()
            }));
        }

        private static FirmaDocumentoRequest R(string tipo, string rol) { return new FirmaDocumentoRequest { SolicitudId = 1, UsuarioId = 2, TipoDocumento = tipo, RolSolicitado = rol }; }
        private static string Read(string path) { return File.ReadAllText(Path.Combine(Root(), path.Replace('/', Path.DirectorySeparatorChar))); }
        private static string Root() { return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")); }
    }
}
