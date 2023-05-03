using System.Reflection;
using Google.Cloud.Firestore;
using System.Collections;
using Microsoft.Extensions.Logging;

namespace GcpHelpers.Firestore
{
    public interface IConverterSupportsLog
    {
        public ILogger Log { set; } 
    }

    /// <summary>
    /// Implements a generic custom converter to handle default POCO serialization
    /// without explicit attribute decoration.
    /// <see ref="https://cloud.google.com/dotnet/docs/reference/Google.Cloud.Firestore/latest/datamodel#custom-converters">Custom Converters</see>
    /// </summary>
    public class GenericFirestoreConverter<T> : IFirestoreConverter<T>, 
        IConverterSupportsLog 
            where T : class, new()
    {
        private readonly PropertyInfo[] _properties;
        private readonly string _idProperty;
        // Expected attribute name for the required unique identifier.
        private const string FirestoreId = "id";
        
        private ILogger _log;

        public ILogger Log { set { _log = value; }}

        /// <summary>
        /// Create a converter by specifing the unique identifier property name
        /// of the type to be converted.
        /// </summary>
        public GenericFirestoreConverter(string idProperty) : this()
        {
            if (idProperty == FirestoreId)
                return;

            if(GetProperty(idProperty) == null)
                throw new ArgumentException(
                    $"idProperty must be the name of a public property of type {typeof(T)}");
            
            _idProperty = idProperty;
        }

        public GenericFirestoreConverter()
        {
            _properties = GetProperties();
        }

        public object ToFirestore(T source) 
        {
            var map = new Dictionary<string, object>();
            
            foreach(var p in _properties)
            {
                // Do not serialize properties without a public getter.   
                if (!p.CanRead)
                    continue;

                object value = p.GetValue(source);

                if (value == null)
                    continue;

                // Firestore expects an id property as unique identifier.
                if (p.Name == _idProperty)
                {
                    map.Add(FirestoreId, value);
                }

                // Try to see if we have a custom converter for this type.
                dynamic converter = ConverterCache.Get(p.PropertyType);

                if (converter != null)
                    map.Add(p.Name, converter.ToFirestore((dynamic)value));
                else
                    map.Add(p.Name, value);
            }
            return map;
        } 


        public T FromFirestore(object value)
        {
            T item = new T();

            if (value is IDictionary<string, object> map)
            {                
                foreach(var pair in map)
                {
                    PropertyInfo property = null;

                    if (pair.Key == FirestoreId && _idProperty != null)
                        property = GetProperty(_idProperty);
                    else
                        property = GetProperty(pair.Key);

                    // Don't write properties that don't have a public setter.
                    if (property != null && property.CanWrite)
                        TrySetValue(item, property, pair.Value);
                }
            }

            return item;
        }

        private PropertyInfo[] GetProperties() =>
            typeof(T).GetProperties(BindingFlags.Public|BindingFlags.Instance);

        private PropertyInfo GetProperty(string name)
        {  
            if (_properties?.Count() > 0)
            {
                return _properties.Where(p => p.Name == name).FirstOrDefault();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Handles special cases for deserialization which aren't handled well by the default
        /// serialization. 
        /// </summary>
        private void TrySetValue(T item, PropertyInfo property, object value)
        {
            if (value == null)
                return;
            
            // Check for Nullable<T>'
            Type propertyType = Nullable.GetUnderlyingType(property.PropertyType);

            if (propertyType == null)
                propertyType = property.PropertyType;

            try
            {
                if (propertyType.IsEnum)
                {
                    SetEnum(item, property, value);
                }
                else if (propertyType == typeof(Int32) || propertyType == typeof(Int16))
                {
                    SetInt32(item, property, value);
                }
                else if (propertyType == typeof(string))
                {
                    SetString(item, property, value);
                }                         
                else if (value is IDictionary<string, object>)
                {
                    SetCustom(item, property, value);
                }
                else if (value is IEnumerable<object>)
                {
                    SetList(item, property, value);
                }
                else 
                {
                    SetCustom(item, property, value);
                }
            }
            catch (Exception e)
            {
                _log?.LogError($"Error in TrySetValue {property.Name}", e);
            }
        }

        private void SetEnum(object item, PropertyInfo property, object value)
        {
            Type propertyType = property.PropertyType;

            if (value == null)
                return;
                
            if (value is int)
            {
                string name = Enum.GetName(propertyType, value);
                property.SetValue(item, Enum.Parse(propertyType, name));
            }
            else
            {
                property.SetValue(item, 
                    Enum.Parse(propertyType, value.ToString())
                );
            }
        }

        private void SetString(object item, PropertyInfo property, object value)
        {
            property.SetValue(item, value.ToString());                  
        }

        private void SetInt32(object item, PropertyInfo property, object value)
        {
            // Firestore always deserlizes int as Int64.
            property.SetValue(item, Convert.ToInt32(value));            
        }

        private void SetList(object item, PropertyInfo property, object value)
        {
            Type type;
            
            if (property.PropertyType.IsGenericType)
            {
                type = property.PropertyType.GenericTypeArguments.First();
            }
            else if (property.PropertyType.IsArray)
            {
                type = property.PropertyType.GetElementType();
            }
            else
            {
                throw new ApplicationException(
                    $"Unable to determine element type of {property.PropertyType}");
            }

            dynamic converter = ConverterCache.Get(type);

            IList list = FirestoreListDeserializer.CreateGenericList(type);

            foreach (var listItem in (IEnumerable<object>)value) 
            {
                if (converter != null)
                {
                    var result = converter.FromFirestore(listItem);
                    list.Add(result);
                }
                else
                {
                    list.Add(listItem);
                }
            }

            if (property.PropertyType.IsArray)
            {
                property.SetValue(item, 
                    CreateArray(property.PropertyType, list));
            }
            else
            {
                property.SetValue(item, list);
            }
        }

        private void SetCustom(object item, PropertyInfo property, object value)
        {
            dynamic converter = ConverterCache.Get(property.PropertyType);

            if (converter != null)
            {
                object converted = converter.FromFirestore(value);
                property.SetValue(item, converted);            
            }
            else
            {
                property.SetValue(item, value);
            }
        }

        private object CreateArray(Type type, IList list)
        {
            Type elementType = type.GetElementType();

            Array array = Array.CreateInstance(elementType, list.Count);
            
            for (int i = 0; i < list.Count; i++)
                array.SetValue(list[i], i);

            return array;
        }
    }
}