using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;

namespace CapaPresentacion.Helpers
{
    public static class OrdenRecaudacionPagoHelper
    {
        private const string IntroHtmlKey = "OrdenRecaudacion:PagoIntroHtml";
        private const string FirmaHtmlKey = "OrdenRecaudacion:PagoFirmaHtml";
        private const string CuentasJsonKey = "OrdenRecaudacion:CuentasBancariasJson";

        private const string IntroHtmlPorDefecto = "Para los servicios <strong>AEROPORTUARIOS y/o AERONAUTICOS</strong>, use las siguientes cuentas. <strong>Realice el pago con 72 horas de anticipacion.</strong>";
        private const string FirmaHtmlPorDefecto = "<strong>DIRECCION FINANCIERA - DGAC</strong>";

        public sealed class CuentaBancariaInfo
        {
            public string Banco { get; set; }
            public string CuentaCorriente { get; set; }
            public string Sublinea { get; set; }
            public string Titular { get; set; }
            public string Ruc { get; set; }
            public string NotaTransferencia { get; set; }
            public string SitioWeb { get; set; }
            public string LogoUrl { get; set; }
            public string LogoMaxWidth { get; set; }
        }

        public static string ObtenerIntroHtml()
        {
            return ObtenerValorConfigurado(IntroHtmlKey, IntroHtmlPorDefecto);
        }

        public static string ObtenerFirmaHtml()
        {
            return ObtenerValorConfigurado(FirmaHtmlKey, FirmaHtmlPorDefecto);
        }

        public static IList<CuentaBancariaInfo> ObtenerCuentasBancarias()
        {
            var cuentasConfiguradas = ObtenerCuentasConfiguradas();
            return cuentasConfiguradas.Any() ? cuentasConfiguradas : ObtenerCuentasPorDefecto();
        }

        public static string ConstruirLeyendaHtml()
        {
            var cuentas = ObtenerCuentasBancarias();
            var builder = new StringBuilder();

            builder.Append("<p>");
            builder.Append(ObtenerIntroHtml());
            builder.Append("</p>");

            foreach (var cuenta in cuentas)
            {
                builder.Append("<p>");
                builder.Append("<b>");
                builder.Append(HttpUtility.HtmlEncode(cuenta.Banco ?? string.Empty));
                builder.Append("</b><br>");
                AppendLinea(builder, "Cuenta Corriente", cuenta.CuentaCorriente);
                AppendLinea(builder, "Sublinea", cuenta.Sublinea);
                AppendLinea(builder, "Titular", cuenta.Titular);
                AppendLinea(builder, "RUC", cuenta.Ruc);

                if (!string.IsNullOrWhiteSpace(cuenta.NotaTransferencia))
                {
                    builder.Append(HttpUtility.HtmlEncode(cuenta.NotaTransferencia));
                    builder.Append("<br>");
                }

                builder.Append("</p>");
            }

            return builder.ToString();
        }

        private static void AppendLinea(StringBuilder builder, string etiqueta, string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return;
            }

            builder.Append(HttpUtility.HtmlEncode(etiqueta));
            builder.Append(": ");
            builder.Append(HttpUtility.HtmlEncode(valor));
            builder.Append("<br>");
        }

        private static string ObtenerValorConfigurado(string key, string valorPorDefecto)
        {
            try
            {
                var valorConfigurado = ConfigurationManager.AppSettings[key];
                if (!string.IsNullOrWhiteSpace(valorConfigurado))
                {
                    return valorConfigurado.Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("OrdenRecaudacionPagoHelper: error leyendo appSetting '" + key + "': " + ex.Message);
            }

            return valorPorDefecto;
        }

        private static IList<CuentaBancariaInfo> ObtenerCuentasConfiguradas()
        {
            try
            {
                var raw = ConfigurationManager.AppSettings[CuentasJsonKey];
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return new List<CuentaBancariaInfo>();
                }

                var serializer = new JavaScriptSerializer();
                var cuentas = serializer.Deserialize<List<CuentaBancariaInfo>>(raw) ?? new List<CuentaBancariaInfo>();
                return cuentas.Where(EsCuentaValida).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("OrdenRecaudacionPagoHelper: error leyendo cuentas configuradas: " + ex.Message);
                return new List<CuentaBancariaInfo>();
            }
        }

        private static bool EsCuentaValida(CuentaBancariaInfo cuenta)
        {
            return cuenta != null
                && !string.IsNullOrWhiteSpace(cuenta.Banco)
                && !string.IsNullOrWhiteSpace(cuenta.CuentaCorriente);
        }

        private static List<CuentaBancariaInfo> ObtenerCuentasPorDefecto()
        {
            return new List<CuentaBancariaInfo>
            {
                new CuentaBancariaInfo
                {
                    Banco = "Banco Pichincha",
                    CuentaCorriente = "2100310688",
                    Sublinea = "30200 (en depositos)",
                    Titular = "Direccion General de Aviacion Civil",
                    Ruc = "1768014410001",
                    NotaTransferencia = "En transferencias NO colocar sublinea",
                    SitioWeb = "https://www.pichincha.com/",
                    LogoUrl = "~/Content/imganes/bancopichincha.png",
                    LogoMaxWidth = "120px"
                },
                new CuentaBancariaInfo
                {
                    Banco = "Banco Internacional",
                    CuentaCorriente = "520608140",
                    Sublinea = "30200 (en depositos)",
                    Titular = "Direccion General de Aviacion Civil",
                    Ruc = "1768014410001",
                    NotaTransferencia = "En transferencias NO colocar sublinea",
                    SitioWeb = "https://www.bancointernacional.com.ec/",
                    LogoUrl = "~/Content/imganes/bancointernacional.png",
                    LogoMaxWidth = "120px"
                },
                new CuentaBancariaInfo
                {
                    Banco = "Banco Ruminahui",
                    CuentaCorriente = "8002531204",
                    Sublinea = "30200 (en depositos)",
                    Titular = "Direccion General de Aviacion Civil",
                    Ruc = "1768014410001",
                    NotaTransferencia = "En transferencias NO colocar sublinea",
                    SitioWeb = "https://www.bgr.com.ec/",
                    LogoUrl = "~/Content/imganes/banco-general-ruminahui.png",
                    LogoMaxWidth = "55px"
                }
            };
        }
    }
}