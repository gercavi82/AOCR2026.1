using System;
using System.Collections.Generic;
using System.IO;
using CapaDatos.Services;
using CapaModelo;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class RegistroRtNotificacionServiceTests
    {
        [TestMethod]
        public void CoordinadorConVariosRoles_RecibeUnAvisoConEnlaceYCorreoInstitucional()
        {
            var f = new Fixture();
            f.Correos = () => new[] { "coordinacion@example.invalid", "COORDINACION@example.invalid" };
            f.Enviar();
            Assert.AreEqual(1, f.Avisos.Count);
            Assert.AreEqual(12, f.Avisos[0].CodigoUsuario);
            Assert.AreEqual(99, f.Avisos[0].EntidadId);
            Assert.AreEqual("/aocr/Usuario/RevisarDesignaciones", f.Avisos[0].Url);
            Assert.IsFalse(f.Avisos[0].Leida);
            Assert.AreEqual(1, f.Cola.Count);
            Assert.AreEqual("coordinacion@example.invalid", f.Cola[0].Para);
        }

        [TestMethod]
        public void FallaConfiguracionInstitucional_UsaCorreoDelCoordinadorYConservaAviso()
        {
            var f = new Fixture();
            f.Correos = () => { throw new InvalidOperationException("configuracion no disponible"); };
            f.Enviar();
            Assert.AreEqual(1, f.Avisos.Count);
            Assert.AreEqual("coordinador@example.invalid", f.Cola[0].Para);
        }

        [TestMethod]
        public void FallaColaCorreo_NoImpideNotificacionInterna()
        {
            var f = new Fixture { FallarCola = true };
            f.Enviar();
            Assert.AreEqual(1, f.Avisos.Count);
        }

        [TestMethod]
        public void FallaAvisoInterno_NoImpideEncolarCorreo()
        {
            var f = new Fixture { FallarAviso = true };
            f.Enviar();
            Assert.AreEqual(1, f.Cola.Count);
        }

        [TestMethod]
        public void CoordinadorSinCorreo_RecibeAvisoInterno()
        {
            var f = new Fixture();
            f.Coordinador.Email = null;
            f.Enviar();
            Assert.AreEqual(1, f.Avisos.Count);
            Assert.AreEqual(0, f.Cola.Count);
        }

        [TestMethod]
        public void NombreRt_SeCodificaComoTextoEnCorreo()
        {
            var f = new Fixture();
            f.Enviar("<script>prueba</script>");
            StringAssert.Contains(f.Cola[0].Cuerpo, "&lt;script&gt;");
            Assert.IsFalse(f.Cola[0].Cuerpo.Contains("<script>"));
            Assert.AreEqual("RT_REGISTRO_PENDIENTE", f.Cola[0].TipoNotificacion);
        }

        private sealed class Fixture
        {
            public readonly Usuario Coordinador = new Usuario { Id = 12, Email = "coordinador@example.invalid" };
            public readonly List<Notificacion> Avisos = new List<Notificacion>();
            public readonly List<EmailQueueItem> Cola = new List<EmailQueueItem>();
            public Func<IEnumerable<string>> Correos = () => new string[0];
            public bool FallarCola;
            public bool FallarAviso;

            public void Enviar(string nombre = "RT de prueba")
            {
                var servicio = new RegistroRtNotificacionService(
                    rol => new List<Usuario> { Coordinador }, () => Correos(),
                    aviso =>
                    {
                        if (FallarAviso) throw new InvalidOperationException("aviso no disponible");
                        Avisos.Add(aviso);
                        return true;
                    },
                    correo =>
                    {
                        if (FallarCola) throw new InvalidOperationException("cola no disponible");
                        Cola.Add(correo);
                    }, new CapaNegocio.Services.LoggingService(Path.Combine(Path.GetTempPath(), "aocr-rt-notificaciones-tests")));
                servicio.NotificarRegistroPendiente(99, nombre, "/aocr/Usuario/RevisarDesignaciones");
            }
        }
    }
}
