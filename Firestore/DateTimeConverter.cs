using Google.Cloud.Firestore;

namespace GcpHelpers.Firestore;

/// <summary>
/// As not to conflict with Google.Cloud.Firestore built-in DateTime conversion
/// this is marked as internal and should not be used outside of GenericFirestoreConverter.
/// GenericFirestoreConverter cannot use the Google.Cloud.Firestore built-in
/// conversion because that implicit behavior is not exposed by the SDK so we
/// need to build our own duplciate here.
/// </summary>
internal class DateTimeConverter : IFirestoreConverter<DateTime?>
{
    public object ToFirestore(DateTime? source) 
    {
        var dateValue = source as DateTime?;

        if (dateValue == null)
            dateValue = DateTime.MinValue;

        // Firestore requires storing DateTime in UTC
        return dateValue?.ToUniversalTime();
    }

    public DateTime? FromFirestore(object value)
    {
        if (value == null)
            return DateTime.MinValue;
            
        Timestamp timestamp = (Timestamp)value;
        return timestamp.ToDateTime();
    }
}