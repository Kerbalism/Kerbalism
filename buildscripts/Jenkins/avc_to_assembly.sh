#!/bin/bash
MAJOR=$(cat $WORKSPACE/GameData/$JOB_NAME/$JOB_NAME.version | jq '.VERSION.MAJOR')
MINOR=$(cat $WORKSPACE/GameData/$JOB_NAME/$JOB_NAME.version | jq '.VERSION.MINOR')
PATCH=$(cat $WORKSPACE/GameData/$JOB_NAME/$JOB_NAME.version | jq '.VERSION.PATCH')
jq --argjson build $BUILD_NUMBER '.VERSION.BUILD=$build' $WORKSPACE/GameData/Kerbalism/Kerbalism.version > version.tmp
rm $WORKSPACE/GameData/Kerbalism/Kerbalism.version
mv version.tmp $WORKSPACE/GameData/Kerbalism/Kerbalism.version
export MOD_VERSION=$MAJOR.$MINOR.$PATCH.$BUILD_NUMBER
echo "Mod version: $MOD_VERSION"
sed -i -E 's/Assembly(\w*)Version\("[0-9\*\.]+"\)/Assembly\1Version("'"$MOD_VERSION"'")/' $WORKSPACE/src/Properties/AssemblyInfo.cs
