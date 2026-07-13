using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaNegocio.DTOs.DocumentosPdf;

namespace CapaNegocio.Services
{
    public interface IDocumentoPdfService
    {
        ResultadoGeneracionPdf Generar(GenerarPdfRequest request);
        DocumentoPdfDto ObtenerVigente(int solicitudId, int inspeccionId, string tipoDocumento);
        DocumentoPdfDto ObtenerPorId(int documentoPdfId);
        IList<DocumentoPdfDto> ObtenerVersiones(int solicitudId, int inspeccionId, string tipoDocumento);
        ResultadoValidacionPdf ValidarArchivo(int documentoPdfId);
        Stream ObtenerArchivoAutorizado(int documentoPdfId, int usuarioId);
    }

    public sealed class DocumentoPdfService : IDocumentoPdfService
    {
        private static readonly string[] EstadosEditables = { "BORRADOR", "EN_REVISION_INSPECTOR", "GENERADO", "CORRECCION_INSPECTOR", "CORREGIDO_INSPECTOR" };
        private static readonly string[] RolesGeneradores = { "INSPECTOR", "INSPECTORTECNICO", "TECNICO", "ADMINISTRADOR" };
        private readonly DocumentoPdfDAO _dao;
        private readonly string _root;
        private readonly Func<int, DocumentoPdfDto, bool> _autorizarLectura;

        public DocumentoPdfService(string almacenamientoProtegido, Func<int, DocumentoPdfDto, bool> autorizarLectura = null)
            : this(almacenamientoProtegido, new DocumentoPdfDAO(), autorizarLectura) { }

        internal DocumentoPdfService(string almacenamientoProtegido, DocumentoPdfDAO dao, Func<int, DocumentoPdfDto, bool> autorizarLectura)
        {
            if (string.IsNullOrWhiteSpace(almacenamientoProtegido)) throw new ArgumentException("Debe configurar el almacenamiento protegido.", "almacenamientoProtegido");
            _root = Path.GetFullPath(almacenamientoProtegido).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            _dao = dao ?? throw new ArgumentNullException("dao");
            _autorizarLectura = autorizarLectura;
        }

        public ResultadoGeneracionPdf Generar(GenerarPdfRequest request)
        {
            ValidarRequest(request);
            var tipo = NormalizarTipo(request.TipoDocumento);
            var origen = _dao.ValidarOrigen(request.SolicitudId, request.InspeccionId, request.DocumentoOrigenId, tipo, request.UsuarioId);
            ValidarOrigen(request, origen);
            var clave = CrearClaveIdempotencia(request, tipo);
            var lockKey = "DOCUMENTO_PDF:" + request.SolicitudId + ":" + request.InspeccionId + ":" + tipo;
            string temporal = null;
            string destino = null;
            bool archivoMovido = false;

            Trace.TraceInformation("[PDF][GENERAR_IN] SolicitudId=" + request.SolicitudId + ";InspeccionId=" + request.InspeccionId + ";Tipo=" + tipo + ";Usuario=" + request.UsuarioId + ";CorrelationId=" + (request.CorrelationId ?? string.Empty));
            using (var cn = _dao.CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        _dao.BloquearGeneracion(cn, tx, lockKey);
                        var existente = _dao.ObtenerPorIdempotencia(cn, tx, request.InspeccionId, tipo, clave);
                        if (existente != null)
                        {
                            tx.Commit();
                            var validacionExistente = ValidarRegistro(existente);
                            if (!validacionExistente.Valido) throw new DocumentoPdfException(409, "La operación ya existe, pero su archivo no supera la validación de integridad.");
                            Trace.TraceInformation("[PDF][IDEMPOTENT_HIT] DocumentoPdfId=" + existente.Id + ";Clave=" + clave);
                            return Exito(existente, true);
                        }

                        var version = _dao.ObtenerSiguienteVersion(cn, tx, request.InspeccionId, tipo);
                        var carpeta = ConstruirCarpeta(request.SolicitudId, request.InspeccionId, tipo, version);
                        Directory.CreateDirectory(carpeta);
                        temporal = Path.Combine(carpeta, ".tmp_" + Guid.NewGuid().ToString("N") + ".pdf");
                        var bytes = request.Generador();
                        if (bytes == null || bytes.Length == 0) throw new DocumentoPdfException(500, "El generador no produjo contenido PDF.");
                        File.WriteAllBytes(temporal, bytes);
                        var fisica = ValidarFisicamente(temporal, null, null);
                        if (!fisica.Valido) throw new DocumentoPdfException(500, fisica.Mensaje);

                        var tokenTipo = TipoParaArchivo(tipo);
                        var nombre = tokenTipo + "_" + request.SolicitudId + "_" + request.InspeccionId + "_v" + version.ToString("000") + ".pdf";
                        destino = Path.Combine(carpeta, nombre);
                        if (File.Exists(destino)) throw new DocumentoPdfException(409, "Ya existe el archivo físico de esta versión; se requiere diagnóstico de consistencia.");
                        File.Move(temporal, destino);
                        temporal = null;
                        archivoMovido = true;

                        var rutaLogica = ConstruirRutaLogica(request.SolicitudId, request.InspeccionId, tipo, version, nombre);
                        var registro = _dao.Registrar(cn, tx, request.SolicitudId, request.InspeccionId,
                            request.DocumentoOrigenId, tipo, version, request.VersionOrigen, request.UsuarioId,
                            request.Rol, rutaLogica, nombre, fisica.HashCalculado, fisica.TamanoCalculado, clave);
                        tx.Commit();
                        Trace.TraceInformation("[PDF][GENERAR_OK] DocumentoPdfId=" + registro.Id + ";Version=" + registro.Version + ";Hash=" + registro.HashSha256 + ";Tamano=" + registro.TamanoBytes);
                        return Exito(registro, false);
                    }
                    catch (Exception ex)
                    {
                        try { tx.Rollback(); } catch { }
                        CompensarArchivo(temporal, "TEMP", ex);
                        if (archivoMovido) CompensarArchivo(destino, "FINAL", ex);
                        Trace.TraceError("[PDF][GENERAR_ERROR] SolicitudId=" + request.SolicitudId + ";InspeccionId=" + request.InspeccionId + ";Tipo=" + tipo + ";Error=" + ex);
                        throw;
                    }
                }
            }
        }

        public DocumentoPdfDto ObtenerVigente(int solicitudId, int inspeccionId, string tipoDocumento)
        {
            return Map(_dao.ObtenerVigente(solicitudId, inspeccionId, NormalizarTipo(tipoDocumento)));
        }

        public DocumentoPdfDto ObtenerPorId(int documentoPdfId) { return Map(_dao.ObtenerPorId(documentoPdfId)); }

        public IList<DocumentoPdfDto> ObtenerVersiones(int solicitudId, int inspeccionId, string tipoDocumento)
        {
            return _dao.ObtenerVersiones(solicitudId, inspeccionId, NormalizarTipo(tipoDocumento)).Select(Map).ToList();
        }

        public ResultadoValidacionPdf ValidarArchivo(int documentoPdfId)
        {
            var registro = _dao.ObtenerPorId(documentoPdfId);
            if (registro == null) return Invalido(404, "Documento PDF inexistente.");
            var resultado = ValidarRegistro(registro);
            if (!resultado.Valido) Trace.TraceWarning("[PDF][INTEGRITY_ERROR] DocumentoPdfId=" + documentoPdfId + ";Motivo=" + resultado.Mensaje);
            return resultado;
        }

        public Stream ObtenerArchivoAutorizado(int documentoPdfId, int usuarioId)
        {
            if (usuarioId <= 0) throw new DocumentoPdfException(401, "Usuario no autenticado.");
            var registro = _dao.ObtenerPorId(documentoPdfId);
            if (registro == null) throw new DocumentoPdfException(404, "Documento PDF inexistente.");
            var dto = Map(registro);
            if (_autorizarLectura == null || !_autorizarLectura(usuarioId, dto))
            {
                Trace.TraceWarning("[PDF][DOWNLOAD_DENY] DocumentoPdfId=" + documentoPdfId + ";Usuario=" + usuarioId);
                throw new DocumentoPdfException(403, "No tiene autorización para descargar este documento.");
            }
            var validacion = ValidarRegistro(registro);
            if (!validacion.Valido) throw new DocumentoPdfException(validacion.Codigo, validacion.Mensaje);
            Trace.TraceInformation("[PDF][DOWNLOAD_OK] DocumentoPdfId=" + documentoPdfId + ";Usuario=" + usuarioId);
            return new FileStream(ResolverRutaFisica(registro.RutaLogica), FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public static string CrearClaveIdempotencia(GenerarPdfRequest request, string tipoNormalizado)
        {
            if (request == null) throw new ArgumentNullException("request");
            return request.SolicitudId + ":" + request.InspeccionId + ":" + request.DocumentoOrigenId + ":" + tipoNormalizado + ":" + request.VersionOrigen + ":GENERAR_PDF";
        }

        private void ValidarRequest(GenerarPdfRequest request)
        {
            if (request == null) throw new DocumentoPdfException(400, "Solicitud de generación inválida.");
            if (request.SolicitudId <= 0 || request.InspeccionId <= 0 || request.DocumentoOrigenId <= 0 || request.UsuarioId <= 0 || request.VersionOrigen <= 0)
                throw new DocumentoPdfException(400, "Solicitud, inspección, documento, usuario y versión son obligatorios.");
            if (request.Generador == null) throw new DocumentoPdfException(400, "No se configuró el generador PDF.");
            var rol = NormalizarToken(request.Rol);
            if (!RolesGeneradores.Contains(rol)) throw new DocumentoPdfException(403, "El rol no puede generar documentos oficiales.");
            if (request.CamposFaltantes != null && request.CamposFaltantes.Any(x => !string.IsNullOrWhiteSpace(x)))
                throw new DocumentoPdfException(422, "Campos obligatorios incompletos: " + string.Join(", ", request.CamposFaltantes.Where(x => !string.IsNullOrWhiteSpace(x))));
        }

        private static void ValidarOrigen(GenerarPdfRequest request, DocumentoPdfOrigenValidacion origen)
        {
            if (origen == null || !origen.Existe) throw new DocumentoPdfException(404, "Documento origen inexistente o no vigente.");
            if (!origen.SolicitudActiva) throw new DocumentoPdfException(409, "La solicitud no está activa.");
            if (!origen.InspectorAsignado && !string.Equals(NormalizarToken(request.Rol), "ADMINISTRADOR", StringComparison.Ordinal))
                throw new DocumentoPdfException(403, "Solo el Inspector asignado puede generar el PDF.");
            if (origen.Firmado) throw new DocumentoPdfException(409, "El documento está firmado y es inmutable.");
            if (origen.Version != request.VersionOrigen) throw new DocumentoPdfException(409, "Conflicto de versión del documento origen.");
            if (request.VersionRegistroEsperada > 0 && origen.Version != request.VersionRegistroEsperada) throw new DocumentoPdfException(409, "Conflicto de versión de registro.");
            if (!string.IsNullOrWhiteSpace(request.EstadoEsperado) && !string.Equals(origen.Estado, request.EstadoEsperado, StringComparison.OrdinalIgnoreCase))
                throw new DocumentoPdfException(409, "El estado del documento cambió durante la operación.");
            if (!EstadosEditables.Contains((origen.Estado ?? string.Empty).Trim().ToUpperInvariant())) throw new DocumentoPdfException(409, "El documento no está en un estado editable.");
            if (!string.IsNullOrWhiteSpace(request.CodigoCompania) && !string.Equals(origen.CodigoCompania, request.CodigoCompania, StringComparison.OrdinalIgnoreCase))
                throw new DocumentoPdfException(403, "El documento no pertenece a la compañía autorizada.");
        }

        private ResultadoValidacionPdf ValidarRegistro(DocumentoPdfRegistro registro)
        {
            string path;
            try { path = ResolverRutaFisica(registro.RutaLogica); }
            catch (DocumentoPdfException ex) { return Invalido(ex.Codigo, ex.Message); }
            return ValidarFisicamente(path, registro.HashSha256, registro.TamanoBytes);
        }

        private static ResultadoValidacionPdf ValidarFisicamente(string path, string hashEsperado, long? tamanoEsperado)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return Invalido(404, "El archivo PDF no existe físicamente.");
            var info = new FileInfo(path);
            if (info.Length < 5) return Invalido(409, "El archivo PDF está vacío o truncado.");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var header = new byte[5];
                if (stream.Read(header, 0, header.Length) != header.Length || header[0] != 0x25 || header[1] != 0x50 || header[2] != 0x44 || header[3] != 0x46 || header[4] != 0x2D)
                    return Invalido(409, "El archivo no tiene una cabecera PDF válida.");
                stream.Position = 0;
                string hash;
                using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
                if (tamanoEsperado.HasValue && tamanoEsperado.Value > 0 && info.Length != tamanoEsperado.Value)
                    return new ResultadoValidacionPdf { Valido=false,Codigo=409,Mensaje="El tamaño físico no coincide con el registro.",HashCalculado=hash,TamanoCalculado=info.Length };
                if (!string.IsNullOrWhiteSpace(hashEsperado) && !string.Equals(hash, hashEsperado, StringComparison.OrdinalIgnoreCase))
                    return new ResultadoValidacionPdf { Valido=false,Codigo=409,Mensaje="El hash SHA-256 no coincide con el registro.",HashCalculado=hash,TamanoCalculado=info.Length };
                return new ResultadoValidacionPdf { Valido=true,Codigo=200,Mensaje="Archivo PDF íntegro.",HashCalculado=hash,TamanoCalculado=info.Length };
            }
        }

        private string ConstruirCarpeta(int solicitudId, int inspeccionId, string tipo, int version)
        {
            var path = Path.Combine(_root, solicitudId.ToString(), inspeccionId.ToString(), TipoParaArchivo(tipo), "v" + version.ToString("000"));
            AsegurarDentroDeRaiz(path); return path;
        }

        private static string ConstruirRutaLogica(int solicitudId, int inspeccionId, string tipo, int version, string nombre)
        {
            return "~/App_Data/AOCR/" + solicitudId + "/" + inspeccionId + "/" + TipoParaArchivo(tipo) + "/v" + version.ToString("000") + "/" + nombre;
        }

        private string ResolverRutaFisica(string rutaLogica)
        {
            if (string.IsNullOrWhiteSpace(rutaLogica)) throw new DocumentoPdfException(409, "El registro no contiene una ruta lógica.");
            var normalizada = rutaLogica.Replace('\\', '/').Trim();
            const string prefijo1 = "~/App_Data/AOCR/";
            const string prefijo2 = "/App_Data/AOCR/";
            string relativa;
            if (normalizada.StartsWith(prefijo1, StringComparison.OrdinalIgnoreCase)) relativa = normalizada.Substring(prefijo1.Length);
            else if (normalizada.StartsWith(prefijo2, StringComparison.OrdinalIgnoreCase)) relativa = normalizada.Substring(prefijo2.Length);
            else throw new DocumentoPdfException(409, "La ruta lógica está fuera del almacenamiento protegido.");
            var path = Path.GetFullPath(Path.Combine(_root, relativa.Replace('/', Path.DirectorySeparatorChar)));
            AsegurarDentroDeRaiz(path); return path;
        }

        private void AsegurarDentroDeRaiz(string path)
        {
            var full = Path.GetFullPath(path);
            var prefix = _root + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new DocumentoPdfException(409, "Ruta de archivo no segura.");
        }

        private static void CompensarArchivo(string path, string etapa, Exception original)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            try { File.Delete(path); }
            catch (Exception compensation) { Trace.TraceError("[PDF][ORPHAN_FILE] Etapa=" + etapa + ";Ruta=" + path + ";ErrorOriginal=" + original.Message + ";ErrorCompensacion=" + compensation); }
        }

        private static string NormalizarTipo(string tipo)
        {
            var token = NormalizarToken(tipo);
            if (token == "AOCR" || token == "RECONOCIMIENTO") return "RECONOCIMIENTO";
            if (token == "CONDICIONES" || token == "CONDICIONESLIMITACIONES") return "CONDICIONES_LIMITACIONES";
            throw new DocumentoPdfException(400, "Tipo de documento oficial no soportado.");
        }

        private static string NormalizarToken(string value)
        {
            return new string((value ?? string.Empty).Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        }

        private static string TipoParaArchivo(string tipo) { return tipo == "RECONOCIMIENTO" ? "AOCR" : "CONDICIONES"; }
        private static ResultadoValidacionPdf Invalido(int codigo, string mensaje) { return new ResultadoValidacionPdf { Valido=false,Codigo=codigo,Mensaje=mensaje }; }
        private static ResultadoGeneracionPdf Exito(DocumentoPdfRegistro registro, bool yaProcesado) { return new ResultadoGeneracionPdf { Exitoso=true,YaProcesado=yaProcesado,Codigo=200,Mensaje=yaProcesado?"La generación ya había sido procesada.":"PDF generado y persistido correctamente.",Documento=Map(registro) }; }

        private static DocumentoPdfDto Map(DocumentoPdfRegistro r)
        {
            if (r == null) return null;
            return new DocumentoPdfDto { Id=r.Id,SolicitudId=r.SolicitudId,InspeccionId=r.InspeccionId,DocumentoOrigenId=r.DocumentoOrigenId,
                TipoDocumento=r.TipoDocumento,Version=r.Version,Estado=r.Estado,NombreArchivo=r.NombreArchivo,RutaLogica=r.RutaLogica,
                MimeType=r.MimeType,TamanoBytes=r.TamanoBytes,HashSha256=r.HashSha256,Vigente=r.Vigente,Firmado=r.Firmado,
                FechaGeneracion=r.FechaGeneracion,UsuarioGeneradorId=r.UsuarioGeneradorId,FechaFirma=r.FechaFirma,UsuarioFirmaId=r.UsuarioFirmaId,
                Eliminado=false,FechaCreacion=r.FechaGeneracion,FechaModificacion=r.FechaGeneracion,VersionRegistro=r.VersionRegistro,CodigoCompania=r.CodigoCompania };
        }
    }

    public sealed class DocumentoPdfException : Exception
    {
        public int Codigo { get; private set; }
        public DocumentoPdfException(int codigo, string message) : base(message) { Codigo = codigo; }
    }
}
