using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Shared
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
        public List<string> Errors { get; set; }

        public static ApiResponse<T> Ok(T data, string message = "Success")
            => new ApiResponse<T> { Success = true, Message = message, Data = data };

        public static ApiResponse<T> Fail(string message, List<string> errors = null)
            => new ApiResponse<T> { Success = false, Message = message, Errors = errors };
    }

    public class ApiResponse : ApiResponse<object>
    {
        public static ApiResponse Ok(string message = "Success")
            => new ApiResponse { Success = true, Message = message };
    }
}
