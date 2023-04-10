using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Google.Cloud.Firestore;

namespace GcpHelpers.Firestore;

public abstract class FirestoreConverterBase
{
    protected ILogger _log;

    protected FirestoreConverterBase()
    {
        Register();
    }

    public ILogger Log { set { _log = value; } }

    internal static ConcurrentDictionary<Type, FirestoreConverterBase> ConverterRegistry = new();

    protected static FirestoreConverterBase GetConverter(Type type)
    {
        return ConverterRegistry[type];
    }

    protected static IFirestoreConverter<T> GetConverter<T>()
    {
        if (!ConverterRegistry.ContainsKey(typeof(T)))
            return null;

        return (IFirestoreConverter<T>)ConverterRegistry[typeof(T)];
    }

    protected abstract void Register();

    protected static object ConvertValue(PropertyInfo property, object value)
    {
        if (value == null)
            return null;
            
        // Check for Nullable<T>'
        Type propertyType = Nullable.GetUnderlyingType(property.PropertyType);

        if (propertyType == null)
            propertyType = property.PropertyType;

        object propertyValue = null;

        if (propertyType == typeof(DateTime))
            propertyValue = ConvertUtcDate(value);
        else if (propertyType.IsEnum)
            propertyValue = ConvertEnum(propertyType, value);
        else if (propertyType == typeof(Int32))
            propertyValue = System.Convert.ToInt32(value); // Firestore always deserlizes int as Int64.
        else if (propertyType == typeof(string))
            propertyValue = value.ToString();
        else if (value is IEnumerable<object> && propertyType.IsGenericType)
            propertyValue = ConvertGenericList(property, value);
        else if (value is List<object> && propertyType.IsArray)
            propertyValue = ConvertList(propertyType, value);       
        else if (value is IDictionary<string, object>)
            propertyValue = ConvertCustomType(property, value);
        else 
            propertyValue = value;     

        return propertyValue;   
    }

    protected static object ConvertUtcDate(object value)
    {
        if (value is Timestamp)
            return ((Timestamp)value).ToDateTime();

        var dateValue = value as DateTime?;
        
        if (dateValue == null)
            dateValue = DateTime.MinValue;        

        return ((DateTime)dateValue).ToUniversalTime();
    }

    private static object ConvertEnum(Type propertyType, object value)
    {
        if (value is int)
        {
            string name = Enum.GetName(propertyType, value);
            return Enum.Parse(propertyType, name);
        }
        else
        {
            return Enum.Parse(propertyType, value.ToString());
        }
    }

    private static object ConvertList(Type propertyType, object value)
    {
        Type generic = typeof(List<>);
        Type[] typeArgs = { propertyType.GetElementType() };
        Type constructed = generic.MakeGenericType(typeArgs);
        
        var instance = Activator.CreateInstance(constructed);
        MethodInfo method = constructed.GetMethod("ToArray");

        foreach (var item in (IList)value)
        {
            ((IList)instance).Add(item);
        }

        return method.Invoke(instance, null);
    }

    private static object ConvertGenericList(PropertyInfo property, object value)
    {
        if (property.PropertyType.GenericTypeArguments.Count() < 1)
            throw new ApplicationException($"Unable to create list, no generic type argument for {property.Name}.");

        Type t = property.PropertyType.GenericTypeArguments.First();

        var listType = typeof(List<>);
        var constructedListType = listType.MakeGenericType(t);
        var genericList = Activator.CreateInstance(constructedListType);
        IList list = value as IList;

        var converter = GetConverter(t);
        MethodInfo method = converter.GetType().GetMethod("FromFirestore");
        if (method == null)
            throw new ApplicationException($"Unable to find converter method in type {nameof(converter)}");

        foreach (var item in list)
        {
            var customTypeInstance = method.Invoke(converter, 
                new object[] { (IDictionary<string, object>)item });

            ((IList)genericList).Add(customTypeInstance);
        }

        return genericList;
    }

    private static object ConvertCustomType(PropertyInfo property, object value)
    {
        object converter = GetConverter(property.PropertyType);

        if (converter == null)
            throw new ApplicationException($"Unable to find converter for type {nameof(property.DeclaringType)}");

        MethodInfo method = converter.GetType().GetMethod("FromFirestore");

        if (method == null)
            throw new ApplicationException($"Unable to find converter method in type {nameof(converter)}");
        
        return method.Invoke(converter, 
            new object[] { (IDictionary<string, object>)value });
    }  
}