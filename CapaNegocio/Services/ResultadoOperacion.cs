namespace CapaNegocio.Services
{
    public class ResultadoOperacion
    {
        public bool Exitoso { get; private set; }
        public string Mensaje { get; private set; }
        public object Datos { get; private set; }

        private ResultadoOperacion(bool exitoso, string mensaje, object datos)
        {
            Exitoso = exitoso;
            Mensaje = mensaje;
            Datos = datos;
        }

        public static ResultadoOperacion Ok(object datos = null, string mensaje = "OK")
            => new ResultadoOperacion(true, mensaje, datos);

        public static ResultadoOperacion Error(string mensaje = "Error")
            => new ResultadoOperacion(false, mensaje, null);
    }
}
