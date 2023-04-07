using Google.Cloud.Firestore;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace GcpHelpers.Firestore;

/// <summary>
/// Implements a generic custom converter to handle default POCO serialization
/// without explicit attribute decoration.
/// <see ref="https://cloud.google.com/dotnet/docs/reference/Google.Cloud.Firestore/latest/datamodel#custom-converters">Custom Converters</see>
/// <see ref="https://github.com/googleapis/google-cloud-dotnet/issues/3255">Related github issue</see>
/// </summary>
public class GenericFirestoreConverter<T> : IFirestoreConverter<T> where T : new()
{
    ILogger _log;
    string _firestoreDocumentId;
    ConverterRegistry _converters;
    const string FirestoreDocumentId = "id";
    readonly Dictionary<string, PropertyInfo> _properties;
    
    public GenericFirestoreConverter(ILogger log, 
        ConverterRegistry registryCache,
        string firestoreDocumentId)
    {
        _log = log;
        _converters = registryCache;
        _firestoreDocumentId = firestoreDocumentId;
        _properties = GetProperties();

        _properties.TryGetValue(_firestoreDocumentId, 
            out PropertyInfo idProperty);

        if (idProperty == null)
            throw new ArgumentException($"Unable to find required id property {_firestoreDocumentId} on {nameof(T)}");
    }

    public object ToFirestore(T value)
    {
        var map = new Dictionary<string, object>();
        
        foreach(var p in _properties)
        {
            string propertyName;
            object propertyValue;
            
            if (p.Key == _firestoreDocumentId)
                propertyName = FirestoreDocumentId;
            else
                propertyName = p.Key;

            propertyValue = p.Value.GetValue(value);
            
            if (p.Value.DeclaringType == typeof(DateTime))
                propertyValue = GetUtcDate(propertyValue);

            map.Add(propertyName, propertyValue);
        }

        return map;
    }

    public T FromFirestore(object value)
    {
        var map = value as IDictionary<string, object>;

        if (map == null)
            throw new ArgumentException($"Unexpected value: {value.GetType()}");

        T converted = new();

        foreach (var kvp in map)
            Convert(converted, kvp.Key, kvp.Value);

        return converted;
    }

    protected virtual void Convert(T instance, string name, object value)
    {
        _properties.TryGetValue(name, out PropertyInfo property);

        if (property == null)
        {
            _log?.LogError($"Unable to find property {name} in map on type {nameof(T)}");
            return;
        }

        TrySetValue(instance, property, value);
    }

    protected virtual object ConvertFromDictionary(PropertyInfo property, 
        IDictionary<string, object> value)
    {
        MethodInfo method = null;
        object converter = null;

        foreach (var c in _converters)
        {
            if (property.DeclaringType !=
                    c.GetType().GetGenericTypeDefinition())
                continue;

            converter = c;
            method = converter.GetType().GetMethod("FromFirestore");
            break;   
        }

        if (method == null)
        {
            _log?.LogError($"Unable to find converter for type {nameof(property.DeclaringType)}");
            return null;
        }
        
        return method.Invoke(converter, new object[] { value });
    }  

    private Dictionary<string, PropertyInfo> GetProperties()
    {
        var dictionary = new Dictionary<string, PropertyInfo>();

        var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);

        if (properties != null)
        {
            foreach (var p in properties)
                dictionary.Add(p.Name, p);
        }
        else
        {
            _log?.LogError($"No public instance properties found for type {nameof(T)}");
        }
        
        return dictionary;
    }

    private void TrySetValue(T item, PropertyInfo property, object value)
    {
        if (value == null)
        {
            _log?.LogWarning($"Value of {property.Name} on {nameof(T)} null.");
            return;
        }
       
        try
        {
            property.SetValue(item, GetPropertyValue(property, value));
        }
        catch (Exception e)
        {
            _log?.LogError($"Unable to convert {property.Name} of type {nameof(property.DeclaringType)} on {nameof(T)}, error:  {e.Message}");
        }
    }

    private object GetPropertyValue(PropertyInfo property, object value)
    {
        // Check for Nullable<T>'
        Type propertyType = Nullable.GetUnderlyingType(property.PropertyType);

        if (propertyType == null)
            propertyType = property.PropertyType;

        object propertyValue = null;

        if (propertyType == typeof(DateTime))
            propertyValue = GetUtcDate(value);
        else if (propertyType.IsEnum)
            propertyValue = GetEnum(propertyType, value);
        else if (propertyType == typeof(Int32))
            propertyValue = System.Convert.ToInt32(value); // Firestore always deserlizes int as Int64.
        else if (propertyType == typeof(string))
            propertyValue = value.ToString();
        else if (value is IEnumerable<object> && propertyType.IsGenericType)
            propertyValue = Helper.CreateGenericList(property, value);
        else if (value is List<object> && propertyType.IsArray)
            propertyValue = GetList(propertyType, value);       
        else if (value is IDictionary<string, object>)
            propertyValue = ConvertFromDictionary(property, (IDictionary<string, object>)value);
        else 
            propertyValue = value;     

        return propertyValue;   
    }

    private object GetUtcDate(object value)
    {
        var dateValue = value as DateTime?;
        
        if (dateValue == null)
            dateValue = DateTime.MinValue;        

        return dateValue?.ToUniversalTime();
    }

    private object GetEnum(Type propertyType, object value)
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

    private object GetList(Type propertyType, object value)
    {
        Type generic = typeof(List<>);
        Type[] typeArgs = { propertyType.GetElementType() };
        Type constructed = generic.MakeGenericType(typeArgs);
        
        var instance = Activator.CreateInstance(constructed);
        MethodInfo method = constructed.GetMethod("ToArray");

        foreach (var o in (List<object>)value)
        {
            ((IList<object>)instance).Add(o);
        }

        return method.Invoke(instance, null);
    }
}