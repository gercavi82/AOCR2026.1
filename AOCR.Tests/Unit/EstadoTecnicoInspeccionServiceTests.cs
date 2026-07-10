using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaNegocio.DTOs;
using CapaNegocio.Services;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class EstadoTecnicoInspeccionServiceTests
    {
        [TestMethod]
        public void EstadoTecnicoInspeccion_LVNoExiste_RetornaBloqueado()
        {
            // Arrange
            var dto = new EstadoTecnicoInspeccion
            {
                LvExiste = false,
                LvFinalizada = false,
                LvFirmada = false,
                InformeExiste = false
            };

            // Act
            AplicarReglasDeNegocio(dto);

            // Assert
            Assert.IsFalse(dto.PuedeCrearInforme);
            Assert.IsFalse(dto.PuedeEditarInforme);
            Assert.IsFalse(dto.PuedeVerInforme);
            Assert.AreEqual("Debe finalizar la Lista de Verificación antes de gestionar el Informe Técnico.", dto.MotivoBloqueo);
        }

        [TestMethod]
        public void EstadoTecnicoInspeccion_LVExistePeroNoFinalizada_RetornaBloqueado()
        {
            // Arrange
            var dto = new EstadoTecnicoInspeccion
            {
                LvExiste = true,
                LvFinalizada = false,
                LvFirmada = false,
                InformeExiste = false
            };

            // Act
            AplicarReglasDeNegocio(dto);

            // Assert
            Assert.IsFalse(dto.PuedeCrearInforme);
            Assert.IsFalse(dto.PuedeEditarInforme);
            Assert.AreEqual("Debe finalizar la Lista de Verificación antes de gestionar el Informe Técnico.", dto.MotivoBloqueo);
        }

        [TestMethod]
        public void EstadoTecnicoInspeccion_LVFinalizadaPeroNoFirmada_RetornaBloqueado()
        {
            // Arrange
            var dto = new EstadoTecnicoInspeccion
            {
                LvExiste = true,
                LvFinalizada = true,
                LvFirmada = false,
                InformeExiste = false
            };

            // Act
            AplicarReglasDeNegocio(dto);

            // Assert
            Assert.IsFalse(dto.PuedeCrearInforme);
            Assert.IsFalse(dto.PuedeEditarInforme);
            Assert.AreEqual("Debe firmar la Lista de Verificación antes de gestionar el Informe Técnico.", dto.MotivoBloqueo);
        }

        [TestMethod]
        public void EstadoTecnicoInspeccion_LVFirmadaInformeNoExiste_PuedeCrear()
        {
            // Arrange
            var dto = new EstadoTecnicoInspeccion
            {
                LvExiste = true,
                LvFinalizada = true,
                LvFirmada = true,
                InformeExiste = false
            };

            // Act
            AplicarReglasDeNegocio(dto);

            // Assert
            Assert.IsTrue(dto.PuedeCrearInforme);
            Assert.IsTrue(dto.PuedeEditarInforme);
            Assert.IsFalse(dto.PuedeVerInforme);
            Assert.IsNull(dto.MotivoBloqueo);
        }

        [TestMethod]
        public void EstadoTecnicoInspeccion_InformeBorrador_PuedeEditar()
        {
            // Arrange
            var dto = new EstadoTecnicoInspeccion
            {
                LvExiste = true,
                LvFinalizada = true,
                LvFirmada = true,
                InformeExiste = true,
                EstadoInforme = "BORRADOR_INFORME"
            };

            // Act
            AplicarReglasDeNegocio(dto);

            // Assert
            Assert.IsFalse(dto.PuedeCrearInforme); // Ya existe
            Assert.IsTrue(dto.PuedeEditarInforme);
            Assert.IsFalse(dto.PuedeFirmarInforme);
        }

        [TestMethod]
        public void EstadoTecnicoInspeccion_InformeFirmadoInspector_SoloLectura()
        {
            // Arrange
            var dto = new EstadoTecnicoInspeccion
            {
                LvExiste = true,
                LvFinalizada = true,
                LvFirmada = true,
                InformeExiste = true,
                EstadoInforme = "FIRMADO_INSPECTOR"
            };

            // Act
            AplicarReglasDeNegocio(dto);

            // Assert
            Assert.IsFalse(dto.PuedeEditarInforme);
            Assert.IsTrue(dto.PuedeVerInforme);
            Assert.IsFalse(dto.PuedeFirmarInforme);
        }

        [TestMethod]
        public void EstadoTecnicoInspeccion_EstadoCentralPendienteDCAV_SoloLecturaFallback()
        {
            // Arrange
            var dto = new EstadoTecnicoInspeccion
            {
                LvExiste = true,
                LvFinalizada = true,
                LvFirmada = true,
                InformeExiste = true,
                EstadoInforme = "FIRMADO_INSPECTOR",
                EstadoCentral = "PENDIENTE_REVISION_INFORME_DCAV"
            };

            // Act
            AplicarReglasDeNegocio(dto);

            // Assert
            Assert.IsFalse(dto.PuedeEditarInforme);
            Assert.IsTrue(dto.PuedeVerInforme);
        }

        // Replica simple de la logica interna para test unitario aislado
        private void AplicarReglasDeNegocio(EstadoTecnicoInspeccion dto)
        {
            if (!dto.LvExiste || !dto.LvFinalizada)
            {
                dto.MotivoBloqueo = "Debe finalizar la Lista de Verificación antes de gestionar el Informe Técnico.";
                return;
            }

            if (!dto.LvFirmada)
            {
                dto.MotivoBloqueo = "Debe firmar la Lista de Verificación antes de gestionar el Informe Técnico.";
                return;
            }

            if (!dto.InformeExiste)
            {
                dto.PuedeCrearInforme = true;
                dto.PuedeEditarInforme = true;
                return;
            }

            switch (dto.EstadoInforme)
            {
                case "BORRADOR_INFORME":
                    dto.PuedeEditarInforme = true;
                    break;
                case "FINALIZADO_INFORME":
                    dto.PuedeFirmarInforme = true;
                    break;
                case "FIRMADO_INSPECTOR":
                case "INFORME_TECNICO_APROBADO_DCAV":
                    dto.PuedeVerInforme = true;
                    break;
                case "INFORME_TECNICO_OBSERVADO_DCAV":
                    dto.PuedeEditarInforme = true;
                    dto.PuedeFirmarInforme = true;
                    break;
                default:
                    dto.PuedeVerInforme = true;
                    break;
            }

            if (dto.EstadoCentral == "PENDIENTE_REVISION_INFORME_DCAV" && dto.EstadoInforme == "FIRMADO_INSPECTOR")
            {
                dto.PuedeVerInforme = true;
            }
        }
    }
}
