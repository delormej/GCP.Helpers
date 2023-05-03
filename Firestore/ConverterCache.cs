using Google.Cloud.Firestore;
using System.Reflection;

namespace GcpHelpers.Firestore;

public class ConverterCache
{
    private readonly static Dictionary<Type, object> Registry = new();

    public static void Add(Type type, object converter)
    {
        Registry.TryAdd(type, converter);
    }

    public static void Add(object converter)
    {
        Type type = converter.GetType();
        if (type.IsGenericType)
        {
            Type generic = type.GenericTypeArguments.First();
            Registry.TryAdd(generic, converter);    
        }
        else
        {
            throw new ApplicationException($"Unable to determine generic type for converter {converter.GetType().FullName}");
        }
    }

    public static dynamic Get(Type type)
    {
        Registry.TryGetValue(type, out dynamic value);
        return value;
    } 

    public static ConverterRegistry CreateRegistry()
    {
        var registry = new ConverterRegistry();
        
        foreach (var converter in Registry)
        {
            var method = GetGenericAdd(converter);
            method.Invoke(registry, new[] { converter.Value });        
        }

        AddDefaultConverters();

        return registry;
    }    

    private static void AddDefaultConverters()
    {
        Registry.Add(typeof(DateTime), new DateTimeConverter());
    }

    private static MethodInfo GetGenericAdd(KeyValuePair<Type, object> converter)
    {
        Type type = converter.Key;

        // if (converter.Value.GetType().IsGenericType)

        var addMethod = typeof(ConverterRegistry).GetMethod(nameof(ConverterRegistry.Add));
        var genericAddMethod = addMethod.MakeGenericMethod(type);
        
        return genericAddMethod;
    }
}