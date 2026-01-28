using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Simone.ModelBinders
{
    /// <summary>
    /// Model binder personalizado para manejar valores decimales
    /// usando InvariantCulture (formato inglés: punto como decimal).
    /// 
    /// Esto evita problemas de conversión cuando el servidor tiene
    /// configurada una cultura diferente (ej. español donde el punto
    /// se usa como separador de miles en lugar de decimal).
    /// </summary>
    public class InvariantDecimalModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext == null)
                throw new ArgumentNullException(nameof(bindingContext));

            var modelName = bindingContext.ModelName;
            var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);

            if (valueProviderResult == ValueProviderResult.None)
                return Task.CompletedTask;

            bindingContext.ModelState.SetModelValue(modelName, valueProviderResult);

            var value = valueProviderResult.FirstValue;

            // Si el valor está vacío, retornar sin error
            if (string.IsNullOrWhiteSpace(value))
                return Task.CompletedTask;

            // ✅ CRITICAL: Normalizar el string para InvariantCulture
            // Remover separadores de miles (puntos en formato español)
            var normalizedValue = value.Trim();

            // Si tiene múltiples puntos, son separadores de miles (formato español)
            var dotCount = normalizedValue.Count(c => c == '.');
            var commaCount = normalizedValue.Count(c => c == ',');

            if (dotCount > 1)
            {
                // Formato español: 1.200.000,50 → remover puntos
                normalizedValue = normalizedValue.Replace(".", "");
            }

            // Convertir coma decimal a punto (formato español → inglés)
            normalizedValue = normalizedValue.Replace(',', '.');

            // ✅ CRITICAL: Usar InvariantCulture para parsear
            if (decimal.TryParse(normalizedValue,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture,
                out var result))
            {
                bindingContext.Result = ModelBindingResult.Success(result);
            }
            else
            {
                // Si falla el parsing, agregar error al ModelState
                bindingContext.ModelState.TryAddModelError(
                    modelName,
                    $"El valor '{value}' no es un número decimal válido.");
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Provider que indica cuándo usar el InvariantDecimalModelBinder.
    /// Se aplica automáticamente a todos los parámetros de tipo decimal.
    /// </summary>
    public class InvariantDecimalModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            // Aplicar solo a decimal y decimal?
            if (context.Metadata.ModelType == typeof(decimal) ||
                context.Metadata.ModelType == typeof(decimal?))
            {
                return new InvariantDecimalModelBinder();
            }

            return null;
        }
    }
}
