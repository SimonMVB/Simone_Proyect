using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Simone.Extensions // Ajusta el namespace a tu preferencia
{
    public static class SessionExtensions
    {
        /// <summary>
        /// Guarda un objeto como JSON en la sesión.
        /// </summary>
        public static void SetObjectAsJson(this ISession session, string key, object value)
        {
            var stringValue = JsonConvert.SerializeObject(value);
            session.SetString(key, stringValue);
        }

        /// <summary>
        /// Recupera un objeto desde la sesión, deserializándolo desde JSON.
        /// </summary>
        public static T GetObjectFromJson<T>(this ISession session, string key)
        {
            var stringValue = session.GetString(key);
            return stringValue == null ? default(T) : JsonConvert.DeserializeObject<T>(stringValue);
        }
    }
}
