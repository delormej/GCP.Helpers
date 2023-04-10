using Google.Cloud.Firestore;
using System.Reflection;
using Microsoft.Extensions.Logging;
using System.Collections;

namespace GcpHelpers.Firestore;

/// <summary>
/// Implements a generic custom converter to handle default POCO serialization
/// without explicit attribute decoration.
/// <see ref="https://cloud.google.com/dotnet/docs/reference/Google.Cloud.Firestore/latest/datamodel#custom-converters">Custom Converters</see>
/// <see ref="https://github.com/googleapis/google-cloud-dotnet/issues/3255">Related github issue</see>
/// </summary>
public class GenericFirestoreConverter<T> : FirestoreConverterBase, 
    IFirestoreConverter<T> where T : new()
{
    public const string FirestoreDocumentId = "id";

    private string _firestoreDocumentId;
    private readonly Dictionary<string, PropertyInfo> _properties;
    
    public GenericFirestoreConverter(
        string firestoreDocumentId = FirestoreDocumentId) : base()
    {
        _firestoreDocumentId = firestoreDocumentId;
        _properties = GetProperties();

        ValidateDocumentId();
    }

    protected override void Register()
    {
        ConverterRegistry.TryAdd(typeof(T), this);

        _log?.LogDebug($"{typeof(T).FullName} converter registered.");
    }    

    public object ToFirestore(T value)
    {
        var map = new Dictionary<string, object>();
        
        foreach(var p in _properties)
        {
            PropertyInfo property = p.Value;
            Type propertyType = property.PropertyType;
            object propertyValue = property.GetValue(value);

            if (propertyValue == null)
                continue;

            string propertyName = p.Key;

            // Object can have id & customId    -- add both to the map
            // Object has only id               -- add to the map "id"
            // Object has only customId         -- add both to the map

            if (propertyName == _firestoreDocumentId &&
                _firestoreDocumentId != FirestoreDocumentId)
            {
                map.Add(FirestoreDocumentId, propertyValue);
            }

            object serializedValue = null;

            if (IsRepeatedField(property))
            {
                var childMap = new List<Dictionary<string, object>>();

                Type genericType = propertyType.GenericTypeArguments[0] ?? typeof(object);
                
                if (ConverterRegistry.ContainsKey(genericType))
                {
                    object converter = GetConverter(genericType);
                    MethodInfo method = converter.GetType().GetMethod("ToFirestore");

                    foreach (var v in (IEnumerable)propertyValue)
                    {
                        childMap.Add( (Dictionary<string, object>)
                            method.Invoke(converter, new object[] { v }));
                    }
                }            
                else
                {
                    foreach (var v in (IEnumerable)propertyValue)
                    {
                        childMap.Add( (Dictionary<string, object>)
                            ConvertValue(p.Value, propertyValue));
                    }
                }
                
                serializedValue = childMap;
            }
            else
            {
                serializedValue = ConvertValue(p.Value, propertyValue);
            }
            
            map.Add(propertyName, serializedValue);
        }

        return map;
    }

    public T FromFirestore(object value)
    {
        var map = value as IDictionary<string, object>;

        if (map == null)
        {
            _log?.LogError($"Unexpected value: {value.GetType()}");
            return default;
        }

        T item = new();

        foreach (var pair in map)
        {
            string propertyName;

            if (pair.Key == FirestoreDocumentId && _firestoreDocumentId != null)
                propertyName = _firestoreDocumentId;
            else
                propertyName = pair.Key;
            
            TrySetValue(item, pair.Key, pair.Value);
        }

        return item;
    }

    protected virtual void TrySetValue(T instance, string name, object value)
    {
        if (name == FirestoreDocumentId)
            name = _firestoreDocumentId;

        _properties.TryGetValue(name, out PropertyInfo property);

        if (value == null || property == null || !property.CanWrite)
            return;
        try
        {
            property.SetValue(instance, ConvertValue(property, value));        
        }
        catch (Exception e)
        {
            _log?.LogError($"Unable to convert {property.Name} of type {property.DeclaringType.FullName}, error:  {e.Message}");
        }
    }

    private bool IsRepeatedField(PropertyInfo p) =>
        p.PropertyType.IsGenericType && 
                (p.PropertyType.GetGenericTypeDefinition() == typeof(List<>) ||
                p.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>));

    private Dictionary<string, PropertyInfo> GetProperties()
    {
        var dictionary = new Dictionary<string, PropertyInfo>();

        var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);

        if (properties != null)
        {
            foreach (var p in properties)
            {
                // Skip "id" if a custom id property name was specified
                if (_firestoreDocumentId != FirestoreDocumentId && 
                        p.Name == FirestoreDocumentId)
                    continue;
                
                dictionary.Add(p.Name, p);
            }
        }
        else
        {
            _log?.LogError($"No public instance properties found for type {nameof(T)}");
        }
        
        return dictionary;
    }

    private void ValidateDocumentId()
    {
        if (_firestoreDocumentId != FirestoreDocumentId)
        {
            _properties.TryGetValue(_firestoreDocumentId, 
                out PropertyInfo idProperty);

            if (idProperty == null)
                throw new ArgumentException($"Unable to find required id property {_firestoreDocumentId} on {typeof(T).FullName}");
        }
    }
}