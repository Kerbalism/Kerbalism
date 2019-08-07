#!/bin/bash

# Increment the build number and set versions.

ASSEMBLY=$TRAVIS_BUILD_DIR/src/Kerbalism/Properties/AssemblyInfo.cs

MOD_VERSION=`grep "^\[assembly: AssemblyVersion" $ASSEMBLY | sed -e 's/.*("//' -e 's/".*//'`

MAJOR=`echo $MOD_VERSION | awk -F "." '{ print $1 }'`
MINOR=`echo $MOD_VERSION | awk -F "." '{ print $2 }'`
PATCH=`echo $MOD_VERSION | awk -F "." '{ print $3 }'`
BUILD=`echo $MOD_VERSION | awk -F "." '{ print $4 }'`

if [ -z "$BUILD" ]; then
	BUILD=0
fi

BUILD=$((BUILD+1))

export MOD_VERSION=$MAJOR.$MINOR.$PATCH.$BUILD
echo "Mod version: $MOD_VERSION"

buildscripts/Travis/set_version.sh $MOD_VERSION
