//using System;
//using System.Linq;
//using System.Text;
//using System.Globalization;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Collections.Generic;
//using Microsoft.Extensions.Logging;
//using Simone.Configuration;

//namespace Simone.Services
//{
//    /// <summary>
//    /// Resuelve la tarifa de envío para un vendedor dado un destino (Provincia / Ciudad).
//    /// Prioridad: Ciudad > Provincia. Si no hay regla del vendedor, intenta las de Admin.
//    /// </summary>
//    public class EnviosResolver
//    {
//        private readonly IEnviosConfigService _envios;
//        private readonly ILogger<EnviosResolver> _logger;

//        public EnviosResolver(IEnviosConfigService envios, ILogger<EnviosResolver> logger)
//        {
//            _envios = envios ?? throw new ArgumentNullException(nameof(envios));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        }

//        /// <summary>
//        /// Devuelve la tarifa (USD) para el vendedor y destino indicado, o null si no hay regla.
//        /// </summary>
//        // Services/ILegalDocumentService.cs
//        public interface ILegalDocumentService
//        {
//            Task<string> GetLatestVersionAsync(string documentType);
//            Task<bool> LogAccessAsync(string documentType, string ipAddress, string userAgent);
//            Task<DateTime> GetLastUpdateDateAsync(string documentType);
//        }

//        // Services/LegalDocumentService.cs
//        public class LegalDocumentService : ILegalDocumentService
//        {
//            private readonly ILogger<LegalDocumentService> _logger;

//            public LegalDocumentService(ILogger<LegalDocumentService> logger)
//            {
//                _logger = logger;
//            }

//            public async Task<string> GetLatestVersionAsync(string documentType)
//            {
//                // Lógica para obtener la versión más reciente
//                return await Task.FromResult("1.0.0");
//            }

//            public async Task<bool> LogAccessAsync(string documentType, string ipAddress, string userAgent)
//            {
//                _logger.LogInformation("Acceso a {DocumentType} desde {IpAddress}", documentType, ipAddress);
//                return await Task.FromResult(true);
//            }

//            public async Task<DateTime> GetLastUpdateDateAsync(string documentType)
//            {
//                return await Task.FromResult(DateTime.UtcNow.AddDays(-new Random().Next(1, 30)));
//            }
//        }