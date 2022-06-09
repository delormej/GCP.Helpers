#!/bin/sh
version=1.0.1
echo 'Publishing version $version' to nuget.org...

export nugetkey=`cat nuget-key` 

dotnet nuget push ./bin/Debug/GcpHelpers.$version.nupkg --api-key $nugetkey --source https://api.nuget.org/v3/index.json

echo 'Done'