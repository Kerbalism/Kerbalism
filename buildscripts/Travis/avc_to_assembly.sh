#!/bin/bash
MAJOR=$(cat $TRAVIS_BUILD_DIR/GameData/Kerbalism/Kerbalism.version | jq '.VERSION.MAJOR')
MINOR=$(cat $TRAVIS_BUILD_DIR/GameData/Kerbalism/Kerbalism.version | jq '.VERSION.MINOR')
PATCH=$(cat $TRAVIS_BUILD_DIR/GameData/Kerbalism/Kerbalism.version | jq '.VERSION.PATCH')
export MOD_VERSION=$MAJOR.$MINOR.$PATCH
echo "Mod version: $MOD_VERSION"
sed -i -E 's/Assembly(\w*)Version\("[0-9\*\.]+"\)/Assembly\1Version("'"$MOD_VERSION"'")/' $TRAVIS_BUILD_DIR/src/Kerbalism/Properties/AssemblyInfo.cs


