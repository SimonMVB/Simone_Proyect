//// Services/LoginLoggerService.cs
//using Microsoft.AspNetCore.Http;
//using PayPalCheckoutSdk.Orders;
//using Simone.Data; // Asegúrate de que este using apunte a tu DbContext
//using Simone.Models;
//using System.Threading.Tasks;

//namespace Simone.Services
//{
//    public class LoginLoggerService
//    {
//        private readonly ApplicationDbContext _context;
//        private readonly IHttpContextAccessor _httpAccessor;

//        public LoginLoggerService(ApplicationDbContext context, IHttpContextAccessor httpAccessor)
//        {
//            _context = context;
//            _httpAccessor = httpAccessor;
//        }

//        public async Task LogLoginAttemptAsync(string username, bool success)
//        {
//            var log = new LogIniciosSesion
//            {
//                Usuario = username,
//                Exitoso = success,
//                DireccionIP = _httpAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
//                UserAgent = _httpAccessor.HttpContext?.Request?.Headers["User-Agent"]
//            };

//            _context.LogIniciosSesiones.Add(log);
//            await _context.SaveChangesAsync();
//        }
//    }
//}