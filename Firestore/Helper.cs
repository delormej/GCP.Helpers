using System.Reflection;

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
}