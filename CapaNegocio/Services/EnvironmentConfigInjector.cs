using System;
using System.Configuration;
using System.Reflection;
using System.Collections.Generic;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Componente centralizado transversal (P02).
    /// Lee variables de entorno e inyecta valores seguros en memoria
    /// sobreescribiendo ConfigurationManager. 
    /// Mantiene compatibilidad total con MVC5 y FR3.
    /// </summary>
    public static class EnvironmentConfigInjector
    {
        // Catálogo de variables definido
        private const string V_PG = "AOCR_POSTGRES_CONNECTION";
        private const string V_PG_MIRROR = "AOCR_POSTGRES_MIRROR_CONNECTION";
        private const string V_DB2 = "AOCR_DB2_CONNECTION";

        // Asignaciones directas para ConnectionStrings
        private static readonly Dictionary<string, string> ConnectionStringMap = new Dictionary<string, string>
        {
            { "AOCRConnection", V_PG },
            { "PostgreSQL", V_PG },
            { "PostgresMirror", V_PG_MIRROR },
            { "P9ConnectionString", V_DB2 }
        };

        // Asignaciones para AppSettings
        private static readonly Dictionary<string, string> AppSettingsMap = new Dictionary<string, string>
        {
            { "AS400:Server", "AOCR_AS400_SERVER" },
            { "AS400:Database", "AOCR_AS400_DATABASE" },
            { "AS400:UserId", "AOCR_AS400_USER" },
            { "AS400:Password", "AOCR_AS400_PASSWORD" },
            { "AS400:Library", "AOCR_AS400_LIBRARY" },
            { "RT_FileStorageRoot", "AOCR_STORAGE_ROOT" }
        };

        // Lista de variables sensibles obligatorias
        private static readonly List<string> VariablesObligatorias = new List<string>
        {
            V_PG, V_DB2, "AOCR_AS400_PASSWORD"
        };

        private static bool _isInjected = false;

        public static void Inject()
        {
            if (_isInjected) return;

            ValidarObligatorias();

            InjectConnectionStrings();
            InjectAppSettings();

            _isInjected = true;
        }

        public static void ResetForTesting()
        {
            _isInjected = false;
        }

        private static void ValidarObligatorias()
        {
            var missing = new List<string>();
            foreach (var envVar in VariablesObligatorias)
            {
                var val = Environment.GetEnvironmentVariable(envVar);
                // Si la variable de entorno no existe, comprobamos si ConfigurationManager tiene un valor de fallback "real".
                // En un entorno 100% saneado, ConfigurationManager tendrá placeholders o estará vacío.
                if (string.IsNullOrWhiteSpace(val))
                {
                    missing.Add(envVar);
                }
            }

            // Fallamos de forma controlada SOLO si hay variables faltantes Y no hay configuración en IIS (que validaremos después).
            // Para ser compatibles con IIS Protected Configuration, si la variable de entorno no está,
            // pero el ConnectionString respectivo no contiene marcadores "${", asumimos que IIS lo desencriptó.
        }

        private static void InjectConnectionStrings()
        {
            var settings = ConfigurationManager.ConnectionStrings;
            if (settings == null) return;

            // Desbloquear colección de solo lectura
            var fi = typeof(ConfigurationElementCollection).GetField("bReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi != null) fi.SetValue(settings, false);

            foreach (var map in ConnectionStringMap)
            {
                var envVal = Environment.GetEnvironmentVariable(map.Value);
                var existing = settings[map.Key];

                if (!string.IsNullOrWhiteSpace(envVal))
                {
                    if (existing != null)
                    {
                        // Desbloquear el elemento específico
                        var elementReadOnlyField = typeof(ConfigurationElement).GetField("_bReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (elementReadOnlyField != null)
                        {
                            elementReadOnlyField.SetValue(existing, false);
                            existing.ConnectionString = envVal;
                            elementReadOnlyField.SetValue(existing, true);
                        }
                    }
                    else
                    {
                        settings.Add(new ConnectionStringSettings(map.Key, envVal));
                    }
                }
                else if (VariablesObligatorias.Contains(map.Value))
                {
                    // Validación de fallo controlado si falta en ambiente y tampoco fue inyectado por IIS
                    if (existing == null || string.IsNullOrWhiteSpace(existing.ConnectionString) || existing.ConnectionString.Contains("${"))
                    {
                        throw new ConfigurationErrorsException($"Falta configuración obligatoria. Defina la variable de entorno: {map.Value} o configure IIS Protected Config.");
                    }
                }
            }

            // Volver a bloquear
            if (fi != null) fi.SetValue(settings, true);
        }

        private static void InjectAppSettings()
        {
            var settings = ConfigurationManager.AppSettings;
            if (settings == null) return;

            // Para NameValueCollection puede estar bloqueado o no
            PropertyInfo isReadOnlyProp = typeof(System.Collections.Specialized.NameObjectCollectionBase)
                .GetProperty("IsReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
            
            bool wasReadOnly = false;
            if (isReadOnlyProp != null)
            {
                wasReadOnly = (bool)isReadOnlyProp.GetValue(settings, null);
                if (wasReadOnly) isReadOnlyProp.SetValue(settings, false, null);
            }

            foreach (var map in AppSettingsMap)
            {
                var envVal = Environment.GetEnvironmentVariable(map.Value);
                if (!string.IsNullOrWhiteSpace(envVal))
                {
                    settings.Set(map.Key, envVal);
                }
                else if (VariablesObligatorias.Contains(map.Value))
                {
                    var existing = settings.Get(map.Key);
                    if (string.IsNullOrWhiteSpace(existing) || existing.Contains("${"))
                    {
                        throw new ConfigurationErrorsException($"Falta configuración obligatoria. Defina la variable de entorno: {map.Value}");
                    }
                }
            }

            if (isReadOnlyProp != null && wasReadOnly)
            {
                isReadOnlyProp.SetValue(settings, true, null);
            }
        }
    }
}
