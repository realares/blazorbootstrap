namespace BlazorBootstrap;
using System.ComponentModel.DataAnnotations;
using System.Reflection;


/// <summary>
/// Various extension methods for <see cref="Type" />.
/// </summary>
public static class TypeExtensions
{
    private static readonly Dictionary<Type, string> typeMap = new()
    {
            { typeof(short), StringConstants.PropertyTypeNameInt16 },
            { typeof(int), StringConstants.PropertyTypeNameInt32 },
            { typeof(long), StringConstants.PropertyTypeNameInt64 },
            { typeof(char), StringConstants.PropertyTypeNameChar },
            { typeof(string), StringConstants.PropertyTypeNameString },
            { typeof(float), StringConstants.PropertyTypeNameSingle },
            { typeof(decimal), StringConstants.PropertyTypeNameDecimal },
            { typeof(double), StringConstants.PropertyTypeNameDouble },
            { typeof(DateTime), StringConstants.PropertyTypeNameDateTime },
            { typeof(bool), StringConstants.PropertyTypeNameBoolean },
            { typeof(Guid), StringConstants.PropertyTypeNameGuid },
#if NET6_0_OR_GREATER
            { typeof(DateOnly), StringConstants.PropertyTypeNameDateOnly },
#endif
        };

    #region Methods

    /// <summary>
    /// Get property type name.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="propertyName"></param>
    /// <returns>string</returns>
    public static string GetPropertyTypeName(this Type type, string propertyName)
    {
        if (type is null || string.IsNullOrWhiteSpace(propertyName))
            return string.Empty;

        Type? currentType = GetPropertyType(type, propertyName);

        if (currentType == null)
            return string.Empty;

        if (currentType.IsEnum)
            return StringConstants.PropertyTypeNameEnum;

        if (typeMap.TryGetValue(currentType, out var typeName))
            return typeName;

        return string.Empty;
    }

    /// <summary>
    /// Get property type.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="propertyName"></param>
    /// <returns>Type?</returns>
    public static Type? GetPropertyType(this Type type, string propertyName)
    {
        if (type == null || string.IsNullOrWhiteSpace(propertyName))
            return null;

        // Split the property path: e.g. "User.Name.First"
        var parts = propertyName.Split('.');
        Type? currentType = type;

        foreach (var part in parts)
        {
            var property = currentType?.GetProperty(part);
            if (property == null)
                return null;

            currentType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        }
        return currentType;
    }

    public static string? GetDisplayName(this Type type, string? name)
    {
        if (name != null)
        {
            var attr = type!.GetMember(name).FirstOrDefault()?.GetCustomAttribute<DisplayAttribute>();
            name = attr?.Name ?? name;
        }

        return name;
    }

    #endregion
}
