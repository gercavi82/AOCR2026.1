namespace CapaModelo.Common
{
    public class ResultadoOperacion
    {
        public bool Ok { get; set; }
        public string Mensaje { get; set; }

        public static ResultadoOperacion Success(string msg = null) => new ResultadoOperacion { Ok = true, Mensaje = msg };
        public static ResultadoOperacion Fail(string msg) => new ResultadoOperacion { Ok = false, Mensaje = msg };
    }
}
