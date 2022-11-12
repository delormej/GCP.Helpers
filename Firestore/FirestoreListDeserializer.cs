using System.Reflection;
using Google.Cloud.Firestore;
using System.Collections;
using System.Collections.Concurrent;

namespace GcpHelpers.Firestore
{
    internal class FirestoreListDeserializer
    {
        public MethodInfo Method;
        public PropertyInfo Property;
        public object Instance;
        private static readonly object _listLock = new();
        private static readonly ConcurrentDictionary<Type, FirestoreListDeserializer> 
            _listConverters = new();

        public IList CreateList()
        {
            return (IList) Activator.CreateInstance(Property.PropertyType);
        }

        public object Convert(object value)
        {
            return Method.Invoke(Instance, new object[] { value });                
        }

        public static FirestoreListDeserializer GetConverter(PropertyInfo property)
        {
            lock(_listLock)
            {
                Type bclType = property.PropertyType.GenericTypeArguments.First();

                if (!_listConverters.ContainsKey(bclType))
                {
                    object instance = Helper.CreateGenericFirestoreConverter(bclType, 
                        "FromFirestore", out MethodInfo method);

                    var deserializer = new FirestoreListDeserializer { 
                            Method = method,
                            Property = property, 
                            Instance = instance };

                    _listConverters.AddOrUpdate(bclType, deserializer, (_,_) => deserializer);

                    return deserializer;
                }
                else
                {
                    return _listConverters[bclType];
                }
            }
        }
    }
}