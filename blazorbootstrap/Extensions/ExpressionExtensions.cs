using System.Linq.Expressions;
using System.Reflection;

namespace BlazorBootstrap;

public static class ExpressionExtensions
{
    #region Methods

    public static Expression<Func<TItem, bool>> And<TItem>(this Expression<Func<TItem, bool>> leftExpression, Expression<Func<TItem, bool>> rightExpression)
    {
        var parameterExpression = leftExpression.Parameters[0];
        SubstExpressionVisitor substExpressionVisitor = new();
        substExpressionVisitor.subst[rightExpression.Parameters[0]] = parameterExpression;

        Expression body = Expression.AndAlso(leftExpression.Body, substExpressionVisitor.Visit(rightExpression.Body));
        return Expression.Lambda<Func<TItem, bool>>(body, parameterExpression);
    }

    public static Expression<Func<TItem, bool>> Or<TItem>(this Expression<Func<TItem, bool>> leftExpression, Expression<Func<TItem, bool>> rightExpression)
    {
        var parameterExpression = leftExpression.Parameters[0];
        SubstExpressionVisitor substExpressionVisitor = new();
        substExpressionVisitor.subst[rightExpression.Parameters[0]] = parameterExpression;

        Expression body = Expression.OrElse(leftExpression.Body, substExpressionVisitor.Visit(rightExpression.Body));
        return Expression.Lambda<Func<TItem, bool>>(body, parameterExpression);
    }

    public static Expression<Func<TItem, bool>>? GetExpressionDelegate<TItem>(ParameterExpression parameterExpression, FilterItem filterItem)
    {
        var propertyType = typeof(TItem).GetPropertyType(filterItem.PropertyName);
        var propertyTypeName = typeof(TItem).GetPropertyTypeName(filterItem.PropertyName);

        if (IsNumericType(propertyTypeName))
            return GetNumericFilter<TItem>(parameterExpression, filterItem, propertyTypeName);

        if (IsStringType(propertyTypeName))
            return GetStringFilter<TItem>(parameterExpression, filterItem);

        if (IsDateType(propertyTypeName))
            return GetDateFilter<TItem>(parameterExpression, filterItem, propertyTypeName);

        if (propertyTypeName == StringConstants.PropertyTypeNameBoolean)
            return GetBooleanFilter<TItem>(parameterExpression, filterItem);

        if (propertyTypeName == StringConstants.PropertyTypeNameEnum)
            return GetEnumFilter<TItem>(parameterExpression, filterItem, propertyType!);

        if (propertyTypeName == StringConstants.PropertyTypeNameGuid)
            return GetGuidFilter<TItem>(parameterExpression, filterItem);

        return null;
    }

    #endregion

    #region Specific Type Filters

    private static Expression<Func<TItem, bool>> GetNumericFilter<TItem>(ParameterExpression parameter, FilterItem filter, string typeName)
    {
        return filter.Operator switch
        {
            FilterOperator.Equals => GetComparisonDelegate<TItem>(parameter, filter, typeName, Expression.Equal),
            FilterOperator.NotEquals => GetComparisonDelegate<TItem>(parameter, filter, typeName, Expression.NotEqual),
            FilterOperator.LessThan => GetComparisonDelegate<TItem>(parameter, filter, typeName, Expression.LessThan),
            FilterOperator.LessThanOrEquals => GetComparisonDelegate<TItem>(parameter, filter, typeName, Expression.LessThanOrEqual),
            FilterOperator.GreaterThan => GetComparisonDelegate<TItem>(parameter, filter, typeName, Expression.GreaterThan),
            FilterOperator.GreaterThanOrEquals => GetComparisonDelegate<TItem>(parameter, filter, typeName, Expression.GreaterThanOrEqual),
            _ => GetComparisonDelegate<TItem>(parameter, filter, typeName, Expression.Equal)
        };
    }

    private static Expression<Func<TItem, bool>> GetStringFilter<TItem>(ParameterExpression parameter, FilterItem filter)
    {
        return filter.Operator switch
        {
            FilterOperator.Contains => GetStringContainsExpressionDelegate<TItem>(parameter, filter),
            FilterOperator.DoesNotContain => GetStringDoesNotContainExpressionDelegate<TItem>(parameter, filter),
            FilterOperator.StartsWith => GetStringStartsWithExpressionDelegate<TItem>(parameter, filter),
            FilterOperator.EndsWith => GetStringEndsWithExpressionDelegate<TItem>(parameter, filter),
            FilterOperator.Equals => GetStringEqualsExpressionDelegate<TItem>(parameter, filter),
            FilterOperator.NotEquals => GetStringNotEqualsExpressionDelegate<TItem>(parameter, filter),
            _ => GetStringContainsExpressionDelegate<TItem>(parameter, filter)
        };
    }

    private static Expression<Func<TItem, bool>> GetDateFilter<TItem>(ParameterExpression parameter, FilterItem filter, string typeName)
    {
        return filter.Operator switch
        {
            FilterOperator.Equals => GetComparisonDelegate<TItem>(parameter, filter, typeName, Expression.Equal),
            FilterOperator.NotEquals => GetComparisonDelegate<TItem>(parameter, filter, typeName, Expression.NotEqual),
            FilterOperator.LessThan => GetComparisonDelegate<TItem>(parameter, filter, typeName, Expression.LessThan),
            FilterOperator.LessThanOrEquals => GetComparisonDelegate<TItem>(parameter, filter, typeName, Expression.LessThanOrEqual),
            FilterOperator.GreaterThan => GetComparisonDelegate<TItem>(parameter, filter, typeName, Expression.GreaterThan),
            FilterOperator.GreaterThanOrEquals => GetComparisonDelegate<TItem>(parameter, filter, typeName, Expression.GreaterThanOrEqual),
            _ => GetComparisonDelegate<TItem>(parameter, filter, typeName, Expression.Equal)
        };
    }

    private static Expression<Func<TItem, bool>> GetBooleanFilter<TItem>(ParameterExpression parameter, FilterItem filter)
    {
        return filter.Operator switch
        {
            FilterOperator.NotEquals => GetBooleanNotEqualExpressionDelegate<TItem>(parameter, filter),
            _ => GetBooleanEqualExpressionDelegate<TItem>(parameter, filter)
        };
    }

    private static Expression<Func<TItem, bool>> GetEnumFilter<TItem>(ParameterExpression parameter, FilterItem filter, Type propertyType)
    {
        return filter.Operator switch
        {
            FilterOperator.NotEquals => GetEnumNotEqualExpressionDelegate<TItem>(parameter, filter, propertyType),
            _ => GetEnumEqualExpressionDelegate<TItem>(parameter, filter, propertyType)
        };
    }

    private static Expression<Func<TItem, bool>> GetGuidFilter<TItem>(ParameterExpression parameter, FilterItem filter)
    {
        return filter.Operator switch
        {
            FilterOperator.NotEquals => GetGuidNotEqualExpressionDelegate<TItem>(parameter, filter),
            _ => GetGuidEqualExpressionDelegate<TItem>(parameter, filter)
        };
    }

    #endregion

    #region Expression Builders

    private static Expression<Func<TItem, bool>> GetComparisonDelegate<TItem>(
        ParameterExpression parameter,
        FilterItem filter,
        string typeName,
        Func<Expression, Expression, BinaryExpression> comparisonAction)
    {
        var propertyExpression = GetPropertyExpression(parameter, filter);
        if (propertyExpression == null) throw new ArgumentException($"Property {filter.PropertyName} not found.");

        var constantValue = GetConstantExpression(filter, typeName, propertyExpression.Type);

        // Handle Nullable types
        Expression comparison;
        if (propertyExpression.Type.IsNullableType())
        {
            var hasValueExpression = Expression.Property(propertyExpression, "HasValue");
            var valueExpression = Expression.Property(propertyExpression, "Value");

            // Create comparison: property.Value == constant
            var opExpression = comparisonAction(valueExpression, Expression.Convert(constantValue, valueExpression.Type));

            // If nullable, we must check HasValue. If false, result is false (except for NotEqual logic which might differ)
            comparison = Expression.AndAlso(hasValueExpression, opExpression);
        }
        else
        {
            comparison = comparisonAction(propertyExpression, Expression.Convert(constantValue, propertyExpression.Type));
        }

        return Expression.Lambda<Func<TItem, bool>>(comparison, parameter);
    }

    public static Expression<Func<TItem, bool>> GetBooleanEqualExpressionDelegate<TItem>(ParameterExpression parameter, FilterItem filter)
    {
        var property = GetPropertyExpression(parameter, filter)!;
        bool.TryParse(filter.Value, out var val);
        var constant = Expression.Constant(val);

        Expression body = property.Type.IsNullableType()
            ? Expression.Equal(Expression.Property(property, "Value"), constant)
            : Expression.Equal(property, constant);

        if (property.Type.IsNullableType())
            body = Expression.AndAlso(Expression.Property(property, "HasValue"), body);

        return Expression.Lambda<Func<TItem, bool>>(body, parameter);
    }

    public static Expression<Func<TItem, bool>> GetBooleanNotEqualExpressionDelegate<TItem>(ParameterExpression parameter, FilterItem filter)
    {
        var property = GetPropertyExpression(parameter, filter)!;
        bool.TryParse(filter.Value, out var val);
        var constant = Expression.Constant(val);

        Expression body = property.Type.IsNullableType()
            ? Expression.NotEqual(Expression.Property(property, "Value"), constant)
            : Expression.NotEqual(property, constant);

        return Expression.Lambda<Func<TItem, bool>>(body, parameter);
    }

    public static Expression<Func<TItem, bool>> GetStringContainsExpressionDelegate<TItem>(ParameterExpression parameter, FilterItem filter)
    {
        var property = GetPropertyExpression(parameter, filter)!;
        var value = Expression.Constant(filter.Value, typeof(string));
        var comparison = Expression.Constant(filter.StringComparison);
        var method = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string), typeof(StringComparison) });

        var nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        var call = Expression.Call(property, method!, value, comparison);

        return Expression.Lambda<Func<TItem, bool>>(Expression.AndAlso(nullCheck, call), parameter);
    }

    public static Expression<Func<TItem, bool>> GetStringDoesNotContainExpressionDelegate<TItem>(ParameterExpression parameter, FilterItem filter)
    {
        var property = GetPropertyExpression(parameter, filter)!;
        var value = Expression.Constant(filter.Value, typeof(string));
        var comparison = Expression.Constant(filter.StringComparison);
        var method = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string), typeof(StringComparison) });

        var nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        var call = Expression.Not(Expression.Call(property, method!, value, comparison));

        return Expression.Lambda<Func<TItem, bool>>(Expression.AndAlso(nullCheck, call), parameter);
    }

    public static Expression<Func<TItem, bool>> GetStringStartsWithExpressionDelegate<TItem>(ParameterExpression parameter, FilterItem filter)
    {
        var property = GetPropertyExpression(parameter, filter)!;
        var value = Expression.Constant(filter.Value, typeof(string));
        var comparison = Expression.Constant(filter.StringComparison);
        var method = typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string), typeof(StringComparison) });

        var nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        var call = Expression.Call(property, method!, value, comparison);

        return Expression.Lambda<Func<TItem, bool>>(Expression.AndAlso(nullCheck, call), parameter);
    }

    public static Expression<Func<TItem, bool>> GetStringEndsWithExpressionDelegate<TItem>(ParameterExpression parameter, FilterItem filter)
    {
        var property = GetPropertyExpression(parameter, filter)!;
        var value = Expression.Constant(filter.Value, typeof(string));
        var comparison = Expression.Constant(filter.StringComparison);
        var method = typeof(string).GetMethod(nameof(string.EndsWith), new[] { typeof(string), typeof(StringComparison) });

        var nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        var call = Expression.Call(property, method!, value, comparison);

        return Expression.Lambda<Func<TItem, bool>>(Expression.AndAlso(nullCheck, call), parameter);
    }

    public static Expression<Func<TItem, bool>> GetStringEqualsExpressionDelegate<TItem>(ParameterExpression parameter, FilterItem filter)
    {
        var property = GetPropertyExpression(parameter, filter)!;
        var value = Expression.Constant(filter.Value, typeof(string));
        var comparison = Expression.Constant(filter.StringComparison);
        var method = typeof(string).GetMethod(nameof(string.Equals), new[] { typeof(string), typeof(StringComparison) });

        var nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        var call = Expression.Call(property, method!, value, comparison);

        return Expression.Lambda<Func<TItem, bool>>(Expression.AndAlso(nullCheck, call), parameter);
    }

    public static Expression<Func<TItem, bool>> GetStringNotEqualsExpressionDelegate<TItem>(ParameterExpression parameter, FilterItem filter)
    {
        var property = GetPropertyExpression(parameter, filter)!;
        var value = Expression.Constant(filter.Value, typeof(string));
        var comparison = Expression.Constant(filter.StringComparison);
        var method = typeof(string).GetMethod(nameof(string.Equals), new[] { typeof(string), typeof(StringComparison) });

        var nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        var call = Expression.Not(Expression.Call(property, method!, value, comparison));

        return Expression.Lambda<Func<TItem, bool>>(Expression.AndAlso(nullCheck, call), parameter);
    }

    public static Expression<Func<TItem, bool>> GetEnumEqualExpressionDelegate<TItem>(ParameterExpression parameter, FilterItem filter, Type propertyType)
    {
        var property = GetPropertyExpression(parameter, filter)!;
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (!Enum.TryParse(underlyingType, filter.Value, out var enumVal)) enumVal = Activator.CreateInstance(underlyingType);

        return Expression.Lambda<Func<TItem, bool>>(Expression.Equal(property, Expression.Convert(Expression.Constant(enumVal), propertyType)), parameter);
    }

    public static Expression<Func<TItem, bool>> GetEnumNotEqualExpressionDelegate<TItem>(ParameterExpression parameter, FilterItem filter, Type propertyType)
    {
        var property = GetPropertyExpression(parameter, filter)!;
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (!Enum.TryParse(underlyingType, filter.Value, out var enumVal)) enumVal = Activator.CreateInstance(underlyingType);

        return Expression.Lambda<Func<TItem, bool>>(Expression.NotEqual(property, Expression.Convert(Expression.Constant(enumVal), propertyType)), parameter);
    }

    public static Expression<Func<TItem, bool>> GetGuidEqualExpressionDelegate<TItem>(ParameterExpression parameter, FilterItem filter)
    {
        var property = GetPropertyExpression(parameter, filter)!;
        Guid.TryParse(filter.Value, out var guid);
        return Expression.Lambda<Func<TItem, bool>>(Expression.Equal(property, Expression.Constant(guid)), parameter);
    }

    public static Expression<Func<TItem, bool>> GetGuidNotEqualExpressionDelegate<TItem>(ParameterExpression parameter, FilterItem filter)
    {
        var property = GetPropertyExpression(parameter, filter)!;
        Guid.TryParse(filter.Value, out var guid);
        return Expression.Lambda<Func<TItem, bool>>(Expression.NotEqual(property, Expression.Constant(guid)), parameter);
    }

    #endregion

    #region Helpers

    private static ConstantExpression GetConstantExpression(FilterItem filter, string typeName, Type targetType)
    {
        if (filter.Value == null) return Expression.Constant(null, targetType);

        object? val = typeName switch
        {
            StringConstants.PropertyTypeNameInt16 => short.TryParse(filter.Value, out var v) ? v : (short)0,
            StringConstants.PropertyTypeNameInt32 => int.TryParse(filter.Value, out var v) ? v : 0,
            StringConstants.PropertyTypeNameInt64 => long.TryParse(filter.Value, out var v) ? v : 0L,
            StringConstants.PropertyTypeNameSingle => float.TryParse(filter.Value, out var v) ? v : 0f,
            StringConstants.PropertyTypeNameDouble => double.TryParse(filter.Value, out var v) ? v : 0.0,
            StringConstants.PropertyTypeNameDecimal => decimal.TryParse(filter.Value, out var v) ? v : 0m,
            StringConstants.PropertyTypeNameDateOnly => DateOnly.TryParse(filter.Value, out var v) ? v : default,
            StringConstants.PropertyTypeNameDateTime => DateTime.TryParse(filter.Value, out var v) ? v : default,
            StringConstants.PropertyTypeNameDateTimeOffset => DateTimeOffset.TryParse(filter.Value, out var v) ? v : default,
            _ => filter.Value
        };

        return Expression.Constant(val);
    }

    private static MemberExpression? GetPropertyExpression(ParameterExpression parameter, FilterItem filter)
    {
        Expression current = parameter;
        foreach (var part in filter.PropertyName.Split('.'))
        {
            current = Expression.Property(current, part);
        }
        return current as MemberExpression;
    }

    public static bool IsNullableType(this Type type) => Nullable.GetUnderlyingType(type) != null;

    private static bool IsNumericType(string typeName) => typeName is
        StringConstants.PropertyTypeNameInt16 or StringConstants.PropertyTypeNameInt32 or
        StringConstants.PropertyTypeNameInt64 or StringConstants.PropertyTypeNameSingle or
        StringConstants.PropertyTypeNameDecimal or StringConstants.PropertyTypeNameDouble;

    private static bool IsStringType(string typeName) => typeName is
        StringConstants.PropertyTypeNameString or StringConstants.PropertyTypeNameChar;

    private static bool IsDateType(string typeName) => typeName is
        StringConstants.PropertyTypeNameDateOnly or StringConstants.PropertyTypeNameDateTime or
        StringConstants.PropertyTypeNameDateTimeOffset;

    #endregion
}

internal class SubstExpressionVisitor : ExpressionVisitor
{
    public Dictionary<Expression, Expression> subst = new();
    protected override Expression VisitParameter(ParameterExpression p) => subst.TryGetValue(p, out var res) ? res : p;
}