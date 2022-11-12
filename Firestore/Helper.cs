using System.Reflection;
using System.Collections;

namespace GcpHelpers.Firestore;

internal class Helper 
{
    internal static object CreateGenericFirestoreConverter(Type bclType, string methodName, out MethodInfo method)
    {
        Type generic = typeof(GenericFirestoreConverter<>);
        Type[] typeArgs = { bclType };
        Type constructed = generic.MakeGenericType(typeArgs);
        method = constructed.GetMethod(methodName);
        var instance = Activator.CreateInstance(constructed);

        return instance;
    }  

    internal static object ConvertFromFirestore(PropertyInfo property, object value)
    {
        var converter = Helper.CreateGenericFirestoreConverter(property.PropertyType, 
            "FromFirestore", out MethodInfo method);

        if (converter != null && method !=null)
        {
            return method.Invoke(converter, new object[] { value });
        }
        else
        {
            throw new ApplicationException(
                $"Unable to create a GenericFirestoreConverter<{property.PropertyType.Name}> for {property.Name}");
        }            
    }

    /// <summary>
    /// Supports recursive instantiation of this GenericFirestoreConverter for child items
    /// which are generically returned from Firestore in an enumerable list.
    /// </summary>
    internal static IList CreateGenericList(PropertyInfo property, object value)
    {
        var values = (IEnumerable<object>)value;

        try 
        {
            var converter = FirestoreListDeserializer.GetConverter(property);
            var list = converter.CreateList();

            foreach (var item in values) 
            {
                var result = converter.Convert(item);
                list.Add(result);
            }

            return list;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error serializaing {property.Name}: {e.Message}");
            return null;
        }          
    }
}