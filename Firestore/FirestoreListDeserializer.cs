using System.Reflection;
using Google.Cloud.Firestore;
using System.Collections;

namespace GCP.Helpers.Firestore
{
    internal class FirestoreListDeserializer
    {
        public MethodInfo Method;
        public PropertyInfo Property;
        public object Instance;

        public IList CreateList()
        {
            return (IList) Activator.CreateInstance(Property.PropertyType);
        }

        public object Convert(object value)
        {
            return Method.Invoke(Instance, new object[] { value });                
        }

        private static Dictionary<Type, FirestoreListDeserializer> _listConverters =
            new Dictionary<Type, FirestoreListDeserializer>();

        public static FirestoreListDeserializer GetConverter(PropertyInfo property)
        {
            Type bclType = property.PropertyType.GenericTypeArguments.First();

            if (!_listConverters.ContainsKey(bclType))
            {
                Type generic = typeof(GenericFirestoreConverter<>);
                Type[] typeArgs = { bclType };
                Type constructed = generic.MakeGenericType(typeArgs);
                MethodInfo method = constructed.GetMethod("FromFirestore");
                var instance = Activator.CreateInstance(constructed);

                _listConverters.Add(bclType, 
                    new FirestoreListDeserializer { 
                        Method = method,
                        Property = property, 
                        Instance = instance }
                );
            }

            return _listConverters[bclType];
        }
    }
}