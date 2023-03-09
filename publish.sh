#!/bin/sh
version=1.0.13

dotnet build
dotnet pack

echo 'Publishing version $version' to nuget.org...

export nugetfile=nuget-key

if [ ! -f $nugetfile ]; then
    echo "Please ensure you have a nuget API key stored in a file named $nugetfile"
    exit 1
fi

export nugetkey=`cat $nugetfile` 

dotnet nuget push ./bin/Debug/GcpHelpers.$version.nupkg --api-key $nugetkey --source https://api.nuget.org/v3/index.json

echo 'Done'
