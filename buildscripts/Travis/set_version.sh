#!/bin/sh

# Update version in AssemblyInfo.cs and *.version files.

ASSEMBLY=$TRAVIS_BUILD_DIR/src/Kerbalism/Properties/AssemblyInfo.cs
VERSIONFILE=$TRAVIS_BUILD_DIR/GameData/Kerbalism/Kerbalism.version

if [ ! -f $ASSEMBLY ]; then
	echo $ASSEMBLY does not exist
	return
fi

if [ ! -f $VERSIONFILE ]; then
	echo $VERSIONFILE does not exist
	return
fi

if [ -z "$1" ]; then
	MOD_VERSION=`grep "^\[assembly: AssemblyVersion" $ASSEMBLY | sed -e 's/.*("//' -e 's/".*//'`
	while true; do
	    read -p "Current version: $MOD_VERSION. New Version: " version
	    read -p "New version will be $version. proceed? [yn] " yn
	    case $yn in
		[Yy]* ) break;;
		[Nn]* ) return;;
		* ) echo "Please answer yes or no.";;
	    esac
	done
else
	version=$1
fi

export MOD_VERSION=$version

sed -i .bak -E 's/Assembly(.*)Version\(".*"\)/Assembly\1Version("'"$MOD_VERSION"'")/' $ASSEMBLY
rm $ASSEMBLY.bak

MAJOR=`echo $MOD_VERSION | awk -F "." '{ print $1 }'`
MINOR=`echo $MOD_VERSION | awk -F "." '{ print $2 }'`
PATCH=`echo $MOD_VERSION | awk -F "." '{ print $3 }'`
BUILD=`echo $MOD_VERSION | awk -F "." '{ print $4 }'`
if [ -z "$BUILD" ]; then
	BUILD=0
fi

sed -i .bak -E 's/"VERSION".*/"VERSION": {"MAJOR": '"$MAJOR"', "MINOR": '"$MINOR"', "PATCH": '"$PATCH"', "BUILD": '"$BUILD"'},/' $VERSIONFILE
rm $VERSIONFILE.bak
