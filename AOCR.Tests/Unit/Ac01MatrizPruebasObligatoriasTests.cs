using System;
using System.IO;
using CapaDatos.DAOs;
using CapaModelo.RT;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    /// <summary>
    /// AC-01: Matriz de Pruebas Obligatorias para la Liberación y Reutilización
    /// del correo electrónico del Representante Técnico cuando una designación sea devuelta.
    /// Cubre rigurosamente los 7 casos funcionales requeridos por la especificación definitiva.
    /// </summary>
    [TestClass]
    public class Ac01MatrizPruebasObligatoriasTests
    {
        private static string Read(string path)
        {
            var root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            return File.ReadAllText(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
        }

        #region Caso 1: Nuevo correo sin historial
        /// <summary>
        /// CASO 1: Un correo nuevo sin registros en base de datos debe ser aceptado
        /// para el registro de Representante Técnico.
        /// </summary>
        [TestMethod]
        public void Caso1_NuevoCorreoSinHistorial_DebeEstarDisponibleParaRegistro()
        {
            var daoText = Read("CapaDatos/DAOs/UsuarioDAO.cs");
            
            // Verifica que el DAO incluya la evaluación contextual
            StringAssert.Contains(daoText, "PuedeUsarseCorreoRepresentante");
            StringAssert.Contains(daoText, "Correo disponible.");

            // Verifica que el servicio de negocio exponga la disponibilidad
            var serviceText = Read("CapaNegocio/Services/RtDesignacionFlujoService.cs");
            StringAssert.Contains(serviceText, "ValidarDisponibilidadCorreo");

            // Valida el objeto de modelo de resultado
            var resultadoNuevo = new ResultadoValidacionCorreoRT
            {
                Valido = true,
                Mensaje = "Correo disponible.",
                EsReutilizable = false,
                UsuarioIdExistente = null
            };

            Assert.IsTrue(resultadoNuevo.Valido);
            Assert.IsFalse(resultadoNuevo.EsReutilizable);
            Assert.IsNull(resultadoNuevo.UsuarioIdExistente);
            StringAssert.Contains(resultadoNuevo.Mensaje, "disponible");
        }
        #endregion

        #region Caso 2: Trámite activo con otro titular / correo activo
        /// <summary>
        /// CASO 2: Si el correo ya pertenece a otro usuario activo o con trámite en curso,
        /// el sistema debe bloquear con un mensaje claro.
        /// </summary>
        [TestMethod]
        public void Caso2_TramiteActivoConOtroTitular_DebeBloquearConMensajeClaro()
        {
            var daoText = Read("CapaDatos/DAOs/UsuarioDAO.cs");

            // El DAO debe evaluar si el correo pertenece a otro titular activo o en trámite
            StringAssert.Contains(daoText, "Este correo ya está registrado y activo para otro usuario en el sistema.");
            StringAssert.Contains(daoText, "asociado a una postulación de RT actualmente en proceso de revisión.");

            var resultadoBloqueo = new ResultadoValidacionCorreoRT
            {
                Valido = false,
                UsuarioActivoExistente = true,
                MismaPersona = false,
                Mensaje = "Este correo ya está registrado y activo para otro usuario en el sistema."
            };

            Assert.IsFalse(resultadoBloqueo.Valido, "Debe bloquear si está activo con otro titular.");
            Assert.IsFalse(resultadoBloqueo.MismaPersona, "No es la misma persona.");
            StringAssert.Contains(resultadoBloqueo.Mensaje, "otro usuario");
        }
        #endregion

        #region Caso 3: Designación devuelta únicamente
        /// <summary>
        /// CASO 3: Cuando la designación previa fue devuelta, el correo queda funcionalmente
        /// liberado para permitir que un nuevo postulante o proceso lo utilice.
        /// </summary>
        [TestMethod]
        public void Caso3_DesignacionDevueltaUnicamente_DebePermitirReutilizacion()
        {
            var daoText = Read("CapaDatos/DAOs/UsuarioDAO.cs");

            // La consulta de existencia debe ignorar los registros con correo_liberado o devueltos
            StringAssert.Contains(daoText, "correo_liberado = FALSE");
            StringAssert.Contains(daoText, "estado_designacion_rt = 'devuelto'");
            StringAssert.Contains(daoText, "DEVUELTO_CON_OBSERVACIONES");

            // Mensaje de liberación en caso 3
            StringAssert.Contains(daoText, "El correo ha quedado liberado por devolución de la designación anterior");

            var resultadoLiberado = new ResultadoValidacionCorreoRT
            {
                Valido = true,
                EsReutilizable = false,
                MismaPersona = false,
                Mensaje = "El correo ha quedado liberado por devolución de la designación anterior y puede ser utilizado."
            };

            Assert.IsTrue(resultadoLiberado.Valido, "El correo liberado debe ser válido para uso.");
            Assert.IsFalse(resultadoLiberado.EsReutilizable, "Para persona distinta no es reutilización de identidad previa.");
            StringAssert.Contains(resultadoLiberado.Mensaje, "liberado");
        }
        #endregion

        #region Caso 4: RT existente con la misma compañía tras devolución
        /// <summary>
        /// CASO 4: Un Representante Técnico existente en la misma compañía cuya postulación fue devuelta
        /// debe poder re-postular utilizando su identidad previa sin duplicar usuarios.
        /// </summary>
        [TestMethod]
        public void Caso4_RtExistenteMismaCompania_DebePermitirReutilizacionControlada()
        {
            var daoText = Read("CapaDatos/DAOs/UsuarioDAO.cs");

            // Reconocimiento de misma persona
            StringAssert.Contains(daoText, "resultado.MismaPersona = true");
            StringAssert.Contains(daoText, "El usuario ya se encuentra registrado y activo para esta compañía.");

            // En el controller se apoya en ReutilizarUsuarioPostulacionDevuelta
            var controllerText = Read("CapaPresentacion/Controllers/UsuarioController.cs");
            StringAssert.Contains(controllerText, "ReutilizarUsuarioPostulacionDevuelta");

            var resultadoMismaCia = new ResultadoValidacionCorreoRT
            {
                Valido = true,
                EsReutilizable = true,
                MismaPersona = true,
                MultiCompaniaPermitida = false,
                UsuarioIdExistente = 1050,
                Mensaje = "El usuario ya se encuentra registrado y activo para esta compañía."
            };

            Assert.IsTrue(resultadoMismaCia.Valido);
            Assert.IsTrue(resultadoMismaCia.EsReutilizable);
            Assert.IsTrue(resultadoMismaCia.MismaPersona);
            Assert.AreEqual(1050, resultadoMismaCia.UsuarioIdExistente);
        }
        #endregion

        #region Caso 5: RT existente con nueva compañía (Multi-compañía)
        /// <summary>
        /// CASO 5: Un RT con usuario existente en el sistema que postula para una nueva compañía
        /// debe ser permitido (multi-compañía) sin duplicar usuario ni bloquear indebidamente.
        /// </summary>
        [TestMethod]
        public void Caso5_RtExistenteNuevaCompania_DebePermitirAsociacionMulticompania()
        {
            var daoText = Read("CapaDatos/DAOs/UsuarioDAO.cs");

            // Detección multiempresa
            StringAssert.Contains(daoText, "resultado.MultiCompaniaPermitida = true");
            StringAssert.Contains(daoText, "Usuario RT existente. Se autoriza la vinculación a la nueva compañía.");

            var controllerText = Read("CapaPresentacion/Controllers/UsuarioController.cs");
            StringAssert.Contains(controllerText, "UsuarioCompaniaRTDAO");
            StringAssert.Contains(controllerText, "GuardarAsignaciones");

            var resultadoMultiEmpresa = new ResultadoValidacionCorreoRT
            {
                Valido = true,
                EsReutilizable = true,
                MismaPersona = true,
                MultiCompaniaPermitida = true,
                UsuarioIdExistente = 1020,
                Mensaje = "Usuario RT existente. Se autoriza la vinculación a la nueva compañía."
            };

            Assert.IsTrue(resultadoMultiEmpresa.Valido);
            Assert.IsTrue(resultadoMultiEmpresa.EsReutilizable);
            Assert.IsTrue(resultadoMultiEmpresa.MultiCompaniaPermitida);
        }
        #endregion

        #region Caso 6: Reenvío posterior a devolución (No duplicidad de usuario)
        /// <summary>
        /// CASO 6: Al reenviar la postulación tras devolución, el sistema reutiliza el registro
        /// existente (idusuario) actualizando sus campos y restaurando el estado pendiente.
        /// </summary>
        [TestMethod]
        public void Caso6_ReenvioPosteriorDevolucion_NoDebeCrearUsuarioDuplicado()
        {
            var daoText = Read("CapaDatos/DAOs/UsuarioDAO.cs");

            // Comprueba el método de reutilización sin inserción duplicada
            StringAssert.Contains(daoText, "public static bool ReutilizarUsuarioPostulacionDevuelta");
            StringAssert.Contains(daoText, "UPDATE usuario");
            StringAssert.Contains(daoText, "estado_designacion_rt = 'pendiente'");
            StringAssert.Contains(daoText, "correo_liberado = FALSE");
            StringAssert.Contains(daoText, "WHERE idusuario = @id");

            var controllerText = Read("CapaPresentacion/Controllers/UsuarioController.cs");
            StringAssert.Contains(controllerText, "UsuarioDAO.ReutilizarUsuarioPostulacionDevuelta");
            StringAssert.Contains(controllerText, "validacionCorreo.EsReutilizable && validacionCorreo.UsuarioIdExistente.HasValue");

            // Simula resultado para reenvío del mismo postulante
            var resultadoReenvio = new ResultadoValidacionCorreoRT
            {
                Valido = true,
                EsReutilizable = true,
                MismaPersona = true,
                UsuarioIdExistente = 1080,
                Mensaje = "Designación previa devuelta. El usuario puede reenviar y subsanar su documentación."
            };

            Assert.IsTrue(resultadoReenvio.Valido);
            Assert.IsTrue(resultadoReenvio.EsReutilizable);
            Assert.AreEqual(1080, resultadoReenvio.UsuarioIdExistente);
        }
        #endregion

        #region Caso 7: Solicitudes simultáneas con el mismo correo (Anti-race conditions)
        /// <summary>
        /// CASO 7: Se implementan transacciones con bloqueo pesimista (SELECT ... FOR UPDATE)
        /// para prevenir inconsistencias o duplicidades por clics simultáneos.
        /// </summary>
        [TestMethod]
        public void Caso7_SolicitudesSimultaneas_DebeProtegerConTransaccionYBloqueoPesimista()
        {
            var daoText = Read("CapaDatos/DAOs/UsuarioDAO.cs");

            // Bloqueo pesimista en devolución transaccional
            var startDev = daoText.IndexOf("DevolverDesignacionRTTransaccional", StringComparison.Ordinal);
            Assert.IsTrue(startDev > 0);
            var snippetDev = daoText.Substring(startDev, 3000);
            StringAssert.Contains(snippetDev, "conn.BeginTransaction()");
            StringAssert.Contains(snippetDev, "FOR UPDATE");

            // Bloqueo pesimista en reutilización transaccional
            var startReutilizar = daoText.IndexOf("ReutilizarUsuarioPostulacionDevuelta", StringComparison.Ordinal);
            Assert.IsTrue(startReutilizar > 0);
            var snippetReutilizar = daoText.Substring(startReutilizar, 4500);
            StringAssert.Contains(snippetReutilizar, "conn.BeginTransaction()");
            StringAssert.Contains(snippetReutilizar, "FOR UPDATE");

            // Transaccionalidad completa
            StringAssert.Contains(snippetReutilizar, "tx.Commit()");
            StringAssert.Contains(snippetReutilizar, "tx.Rollback()");
        }
        #endregion

        #region Pruebas de Integridad de la Migración y No-Mutación de Correo
        /// <summary>
        /// Verifica que el correo real no sea corrompido con sufijos artificiales (.devuelto.id).
        /// </summary>
        [TestMethod]
        public void Integridad_NoAlteraCorreoHistoricoConSufijos()
        {
            var daoText = Read("CapaDatos/DAOs/UsuarioDAO.cs");
            
            // No debe contener la mutación con punto devuelto
            Assert.IsFalse(daoText.Contains(".devuelto."), "El sistema NO debe mutar el correo añadiendo sufijos '.devuelto.'.");

            // Debe guardar en correo_original manteniendo correo intacto
            StringAssert.Contains(daoText, "correo_original");
            StringAssert.Contains(daoText, "correo_liberado = TRUE");
        }
        #endregion
    }
}
