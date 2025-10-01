// Simone/Services/BancosConfigService.cs
using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using Simone.Configuration;

namespace Simone.Services
{
    /// <summary>
    /// Servicio de configuración de cuentas bancarias para:
    ///  - Administrador (App_Data/bancos-admin.json)
    ///  - Vendedor (App_Data/bancos-proveedor-{proveedorIdSanitizado}.json)
    /// IO atómico y acceso thread-safe.
    /// </summary>
    public interface IBancosConfigService
    {
        Task<IReadOnlyList<CuentaBancaria>> GetAdminAsync(CancellationToken ct = default);
        Task SetAdminAsync(IEnumerable<CuentaBancaria> cuentas, CancellationToken ct = default);

        Task<IReadOnlyList<CuentaBancaria>> GetByProveedorAsync(string proveedorId, CancellationToken ct = default);
        Task SetByProveedorAsync(string proveedorId, IEnumerable<CuentaBancaria> cuentas, CancellationToken ct = default);

        // (Compat opcional)
        // Task<IReadOnlyList<CuentaBancaria>> GetByProveedorAsync(int proveedorId, CancellationToken ct = default);
        // Task SetByProveedorAsync(int proveedorId, IEnumerable<CuentaBancaria> cuentas, CancellationToken ct = default);
    }

    public class BancosConfigService : IBancosConfigService
    {
        private readonly string _dataFolder;

        private static readonly JsonSerializerOptions _opts = new()
        {
            WriteIndented = true
        };

        // Serializa escrituras entre hilos (async-friendly)
        private static readonly SemaphoreSlim _gate = new(1, 1);

        private const string AdminFileName = "bancos-admin.json";

        public BancosConfigService(IHostEnvironment env)
        {
            _dataFolder = Path.Combine(env.ContentRootPath, "App_Data");
            Directory.CreateDirectory(_dataFolder);
        }

        private string AdminFile() => Path.Combine(_dataFolder, AdminFileName);

        // Genera un nombre de archivo seguro para el proveedorId (string)
        private string ProvFile(string proveedorId)
        {
            var safeId = SanitizeForFileName(RequireProveedorId(proveedorId));
            return Path.Combine(_dataFolder, $"bancos-proveedor-{safeId}.json");
        }

        // ----------------------------
        // API pública (interfaz)
        // ----------------------------
        public async Task<IReadOnlyList<CuentaBancaria>> GetAdminAsync(CancellationToken ct = default)
        {
            var list = await ReadListSafeAsync(AdminFile(), ct).ConfigureAwait(false);
            return list.AsReadOnly();
        }

        public async Task SetAdminAsync(IEnumerable<CuentaBancaria> cuentas, CancellationToken ct = default)
        {
            await WriteListSafeAsync(AdminFile(), cuentas ?? new List<CuentaBancaria>(), ct)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<CuentaBancaria>> GetByProveedorAsync(string proveedorId, CancellationToken ct = default)
        {
            var path = ProvFile(proveedorId);
            var list = await ReadListSafeAsync(path, ct).ConfigureAwait(false);
            return list.AsReadOnly();
        }

        public async Task SetByProveedorAsync(string proveedorId, IEnumerable<CuentaBancaria> cuentas, CancellationToken ct = default)
        {
            var path = ProvFile(proveedorId);
            await WriteListSafeAsync(path, cuentas ?? new List<CuentaBancaria>(), ct)
                .ConfigureAwait(false);
        }

        // ----------------------------
        // IO helpers (async + atómico)
        // ----------------------------
        private static async Task<List<CuentaBancaria>> ReadListSafeAsync(string path, CancellationToken ct)
        {
            try
            {
                // Recuperación simple si quedó .tmp tras un crash
                var tmp = path + ".tmp";
                if (!File.Exists(path) && File.Exists(tmp))
                {
                    try { File.Move(tmp, path, overwrite: true); } catch { /* best-effort */ }
                }

                if (!File.Exists(path)) return new();

                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                var list = await JsonSerializer.DeserializeAsync<List<CuentaBancaria>>(fs, _opts, ct).ConfigureAwait(false);
                return list ?? new();
            }
            catch
            {
                // Fail-safe ante corrupción: lista vacía
                return new();
            }
        }

        private static async Task WriteListSafeAsync(string path, IEnumerable<CuentaBancaria> list, CancellationToken ct)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var dir = Path.GetDirectoryName(path)!;
                Directory.CreateDirectory(dir);

                var tmp = path + ".tmp";
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best-effort */ }

                var json = JsonSerializer.Serialize(list, _opts);
                var bytes = Encoding.UTF8.GetBytes(json);

                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    await fs.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
                    await fs.FlushAsync(ct).ConfigureAwait(false);
                }

                File.Move(tmp, path, overwrite: true);
            }
            finally
            {
                _gate.Release();
            }
        }

        // ----------------------------
        // Validación y utilidades
        // ----------------------------
        private static string RequireProveedorId(string proveedorId)
        {
            if (string.IsNullOrWhiteSpace(proveedorId))
                throw new ArgumentException("El proveedorId no puede ser nulo o vacío.", nameof(proveedorId));
            return proveedorId.Trim();
        }

        /// <summary>
        /// Limpia proveedorId para que sea seguro como parte del nombre de archivo.
        /// Si quedan caracteres fuera de [A-Za-z0-9_-], se reemplazan por '-'.
        /// Si el resultado es muy largo o queda vacío, se usa un hash estable.
        /// </summary>
        private static string SanitizeForFileName(string raw)
        {
            var s = (raw ?? string.Empty).Trim();

            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (char.IsLetterOrDigit(ch) || ch is '-' or '_')
                    sb.Append(ch);
                else
                    sb.Append('-');
            }

            var safe = sb.ToString().Trim('-');
            if (safe.Length == 0 || safe.Length > 80) return HashForFileName(s);
            return safe;
        }

        private static string HashForFileName(string input)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
