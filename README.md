# GcpHelpers
[This repo](https://github.com/delormej/Gcp.Helpers) is a collection of helper objects *and tips* for working with the GCP [.NET Cloud Client Libraries](https://cloud.google.com/dotnet/docs/reference).  See [Getting started with .NET](https://cloud.google.com/dotnet/docs/getting-started) on Google Cloud to _get started_, these tools are useful for more advanced uses. 

## Authentication
... 

## Firestore
Firestore serialization in the [Google.Cloud.Firestore](https://cloud.google.com/dotnet/docs/reference/Google.Cloud.Firestore/latest) .NET library is already more extensive than in other languages.  As [documented in the Data model](https://cloud.google.com/dotnet/docs/reference/Google.Cloud.Firestore/latest/datamodel), in order for this serialization to work, you must decorate your objects with `[Firestore*]` properties as the examples show in the linked data model page.  

If you try to use your own POCO and do not modify it to have the Firestore attributes: 

```csharp
    var doc = _firestore.Collection(_usersCollection)
        .Document(id);
    var snapshot = await doc.GetSnapshotAsync();

    if (snapshot != null)
        return snapshot.ConvertTo<MyUserType>();
```

You will get an exception `System.ArgumentException: Unable to create converter for type MyUserType`.  

Personally putting implementation/provider specific attributes on my clean POCOs feels dirty to me.  Also, if you are trying to use an existing data model you don't want to change that model just for Firestore.  For example, you might want to have the flexibility to change providers; maybe use Mongo instead or another cloud database.  Thankfully, there's an _escape hatch_ for this; it's called a [Custom Converter](https://cloud.google.com/dotnet/docs/reference/Google.Cloud.Firestore/latest/datamodel#custom-converters).  After you build a custom converter you add it to the `ConverterRegistry` when creating a `FirestoreDbBuilder` as [shown here](https://cloud.google.com/dotnet/docs/reference/Google.Cloud.Firestore/latest/datamodel#converter-registries). 

The problem with this is that you need to create a converter for every type.  The default serialization behavior I want is actually the same for every one, and hence we should use Generics.  This repo implements a `GenericFirestoreConverter<T>` which is the default serialization I'd like to see and it _just works_ for all my POCOs.  You register is just like you would any other converter, except that it is a Generic and takes a type parameter of `<T>` for example:

```csharp
    _firestore = new FirestoreDbBuilder
    {
        ProjectId = config["ProjectId"],
        ConverterRegistry = new ConverterRegistry
        {
            new GenericFirestoreConverter<MyUserType>("UserId"),
            new GenericFirestoreConverter<MyAddressType>("AddressId"),
            ...
        }
    }
```

You will also note that the constructor has an _optional_ `string` parameter which is the name of the property that will be used as the `FirestoreDocumentId`.  

### Handling Collections
If your POCO has children, i.e. `MyUserType` has a member `Addresses` of type `IEnumerable<MyAddressType>`, this custom converter searches the `ConverterRegistry` and uses the converter you registered to handle deserialization recursively.  Interestingly, this is not necesary for serialization.

## Instance Metadata
...

## API Gateway / OpenAPI with Swagger
...

## Secret Manager
...

## Logging
Now included in the Beta version of the Google SDK.  To use add the folowing package to your `.csproj`

```xml
<PackageReference Include="Google.Cloud.Logging.Console" Version="1.0.0-beta01" />
```
Reference the package and add the formatter
```csharp
using Google.Cloud.Logging.Console;
...

builder.Logging.AddGoogleCloudConsole();
```

## nuget
This package is a public package available on [nuget](https://www.nuget.org/packages/GcpHelpers/), to use the package:

```bash
dotnet add package GcpHelpers 
```

For developers maintaining the package, publish using the included wrapper shell script:

```bash
./publish.sh
```

You will need to login to your [apikeys](https://www.nuget.org/account/apikeys) and get a key.  Save this in a file called `nuget-key` in the root directory of this project.
