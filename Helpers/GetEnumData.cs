using System.Reflection;
using System.ComponentModel.DataAnnotations;
using grefurBackend.Models;

namespace grefurBackend.Helpers;

public static class EnumHelper
{
    public static IEnumerable<object> GetEnumMetadata<T>() where T : Enum
    {
        var enumType = typeof(T);

        return Enum.GetValues(enumType)
            .Cast<T>()
            .Select(e =>
            {
                var field = enumType.GetField(e.ToString());
                var roleMetadata = field?.GetCustomAttribute<RoleMetadataAttribute>();
                var displayMetadata = field?.GetCustomAttribute<DisplayAttribute>();

                return new
                {
                    id = (int)(object)e,
                    name = e.ToString(),
                    description = roleMetadata?.Description ?? displayMetadata?.Description ?? "",
                    accessLevel = roleMetadata?.AccessLevel ?? 0,
                    displayName = displayMetadata?.Name ?? e.ToString()
                };
            });
    }
}