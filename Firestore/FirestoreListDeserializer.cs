using System.Collections;

namespace GcpHelpers.Firestore
{
    internal class FirestoreListDeserializer
    {
        public static IList CreateGenericList(Type propertyType)
        {
            if (propertyType.IsGenericType)
            {
                if (propertyType.GenericTypeArguments.Count() < 1)
                    throw new ApplicationException($"Unable to create list, no generic type argument for {propertyType.Name}.");

                Type type = propertyType.GenericTypeArguments.First();

                return InternalCreateGenericList(type);
            }
            else
            {
                return InternalCreateGenericList(propertyType);
            }
        }

        static IList InternalCreateGenericList(Type type)
        {
            var listType = typeof(List<>);
            var constructedListType = listType.MakeGenericType(type);

            return (IList) Activator.CreateInstance(constructedListType);
        }
    }
}