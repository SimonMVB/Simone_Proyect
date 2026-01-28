using System;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Simone.Extensions
{
    /// <summary>
    /// Extensiones para trabajar con sesiones en ASP.NET Core
    /// Proporciona serialización/deserialización de objetos a JSON
    /// Versión CORREGIDA - Sin recursión infinita
    /// 
    /// ⚠️ IMPORTANTE: NO se definen métodos GetString/SetString porque ISession ya los tiene.
    /// Definirlos con la misma firma causa StackOverflowException por recursión infinita.
    /// </summary>
    public static class SessionExtensions
    {
        #region Constantes

        private const string EXC_SESSION_NULL = "La sesión no puede ser nula";
        private const string EXC_KEY_NULL = "La clave no puede ser nula o vacía";
        private const string EXC_VALUE_NULL = "El valor no puede ser nulo";

        // Opciones de JSON reutilizables (mejor performance - evita crear nuevas instancias)
        private static readonly JsonSerializerOptions _jsonOptionsWrite = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        private static readonly JsonSerializerOptions _jsonOptionsRead = new()
        {
            PropertyNameCaseInsensitive = true
        };

        #endregion

        #region Serialization - JSON

        /// <summary>
        /// Guarda un objeto como JSON en la sesión
        /// </summary>
        /// <typeparam name="T">Tipo del objeto a guardar</typeparam>
        /// <param name="session">Sesión actual</param>
        /// <param name="key">Clave para almacenar el objeto</param>
        /// <param name="value">Objeto a serializar y guardar</param>
        public static void SetObjectAsJson<T>(this ISession session, string key, T value)
        {
            ValidateSession(session);
            ValidateKey(key);
            ValidateValue(value);

            try
            {
                var jsonString = JsonSerializer.Serialize(value, _jsonOptionsWrite);
                session.SetString(key, jsonString); // ✅ Llama al método NATIVO de ISession
            }
            catch (Exception ex) when (ex is not ArgumentNullException)
            {
                throw new InvalidOperationException(
                    $"Error al serializar objeto para la clave '{key}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Recupera un objeto desde la sesión, deserializándolo desde JSON
        /// </summary>
        /// <typeparam name="T">Tipo del objeto a recuperar</typeparam>
        /// <param name="session">Sesión actual</param>
        /// <param name="key">Clave del objeto almacenado</param>
        /// <returns>El objeto deserializado o default(T) si no existe</returns>
        public static T? GetObjectFromJson<T>(this ISession session, string key)
        {
            ValidateSession(session);
            ValidateKey(key);

            // ✅ Llama al método NATIVO de ISession (no hay conflicto de nombres)
            var jsonString = session.GetString(key);

            if (string.IsNullOrWhiteSpace(jsonString))
            {
                return default;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(jsonString, _jsonOptionsRead);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Error al deserializar objeto de la clave '{key}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Intenta recuperar un objeto desde la sesión de forma segura (sin excepciones)
        /// </summary>
        /// <typeparam name="T">Tipo del objeto a recuperar</typeparam>
        /// <param name="session">Sesión actual</param>
        /// <param name="key">Clave del objeto almacenado</param>
        /// <param name="value">Objeto recuperado (out parameter)</param>
        /// <returns>True si se recuperó exitosamente, false en caso contrario</returns>
        public static bool TryGetObjectFromJson<T>(this ISession session, string key, out T? value)
        {
            value = default;

            if (session == null || string.IsNullOrWhiteSpace(key))
                return false;

            try
            {
                var jsonString = session.GetString(key);

                if (string.IsNullOrWhiteSpace(jsonString))
                    return false;

                value = JsonSerializer.Deserialize<T>(jsonString, _jsonOptionsRead);
                return value != null;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Session Management

        /// <summary>
        /// Verifica si existe un valor en la sesión para la clave especificada
        /// </summary>
        /// <param name="session">Sesión actual</param>
        /// <param name="key">Clave a verificar</param>
        /// <returns>True si existe, false en caso contrario</returns>
        public static bool Has(this ISession session, string key)
        {
            if (session == null || string.IsNullOrWhiteSpace(key))
                return false;

            try
            {
                var value = session.GetString(key);
                return !string.IsNullOrWhiteSpace(value);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica si existe una clave en la sesión (alias de Has)
        /// </summary>
        public static bool ContainsKey(this ISession session, string key)
        {
            return Has(session, key);
        }

        /// <summary>
        /// Elimina un valor de la sesión si existe
        /// </summary>
        /// <param name="session">Sesión actual</param>
        /// <param name="key">Clave a eliminar</param>
        /// <returns>True si se eliminó, false si no existía o hubo error</returns>
        public static bool RemoveIfExists(this ISession session, string key)
        {
            if (session == null || string.IsNullOrWhiteSpace(key))
                return false;

            try
            {
                if (Has(session, key))
                {
                    session.Remove(key);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Obtiene un objeto o devuelve un valor por defecto si no existe
        /// </summary>
        /// <typeparam name="T">Tipo del objeto a recuperar</typeparam>
        /// <param name="session">Sesión actual</param>
        /// <param name="key">Clave del objeto almacenado</param>
        /// <param name="defaultValue">Valor por defecto si no existe</param>
        /// <returns>El objeto recuperado o el valor por defecto</returns>
        public static T GetObjectOrDefault<T>(this ISession session, string key, T defaultValue)
        {
            if (session == null || string.IsNullOrWhiteSpace(key))
                return defaultValue;

            try
            {
                if (!Has(session, key))
                    return defaultValue;

                var value = GetObjectFromJson<T>(session, key);
                return value ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Limpia todos los valores de la sesión de forma segura
        /// </summary>
        /// <param name="session">Sesión actual</param>
        /// <returns>True si se limpió correctamente</returns>
        public static bool ClearAll(this ISession session)
        {
            if (session == null)
                return false;

            try
            {
                session.Clear();
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Convenience Methods - Int32

        /// <summary>
        /// Guarda un entero en la sesión
        /// </summary>
        public static void SetInt(this ISession session, string key, int value)
        {
            ValidateSession(session);
            ValidateKey(key);
            session.SetInt32(key, value);
        }

        /// <summary>
        /// Obtiene un entero de la sesión o null si no existe
        /// </summary>
        public static int? GetInt(this ISession session, string key)
        {
            if (session == null || string.IsNullOrWhiteSpace(key))
                return null;

            try
            {
                return session.GetInt32(key);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Obtiene un entero de la sesión o un valor por defecto
        /// </summary>
        public static int GetIntOrDefault(this ISession session, string key, int defaultValue = 0)
        {
            return GetInt(session, key) ?? defaultValue;
        }

        #endregion

        #region Convenience Methods - String (SAFE - nombres diferentes a los nativos)

        // ⚠️ NO DEFINIR SetString ni GetString - ISession ya los tiene
        // Definirlos causa StackOverflowException por recursión infinita

        /// <summary>
        /// Guarda un string en la sesión de forma segura
        /// Usa este método en lugar de session.SetString() si quieres manejo de errores
        /// </summary>
        public static bool SetStringSafe(this ISession session, string key, string? value)
        {
            if (session == null || string.IsNullOrWhiteSpace(key))
                return false;

            try
            {
                if (value == null)
                {
                    session.Remove(key);
                }
                else
                {
                    session.SetString(key, value);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Obtiene un string de la sesión de forma segura (nunca lanza excepción)
        /// </summary>
        public static string? GetStringSafe(this ISession session, string key)
        {
            if (session == null || string.IsNullOrWhiteSpace(key))
                return null;

            try
            {
                return session.GetString(key);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Obtiene un string de la sesión o un valor por defecto
        /// </summary>
        public static string GetStringOrDefault(this ISession session, string key, string defaultValue = "")
        {
            return GetStringSafe(session, key) ?? defaultValue;
        }

        #endregion

        #region Convenience Methods - Bool

        /// <summary>
        /// Guarda un booleano en la sesión (como string "true"/"false")
        /// </summary>
        public static void SetBool(this ISession session, string key, bool value)
        {
            ValidateSession(session);
            ValidateKey(key);
            session.SetString(key, value ? "true" : "false");
        }

        /// <summary>
        /// Obtiene un booleano de la sesión o null si no existe
        /// </summary>
        public static bool? GetBool(this ISession session, string key)
        {
            if (session == null || string.IsNullOrWhiteSpace(key))
                return null;

            try
            {
                var value = session.GetString(key);

                if (string.IsNullOrWhiteSpace(value))
                    return null;

                return value.ToLowerInvariant() switch
                {
                    "true" or "1" or "yes" or "si" => true,
                    "false" or "0" or "no" => false,
                    _ => bool.TryParse(value, out var result) ? result : null
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Obtiene un booleano de la sesión o un valor por defecto
        /// </summary>
        public static bool GetBoolOrDefault(this ISession session, string key, bool defaultValue = false)
        {
            return GetBool(session, key) ?? defaultValue;
        }

        #endregion

        #region Convenience Methods - Decimal

        /// <summary>
        /// Guarda un decimal en la sesión
        /// </summary>
        public static void SetDecimal(this ISession session, string key, decimal value)
        {
            ValidateSession(session);
            ValidateKey(key);
            session.SetString(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Obtiene un decimal de la sesión o null si no existe
        /// </summary>
        public static decimal? GetDecimal(this ISession session, string key)
        {
            if (session == null || string.IsNullOrWhiteSpace(key))
                return null;

            try
            {
                var value = session.GetString(key);

                if (string.IsNullOrWhiteSpace(value))
                    return null;

                return decimal.TryParse(value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var result) ? result : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Obtiene un decimal de la sesión o un valor por defecto
        /// </summary>
        public static decimal GetDecimalOrDefault(this ISession session, string key, decimal defaultValue = 0m)
        {
            return GetDecimal(session, key) ?? defaultValue;
        }

        #endregion

        #region Convenience Methods - DateTime

        /// <summary>
        /// Guarda un DateTime en la sesión (formato ISO 8601)
        /// </summary>
        public static void SetDateTime(this ISession session, string key, DateTime value)
        {
            ValidateSession(session);
            ValidateKey(key);
            session.SetString(key, value.ToString("O")); // ISO 8601
        }

        /// <summary>
        /// Obtiene un DateTime de la sesión o null si no existe
        /// </summary>
        public static DateTime? GetDateTime(this ISession session, string key)
        {
            if (session == null || string.IsNullOrWhiteSpace(key))
                return null;

            try
            {
                var value = session.GetString(key);

                if (string.IsNullOrWhiteSpace(value))
                    return null;

                return DateTime.TryParse(value,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var result) ? result : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Obtiene un DateTime de la sesión o un valor por defecto
        /// </summary>
        public static DateTime GetDateTimeOrDefault(this ISession session, string key, DateTime? defaultValue = null)
        {
            return GetDateTime(session, key) ?? defaultValue ?? DateTime.MinValue;
        }

        #endregion

        #region Validation Helpers

        /// <summary>
        /// Valida que la sesión no sea nula
        /// </summary>
        private static void ValidateSession(ISession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session), EXC_SESSION_NULL);
        }

        /// <summary>
        /// Valida que la clave no sea nula o vacía
        /// </summary>
        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(EXC_KEY_NULL, nameof(key));
        }

        /// <summary>
        /// Valida que el valor no sea nulo
        /// </summary>
        private static void ValidateValue<T>(T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), EXC_VALUE_NULL);
        }

        #endregion
    }

    #region Session Keys Constants

    /// <summary>
    /// Constantes para claves de sesión comunes
    /// Centraliza las claves para evitar errores de tipeo
    /// </summary>
    public static class SessionKeys
    {
        // Usuario
        public const string UserId = "UserId";
        public const string UserName = "UserName";
        public const string UserRole = "UserRole";
        public const string UserPreferences = "UserPreferences";

        // Carrito
        public const string ShoppingCart = "ShoppingCart";
        public const string CartId = "CartId";
        public const string CartCount = "CartCount";

        // Checkout
        public const string CheckoutData = "CheckoutData";
        public const string SelectedAddress = "SelectedAddress";
        public const string SelectedPayment = "SelectedPayment";
        public const string Cupon = "Cupon";

        // Navegación
        public const string ReturnUrl = "ReturnUrl";
        public const string LastPage = "LastPage";

        // Mensajes
        public const string FlashMessage = "FlashMessage";
        public const string FlashType = "FlashType";
    }

    #endregion
}