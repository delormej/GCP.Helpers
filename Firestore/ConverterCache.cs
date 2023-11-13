using Google.Cloud.Firestore;
using System.Reflection;
using Microsoft.Extensions.Logging;

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

        // Do not return these with the ConverterRegistry as they will 
        // conflict with the built-in Google.Cloud.Firestore converters
        AddDefaultConverters();

        return registry;
    }

    public static void AddConverterLogger(ILogger log)
    {
        foreach (var value in Registry.Values)
        {
            var converter = value as IConverterSupportsLog;

            if (converter != null)
                converter.Log = log;
        }
    }

    private static void AddDefaultConverters()
    {
        // If this is returned with ConverterRegistry an endless
        // recursive call chain will eventually stack overflow.
        Registry.Add(typeof(DateTime), new DateTimeConverter());
        Registry.Add(typeof(DateTime?), new DateTimeConverter());
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