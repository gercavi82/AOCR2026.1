using System.Collections.Generic;
using System.Linq;

public class ParametroDAO
{
    // ... métodos existentes ...

    /// <summary>
    /// Devuelve valores de test configurables como diccionario
    /// </summary>
    public Dictionary<string, string> ObtenerValoresTest()
    {
        var parametros = ListarTodos();
        var clavesTest = new[]
        {
            "TEST_OPERADOR_DEFECTO",
            "TEST_REPRESENTANTE_DEFECTO",
            "TEST_CEDULA_DEFECTO",
            "TEST_DIRECCION_DEFECTO",
            "TEST_TELEFONO_DEFECTO",
            "TEST_EMAIL_DEFECTO",
            "TEST_RUC_DEFECTO",
            "TEST_RAZON_SOCIAL_DEFECTO",
            "TEST_DESCRIPCION_DEFECTO",
            "TEST_OBSERVACIONES_DEFECTO"
        };

        return parametros
            .Where(p => clavesTest.Contains(p.Clave))
            .ToDictionary(p => p.Clave, p => p.Valor);
    }

    /// <summary>
    /// Devuelve configuración PDF como diccionario
    /// </summary>
    public Dictionary<string, string> ObtenerConfiguracionPDF()
    {
        var parametros = ListarTodos();
        var clavesPdf = new[]
        {
            "PDF_MARGEN_SUPERIOR",
            "PDF_MARGEN_INFERIOR",
            "PDF_MARGEN_IZQUIERDO",
            "PDF_MARGEN_DERECHO",
            "PDF_FUENTE",
            "PDF_TAMANIO_FUENTE"
        };

        return parametros
            .Where(p => clavesPdf.Contains(p.Clave))
            .ToDictionary(p => p.Clave, p => p.Valor);
    }

    /// <summary>
    /// Devuelve montos demo como diccionario
    /// </summary>
    public Dictionary<string, string> ObtenerMontosDemo()
    {
        var parametros = ListarTodos();
        var clavesMontos = new[]
        {
            "MONTO_DEMO_1",
            "MONTO_DEMO_2",
            "MONTO_DEMO_3"
        };

        return parametros
            .Where(p => clavesMontos.Contains(p.Clave))
            .ToDictionary(p => p.Clave, p => p.Valor);
    }
}
