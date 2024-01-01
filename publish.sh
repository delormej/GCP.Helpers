#!/bin/sh
version=1.3.5

dotnet build --configuration release -o ./bin/release/net6.0/
dotnet pack --configuration release

echo 'Publishing version $version' to nuget.org...

export nugetfile=nuget-key

if [ ! -f $nugetfile ]; then
    echo "Please ensure you have a nuget API key stored in a file named $nugetfile"
    exit 1
fi

export nugetkey=`cat $nugetfile` 

#ls -l ./bin/release/GcpHelpers.$version.nupkg
dotnet nuget push ./bin/release/GcpHelpers.$version.nupkg --api-key $nugetkey --source https://api.nuget.org/v3/index.json

echo 'Done'
