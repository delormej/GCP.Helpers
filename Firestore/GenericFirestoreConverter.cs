using System.Reflection;
using Google.Cloud.Firestore;
using System.Collections;

namespace GcpHelpers.Firestore
{
    /// <summary>
    /// Implements a generic custom converter to handle default POCO serialization
    /// without explicit attribute decoration.
    /// <see ref="https://cloud.google.com/dotnet/docs/reference/Google.Cloud.Firestore/latest/datamodel#custom-converters">Custom Converters</see>
    /// </summary>
    public class GenericFirestoreConverter<T> 
            : IFirestoreConverter<T> where T : class, new()
    {
        private readonly PropertyInfo[] _properties;
        private readonly string _idProperty;
        // Expected attribute name for the required unique identifier.
        private const string FirestoreId = "id";
        private readonly ConverterRegistry _registry;

        /// <summary>
        /// Create a converter by specifing the unique identifier property name
        /// of the type to be converted.
        /// </summary>
        public GenericFirestoreConverter(string idProperty, 
            ConverterRegistry registry = null) : this()
        {
            if (registry != null)
                _registry = registry;
   
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

        public object ToFirestore(T value) 
        {
            var map = new Dictionary<string, object>();
            foreach(var p in _properties)
            {
                // Firestore expects an id property as unique identifier.
                if (p.Name == _idProperty)
                {
                    map.Add(FirestoreId, p.GetValue(value));
                }

                if (p.PropertyType == typeof(DateTime))
                {
                    var dateValue = p.GetValue(value) as DateTime?;
                    
                    if (dateValue == null)
                        dateValue = DateTime.MinValue;

                    // Firestore requires storing DateTime in UTC
                    map.Add(p.Name, dateValue?.ToUniversalTime());
                }
                else
                {
                    IFirestoreConverter<T> converter = GetConverter<T>(p);  
                    
                    if (converter != null)
                        map.Add(p.Name, converter.ToFirestore((T)p.GetValue(value)));
                    else
                        map.Add(p.Name, p.GetValue(value));
                }
            }
            return map;
        } 

        private IFirestoreConverter<P> GetConverter<P>(PropertyInfo p)
        {
            if (_registry == null)
                return null;

            foreach (var c in _registry)
            {
                var converter = c as IFirestoreConverter<P>;

                if (converter == null)
                    continue;

                if (converter.GetType().GenericTypeArguments[0].FullName ==
                    p.PropertyType.FullName)
                {
                    return converter;
                }
            }
            
            return null;
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
                    
                    if (property != null)
                        TrySetValue(item, property, pair.Value);
                }
            }

            return item;
        }

        private PropertyInfo[] GetProperties()
        {
            var properties = typeof(T).GetProperties(BindingFlags.Public|BindingFlags.Instance);
            // Only use properties that can be written to and read.
            return properties.Where(p => p.CanRead == true && p.CanWrite == true)
                .ToArray();
        }

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
                if (propertyType == typeof(DateTime))
                {
                    Timestamp obj = (Timestamp)value;
                    property.SetValue(item, obj.ToDateTime());
                }
                else if (propertyType.IsEnum)
                {
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
                else if (propertyType == typeof(Int32))
                {
                    // Firestore always deserlizes int as Int64.
                    property.SetValue(item, Convert.ToInt32(value));
                }
                else if (propertyType == typeof(string))
                {
                    property.SetValue(item, value.ToString());      
                }                         
                else if (value is IEnumerable<object> && propertyType.IsGenericType)
                {
                    // Generic Lists of child objects are not handled implicitly since we 
                    // are using a custom converter, so recursively create converters.
                    IList list = Helper.CreateGenericList(property, value);
                    property.SetValue(item, list);
                }
                else if (value is List<object> && propertyType.IsArray)
                {
                    Type generic = typeof(List<>);
                    Type[] typeArgs = { propertyType.GetElementType() };
                    Type constructed = generic.MakeGenericType(typeArgs);
                    
                    var instance = Activator.CreateInstance(constructed);
                    MethodInfo method = constructed.GetMethod("ToArray");

                    foreach (var o in (List<object>)value)
                    {
                        ((IList)instance).Add(o);
                    }

                    property.SetValue(item, method.Invoke(instance, null));
                }       
                else if (value is IDictionary<string, object>)
                {
                    property.SetValue(item, 
                        Helper.ConvertFromFirestore(property, value));
                }
                else 
                {
                    property.SetValue(item, value);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in TrySetValue {property.Name}: {e.Message}");
            }
        }
    }
}