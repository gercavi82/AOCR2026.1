using System;
using System.IO;
using System.Security.Cryptography;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    public sealed class DocumentosFinalesWorkflowService
    {
        private readonly DocumentosFinalesWorkflowDAO _dao;

        public DocumentosFinalesWorkflowService() : this(new DocumentosFinalesWorkflowDAO()) { }

        public DocumentosFinalesWorkflowService(DocumentosFinalesWorkflowDAO dao)
        {
            _dao = dao ?? throw new ArgumentNullException("dao");
        }

        public DocumentosFinalesResultado FinalizarDocumentosYEnviarParaFirma(
            int solicitudId,
            int inspeccionId,
            int inspectorId,
            string inspectorNombre,
            string baseUrl,
            Func<string, string> resolverRutaFisica)
        {
            if (solicitudId <= 0 || inspeccionId <= 0 || inspectorId <= 0)
                return Error("Solicitud, inspeccion o Inspector autenticado invalidos.");

            try
            {
                var solicitud = new SolicitudAOCRDAO().ObtenerPorCodigo(solicitudId);
                var plan = new AocrCierrePorTipoTramiteService().Resolver(solicitud);
                if (!plan.EsValido) return Error(plan.Motivo);

                var aocr = plan.GenerarAocr ? _dao.ObtenerVigente(solicitudId, "RECONOCIMIENTO") : null;
                var condiciones = plan.GenerarCondiciones ? _dao.ObtenerVigente(solicitudId, "CONDICIONES_LIMITACIONES") : null;
                var evidenciaAocr = plan.GenerarAocr ? VerificarPdf(aocr, resolverRutaFisica, false) : null;
                var evidenciaCondiciones = plan.GenerarCondiciones ? VerificarPdf(condiciones, resolverRutaFisica, false) : null;
                return _dao.FinalizarYEncolar(new DocumentoFinalEnvioRequest
                {
                    SolicitudId = solicitudId,
                    InspeccionId = inspeccionId,
                    InspectorId = inspectorId,
                    InspectorNombre = inspectorNombre,
                    BaseUrl = baseUrl,
                    Aocr = evidenciaAocr,
                    Condiciones = evidenciaCondiciones,
                    RequiereAocr = plan.GenerarAocr,
                    RequiereCondiciones = plan.GenerarCondiciones,
                    VersionConcurrencia = Math.Max(aocr != null ? aocr.VersionConcurrencia : 0L, condiciones != null ? condiciones.VersionConcurrencia : 0L)
                });
            }
            catch (UnauthorizedAccessException ex) { return Error(ex.Message); }
            catch (InvalidOperationException ex) { return Error(ex.Message); }
            catch (Exception) { return Error("No fue posible finalizar los documentos. La transaccion fue revertida."); }
        }

        public DocumentosFinalesResultado RegistrarFirmaInstitucional(
            DocumentoFinalFirmaRequest request,
            Func<string, string> resolverRutaFisica)
        {
            if (request == null || request.SolicitudId <= 0 || request.InspeccionId <= 0 || request.UsuarioId <= 0)
                return Error("Contexto de firma institucional invalido.");

            try
            {
                VerificarPdfFirmadoSolicitado(request, resolverRutaFisica);
                var otroTipo = NormalizarTipo(request.TipoDocumento) == "RECONOCIMIENTO" ? "CONDICIONES_LIMITACIONES" : "RECONOCIMIENTO";
                var otro = _dao.ObtenerVigente(request.SolicitudId, otroTipo);
                DocumentoFinalEvidencia otraEvidencia = null;
                if (otro != null && !string.IsNullOrWhiteSpace(otro.RutaPdfFirmado))
                    otraEvidencia = VerificarPdf(otro, resolverRutaFisica, true);
                return _dao.RegistrarFirmaYFinalizar(request, otraEvidencia);
            }
            catch (UnauthorizedAccessException ex) { return Error(ex.Message); }
            catch (InvalidOperationException ex) { return Error(ex.Message); }
            catch (Exception) { return Error("No fue posible registrar la firma. La transaccion fue revertida."); }
        }

        private static DocumentoFinalEvidencia VerificarPdf(AocrDocumentoGenerado documento, Func<string, string> resolver, bool firmado)
        {
            if (documento == null) throw new InvalidOperationException("Deben existir AOCR y Condiciones y Limitaciones vigentes.");
            var rutaPersistida = firmado ? documento.RutaPdfFirmado : documento.RutaDocumento;
            var hashPersistido = firmado ? documento.HashPdfFirmado : documento.HashPdf;
            var ruta = resolver != null ? resolver(rutaPersistida) : rutaPersistida;
            if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
                throw new InvalidOperationException("El PDF vigente de " + documento.TipoDocumento + " no existe fisicamente.");
            var info = new FileInfo(ruta);
            if (info.Length <= 4) throw new InvalidOperationException("El PDF de " + documento.TipoDocumento + " esta vacio.");
            using (var stream = File.OpenRead(ruta))
            {
                var magic = new byte[4];
                if (stream.Read(magic, 0, magic.Length) != magic.Length || magic[0] != 0x25 || magic[1] != 0x50 || magic[2] != 0x44 || magic[3] != 0x46)
                    throw new InvalidOperationException("El archivo de " + documento.TipoDocumento + " no es un PDF valido.");
            }
            var hash = Sha256(ruta);
            if (string.IsNullOrWhiteSpace(hashPersistido) || !string.Equals(hash, hashPersistido, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La integridad SHA-256 de " + documento.TipoDocumento + " no coincide.");
            var bytesPersistidos = firmado ? new FileInfo(ruta).Length : documento.TamanioPdf.GetValueOrDefault();
            if (!firmado && bytesPersistidos != info.Length)
                throw new InvalidOperationException("El tamanio persistido de " + documento.TipoDocumento + " no coincide.");
            return new DocumentoFinalEvidencia { DocumentoId = documento.CodigoDocumento, InspeccionId = documento.CodigoInspeccion.GetValueOrDefault(), Version = documento.VersionDocumento, TipoDocumento = documento.TipoDocumento, RutaPdf = rutaPersistida, HashPdf = hash, TamanioPdf = info.Length };
        }

        private static void VerificarPdfFirmadoSolicitado(DocumentoFinalFirmaRequest request, Func<string, string> resolver)
        {
            var ruta = resolver != null ? resolver(request.RutaPdfFirmado) : request.RutaPdfFirmado;
            if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta)) throw new InvalidOperationException("El PDF firmado no existe fisicamente.");
            var info = new FileInfo(ruta);
            if (info.Length <= 4 || info.Length != request.TamanioPdfFirmado) throw new InvalidOperationException("El tamanio del PDF firmado no coincide.");
            using (var stream = File.OpenRead(ruta))
            {
                var magic = new byte[4];
                if (stream.Read(magic, 0, magic.Length) != magic.Length || magic[0] != 0x25 || magic[1] != 0x50 || magic[2] != 0x44 || magic[3] != 0x46)
                    throw new InvalidOperationException("El archivo firmado no es un PDF valido.");
            }
            var hash = Sha256(ruta);
            if (string.IsNullOrWhiteSpace(request.HashPdfFirmado) || !string.Equals(hash, request.HashPdfFirmado, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La integridad SHA-256 del PDF firmado no coincide.");
        }

        private static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string NormalizarTipo(string tipo)
        {
            var value = (tipo ?? string.Empty).Trim().ToUpperInvariant();
            return value == "AOCR" || value == "RECONOCIMIENTO" ? "RECONOCIMIENTO" : "CONDICIONES_LIMITACIONES";
        }

        private static DocumentosFinalesResultado Error(string mensaje)
        {
            return new DocumentosFinalesResultado { Exitoso = false, Mensaje = mensaje ?? "Operacion rechazada." };
        }
    }
}
