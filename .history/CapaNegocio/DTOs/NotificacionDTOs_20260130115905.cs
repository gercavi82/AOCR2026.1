using System;

namespace CapaNegocio.DTOs
{
    public class OperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorCode { get; set; }

        public static OperationResult Ok(string message = null) => new OperationResult { Success = true, Message = message };
        public static OperationResult Fail(string message, string errorCode = null) => new OperationResult { Success = false, Message = message, ErrorCode = errorCode };
    }

    public class OperationResult<T> : OperationResult
    {
        public T Data { get; set; }

        public static OperationResult<T> Ok(T data, string message = null) => new OperationResult<T> { Success = true, Data = data, Message = message };
        public static new OperationResult<T> Fail(string message, string errorCode = null) => new OperationResult<T> { Success = false, Message = message, ErrorCode = errorCode };
    }

    public class EnviarNotificacionRequest
    {
        public int OrdenId { get; set; }
        public string TipoNotificacion { get; set; }
        public string EmailDestino { get; set; }
        public string NombreDestino { get; set; }
        public bool AdjuntarPdf { get; set; }
        public byte[] AdjuntoPdf { get; set; }
        public string NombreAdjunto { get; set; }
    }
}
