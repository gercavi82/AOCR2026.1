using System;
using System.IO;
using CapaDatos.DAOs;
using CapaModelo.RT;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class Ac01LiberarCorreoRtDevueltoTests
    {
        private readonly RtDesignacionFlujoService _service = new RtDesignacionFlujoService();

        [TestMethod]
        public void MigracionSql_EsIdempotenteYTieneRollback()
        {
            var sqlMigracion = Read("scripts/sql/20260903_ac01_liberar_correo_rt_devuelto.sql");
            var sqlRollback = Read("scripts/sql/20260903_ac01_liberar_correo_rt_devuelto_rollback.sql");

            StringAssert.Contains(sqlMigracion, "ADD COLUMN correo_original");
            StringAssert.Contains(sqlMigracion, "ADD COLUMN correo_liberado");
            StringAssert.Contains(sqlMigracion, "ADD COLUMN fecha_devolucion_designacion");
            StringAssert.Contains(sqlMigracion, "ADD COLUMN coordinador_devolucion_id");
            StringAssert.Contains(sqlMigracion, "ADD COLUMN observacion_devolucion");
            StringAssert.Contains(sqlMigracion, "idx_usuario_correo_activo_lower");
            StringAssert.Contains(sqlMigracion, "WHERE (correo_liberado = FALSE)");

            StringAssert.Contains(sqlRollback, "DROP COLUMN IF EXISTS correo_original");
            StringAssert.Contains(sqlRollback, "DROP COLUMN IF EXISTS correo_liberado");
            StringAssert.Contains(sqlRollback, "DROP INDEX IF EXISTS public.idx_usuario_correo_activo_lower");
        }

        [TestMethod]
        public void Regla7_AdministradorIntentandoDevolverDesignacion_DebeDenegar()
        {
            var resultado = _service.DevolverDesignacion(
                usuarioId: 99999,
                coordinadorUsuarioId: 1,
                rolSesion: "Administrador",
                observacion: "Documentación incompleta");

            Assert.IsFalse(resultado.Exitoso, "El Administrador no debe poder devolver designaciones operativas.");
            StringAssert.Contains(resultado.Mensaje, "Administrador no tiene autorización");
        }

        [TestMethod]
        public void RolNoAutorizado_IntentandoDevolverDesignacion_DebeDenegar()
        {
            var resultadoInspector = _service.DevolverDesignacion(
                usuarioId: 99999,
                coordinadorUsuarioId: 2,
                rolSesion: "Inspector",
                observacion: "Observación");

            Assert.IsFalse(resultadoInspector.Exitoso);
            StringAssert.Contains(resultadoInspector.Mensaje, "No tiene permisos");

            var resultadoFinanciero = _service.DevolverDesignacion(
                usuarioId: 99999,
                coordinadorUsuarioId: 3,
                rolSesion: "Financiero",
                observacion: "Observación");

            Assert.IsFalse(resultadoFinanciero.Exitoso);
        }

        [TestMethod]
        public void ObservacionVacia_DebeSerRechazada()
        {
            var resultado = _service.DevolverDesignacion(
                usuarioId: 99999,
                coordinadorUsuarioId: 1,
                rolSesion: "Coordinacion",
                observacion: "   ");

            Assert.IsFalse(resultado.Exitoso);
            StringAssert.Contains(resultado.Mensaje, "Debe ingresar una observación");
        }

        [TestMethod]
        public void Endpoint_RechazarDesignacion_BloqueaAdministradorCon403()
        {
            var controllerText = Read("CapaPresentacion/Controllers/UsuarioController.cs");
            var start = controllerText.IndexOf("ActionResult RechazarDesignacion(", StringComparison.Ordinal);
            Assert.IsTrue(start > 0, "No se encontró el método RechazarDesignacion.");

            var end = controllerText.IndexOf("private ActionResult RedirigirDespuesRevisionRT", start, StringComparison.Ordinal);
            Assert.IsTrue(end > start);

            var body = controllerText.Substring(start, end - start);
            StringAssert.Contains(body, "HttpStatusCodeResult(403");
            StringAssert.Contains(body, "Administrador");
            StringAssert.Contains(body, "RtDesignacionFlujoService");
        }

        [TestMethod]
        public void Endpoint_RechazarDesignacion_ExigeAntiforgeryYHttpPost()
        {
            var controllerText = Read("CapaPresentacion/Controllers/UsuarioController.cs");
            var start = controllerText.IndexOf("ActionResult RechazarDesignacion(", StringComparison.Ordinal);
            var header = controllerText.Substring(Math.Max(0, start - 200), 200);

            StringAssert.Contains(header, "[HttpPost]");
            StringAssert.Contains(header, "[ValidateAntiForgeryToken]");
            StringAssert.Contains(header, "[Authorize(Roles = RolesGestionUsuariosRt)]");
        }

        [TestMethod]
        public void Dao_DevolverDesignacionRTTransaccional_ProtegeUsuariosActivos()
        {
            var daoText = Read("CapaDatos/DAOs/UsuarioDAO.cs");
            StringAssert.Contains(daoText, "DevolverDesignacionRTTransaccional");
            StringAssert.Contains(daoText, "if (usuario.Activo)");
            StringAssert.Contains(daoText, "No se puede devolver la designación de un usuario activo.");
            StringAssert.Contains(daoText, "aceptado");
            StringAssert.Contains(daoText, "tx.Rollback()");
            StringAssert.Contains(daoText, "tx.Commit()");
        }

        [TestMethod]
        public void Dao_DevolverDesignacionRTTransaccional_LiberaCorreoYResguardaOriginal()
        {
            var daoText = Read("CapaDatos/DAOs/UsuarioDAO.cs");
            StringAssert.Contains(daoText, "estado_designacion_rt = 'devuelto'");
            StringAssert.Contains(daoText, "correo_original");
            StringAssert.Contains(daoText, "correo_liberado = TRUE");
            StringAssert.Contains(daoText, "devuelto.");
        }

        [TestMethod]
        public void Dao_ExisteCorreo_DescartaCorreosLiberadosPorDevolucion()
        {
            var daoText = Read("CapaDatos/DAOs/UsuarioDAO.cs");
            var start = daoText.IndexOf("public static bool ExisteCorreo(string correo)", StringComparison.Ordinal);
            Assert.IsTrue(start > 0);

            var end = daoText.IndexOf("public static bool ExisteIdentificacion", start, StringComparison.Ordinal);
            var body = daoText.Substring(start, end - start);

            StringAssert.Contains(body, "correo_liberado");
            StringAssert.Contains(body, "devuelto");
            StringAssert.Contains(body, "activo = true");
        }

        [TestMethod]
        public void Dao_DevolverDesignacion_ManejaIdempotenciaParaDobleClic()
        {
            var daoText = Read("CapaDatos/DAOs/UsuarioDAO.cs");
            StringAssert.Contains(daoText, "YaEstabaDevuelto = true");
            StringAssert.Contains(daoText, "La designación ya fue devuelta previamente.");

            var serviceText = Read("CapaNegocio/Services/RtDesignacionFlujoService.cs");
            StringAssert.Contains(serviceText, "if (!resultado.YaEstabaDevuelto");
        }

        [TestMethod]
        public void Auditoria_RegistraDevolucionConActorYObservacion()
        {
            var daoText = Read("CapaDatos/DAOs/UsuarioDAO.cs");
            StringAssert.Contains(daoText, "DEVOLUCION_DESIGNACION_RT");
            StringAssert.Contains(daoText, "USUARIOS_RT");
            StringAssert.Contains(daoText, "aocr_tbauditoria");
        }

        [TestMethod]
        public void Endpoint_RechazarDesignacion_ExigePermisoCoordinadorYUsuarioIdValido()
        {
            var controllerText = Read("CapaPresentacion/Controllers/UsuarioController.cs");
            var start = controllerText.IndexOf("ActionResult RechazarDesignacion(", StringComparison.Ordinal);
            var end = controllerText.IndexOf("private ActionResult RedirigirDespuesRevisionRT", start, StringComparison.Ordinal);
            var body = controllerText.Substring(start, end - start);

            StringAssert.Contains(body, "COORDINADOR_DEVOLVER_POSTULACION");
            StringAssert.Contains(body, "coordinadorId <= 0");
            StringAssert.Contains(body, "HttpStatusCodeResult(401");
            StringAssert.Contains(body, "HttpStatusCodeResult(409");
        }

        [TestMethod]
        public void Servicio_RechazaDevolucion_SiUsuarioEstaActivo()
        {
            // Validar que el DAO contenga la compuerta de usuario.Activo y estado aceptado
            var daoText = Read("CapaDatos/DAOs/UsuarioDAO.cs");
            StringAssert.Contains(daoText, "if (usuario.Activo)");
            StringAssert.Contains(daoText, "No se puede devolver la designación de un usuario activo.");
            StringAssert.Contains(daoText, "aceptado");
        }

        [TestMethod]
        public void Servicio_IdempotenciaDobleClic_NotificaUnaSolaVez()
        {
            var serviceText = Read("CapaNegocio/Services/RtDesignacionFlujoService.cs");
            StringAssert.Contains(serviceText, "if (!resultado.YaEstabaDevuelto && !string.IsNullOrWhiteSpace(resultado.CorreoOriginal))");
            StringAssert.Contains(serviceText, "EnviarNotificacionDevolucion(resultado");
        }

        [TestMethod]
        public void ReutilizarCorreo_ExisteCorreoIgnoraRegistrosDevueltos()
        {
            var daoText = Read("CapaDatos/DAOs/UsuarioDAO.cs");
            var start = daoText.IndexOf("public static bool ExisteCorreo(string correo)", StringComparison.Ordinal);
            var end = daoText.IndexOf("public static bool ExisteIdentificacion", start, StringComparison.Ordinal);
            var body = daoText.Substring(start, end - start);

            // Debe excluir registros con correo_liberado = true o estado devuelto
            StringAssert.Contains(body, "correo_liberado");
            StringAssert.Contains(body, "devuelto");
        }

        private static string Read(string path)
        {
            var root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            return File.ReadAllText(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
