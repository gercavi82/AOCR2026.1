using System;
using System.Reflection;

namespace CapaPresentacion.Helpers
{
    public static class ReflectionMapHelper
    {
        public static void CopyIfExists<TFrom, TTo>(TFrom from, TTo to, params string[] propertyNames)
        {
            if (from == null || to == null || propertyNames == null) return;

            var fromType = typeof(TFrom);
            var toType = typeof(TTo);

            foreach (var name in propertyNames)
            {
                var pFrom = fromType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                var pTo = toType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);

                if (pFrom == null || pTo == null) continue;
                if (!pTo.CanWrite) continue;

                var value = pFrom.GetValue(from, null);

                // Compatibilidad básica de tipos (ej: DateTime? -> DateTime?)
                if (value == null)
                {
                    pTo.SetValue(to, null);
                    continue;
                }

                if (pTo.PropertyType.IsAssignableFrom(pFrom.PropertyType))
                {
                    pTo.SetValue(to, value);
                    continue;
                }

                // Intentar conversión
                try
                {
                    var converted = Convert.ChangeType(value, Nullable.GetUnderlyingType(pTo.PropertyType) ?? pTo.PropertyType);
                    pTo.SetValue(to, converted);
                }
                catch
                {
                    // si no se puede convertir, ignorar
                }
            }
        }
    }
}
