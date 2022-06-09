#!/bin/sh
version=1.0.1
echo 'Publishing version $version' to nuget.org...

export nugetkey=`cat nuget-key` 

pushd
cd bin/debug/
dotnet nuget push GcpHelpers.$version.nupkg --api-key $nugetkey --source https://api.nuget.org/v3/index.json
popd

echo 'Done'