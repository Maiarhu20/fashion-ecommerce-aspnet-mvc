using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Services
{
    public class ServiceResult<T>
    {
        public bool Succeeded { get; set; }
        public T? Data { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }

        public static ServiceResult<T> Success(T data) => new() { Succeeded = true, Data = data };
        public static ServiceResult<T> Failure(string error) => new() { Succeeded = false, ErrorMessage = error };
        public static ServiceResult<T> Failure(string error, Exception ex) => new() { Succeeded = false, ErrorMessage = error, Exception = ex };
    }

    public class ServiceResult
    {
        public bool Succeeded { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }

        public static ServiceResult Success => new() { Succeeded = true };
        public static ServiceResult Failure(string error) => new() { Succeeded = false, ErrorMessage = error };
        public static ServiceResult Failure(string error, Exception ex) => new() { Succeeded = false, ErrorMessage = error, Exception = ex };
    }
}
