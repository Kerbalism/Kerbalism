#!/bin/bash
MAJOR=$(cat $WORKSPACE/GameData/$JOB_NAME/$JOB_NAME.version | jq '.VERSION.MAJOR')
MINOR=$(cat $WORKSPACE/GameData/$JOB_NAME/$JOB_NAME.version | jq '.VERSION.MINOR')
PATCH=$(cat $WORKSPACE/GameData/$JOB_NAME/$JOB_NAME.version | jq '.VERSION.PATCH')
BUILD=$(cat $WORKSPACE/GameData/$JOB_NAME/$JOB_NAME.version | jq '.VERSION.BUILD')
export MOD_VERSION=$MAJOR.$MINOR.$PATCH.$BUILD
echo "Mod version: $MOD_VERSION"
sed -i -E 's/Assembly(\w*)Version\("[0-9\*\.]+"\)/Assembly\1Version("'"$MOD_VERSION"'")/' $WORKSPACE/src/Properties/AssemblyInfo.cs