#!/bin/bash
# use "sh make_zip_release.sh -q" to disable the version confirm prompt

if ! test -e "Kerbalism.sln"; then 
	cd .. 
	cd .. 
fi
if ! test -e "Kerbalism.sln"; then
	echo "This script must be run from the Kerbalism repository root"
	exit
fi

#constants
OUTPUT_PATH="buildscripts/MakeRelease/Output"
CORE_PATH="GameData/Kerbalism"
CONFIG_PATH="GameData/KerbalismConfig"
VERSION_PATH="$CORE_PATH/Kerbalism.version"
CORE_NAME="Kerbalism-Core" 
CONFIG_NAME="Kerbalism-Config"

if ! test -d "$OUTPUT_PATH"; then mkdir "$OUTPUT_PATH"; fi

echo "-------------------------------------------------------"
echo "Parsing $VERSION_PATH metadata..."
echo "-------------------------------------------------------"
VERSION_ALL=$(grep VERSION "$VERSION_PATH")
echo "$VERSION_ALL"
MOD_VERSION_INFO=$(echo $VERSION_ALL | grep '"VERSION"')
MOD_MAJOR=$(echo $MOD_VERSION_INFO | cut -d':' -f 3 | grep -oEi '[0-9]')
MOD_MINOR=$(echo $MOD_VERSION_INFO | cut -d':' -f 4 | grep -oEi '[0-9]')
MOD_PATCH=$(echo $MOD_VERSION_INFO | cut -d':' -f 5 | grep -oEi '[0-9]')
MOD_VERSION="$MOD_MAJOR.$MOD_MINOR.$MOD_PATCH"
echo "Detected Kerbalism version : $MOD_VERSION"
echo "-------------------------------------------------------"

if ! echo $1 | grep -q "q"; then
    while true; do
		read -p "Please verifiy that the version information is correct [y/n]" yn
		case $yn in
			[Yy]* ) break;;
			[Nn]* ) exit;;
			* ) echo "Please answer yes or no."
			;;
		esac
	done
	echo "-------------------------------------------------------"
fi

CORE_ZIP="$OUTPUT_PATH/$CORE_NAME-V$MOD_VERSION.zip" 
CONFIG_ZIP="$OUTPUT_PATH/$CONFIG_NAME-V$MOD_VERSION.zip"

if test -e "$CORE_ZIP"; then rm $CORE_ZIP; fi
if test -e "$CONFIG_ZIP"; then rm $CONFIG_ZIP; fi

zip -r "$CORE_ZIP" "$CORE_PATH" -q
echo "$CORE_ZIP created..."

echo "Copying Kerbalism.version file to KerbalismConfig (CKAN support)..."
cp "$VERSION_PATH" "$CONFIG_PATH/Kerbalism.version"

zip -r "$CONFIG_ZIP" "$CONFIG_PATH" -q
echo "$CONFIG_ZIP created..."

echo "Deleting Kerbalism.version copy..."
rm "$CONFIG_PATH/Kerbalism.version"
echo "-------------------------------------------------------"