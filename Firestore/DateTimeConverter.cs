using Google.Cloud.Firestore;

namespace GcpHelpers.Firestore;

public class DateTimeConverter : IFirestoreConverter<DateTime>
{
    public object ToFirestore(DateTime source) 
    {
        var dateValue = source as DateTime?;

        if (dateValue == null)
            dateValue = DateTime.MinValue;

        // Firestore requires storing DateTime in UTC
        return dateValue?.ToUniversalTime();
    }

    public DateTime FromFirestore(object value)
    {
        if (value == null)
            return DateTime.MinValue;
            
        Timestamp timestamp = (Timestamp)value;
        return timestamp.ToDateTime();
    }
}