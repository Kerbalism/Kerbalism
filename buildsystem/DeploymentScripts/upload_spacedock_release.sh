#!/bin/bash

WORKING_PATH="$PWD"
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
CORE_NAME="Kerbalism-Core" 
CONFIG_NAME="Kerbalism-Config"
SPACEDOCK_NAME="Spacedock-Release"
SPACEDOCK_MOD_ID="1774"
VERSION_PATH="GameData/Kerbalism/Kerbalism.version"
PATH_TO_README="README.md"
PATH_TO_CHANGELOG="CHANGELOG.md"
RELEASE_SCRIPT="buildscripts/MakeRelease/make_zip_release.sh"

KSP_VERSION_INFO=$(grep '"KSP_VERSION"' "$VERSION_PATH")
KSP_MAJOR=$(echo $KSP_VERSION_INFO | cut -d':' -f 3 | grep -oEi '[0-9]*')
KSP_MINOR=$(echo $KSP_VERSION_INFO | cut -d':' -f 4 | grep -oEi '[0-9]*')
KSP_PATCH=$(echo $KSP_VERSION_INFO | cut -d':' -f 5 | grep -oEi '[0-9]*')
KSP_VERSION="$KSP_MAJOR.$KSP_MINOR.$KSP_PATCH"

MOD_VERSION_INFO=$(grep '"VERSION"' "$VERSION_PATH")
MOD_MAJOR=$(echo $MOD_VERSION_INFO | cut -d':' -f 3 | grep -oEi '[0-9]')
MOD_MINOR=$(echo $MOD_VERSION_INFO | cut -d':' -f 4 | grep -oEi '[0-9]')
MOD_PATCH=$(echo $MOD_VERSION_INFO | cut -d':' -f 5 | grep -oEi '[0-9]')
MOD_VERSION="$MOD_MAJOR.$MOD_MINOR.$MOD_PATCH"

CORE_ZIP="$CORE_NAME-V$MOD_VERSION.zip" 
CONFIG_ZIP="$CONFIG_NAME-V$MOD_VERSION.zip"
SPACEDOCK_ZIP="$SPACEDOCK_NAME-V$MOD_VERSION.zip"
SPACEDOCK_ZIP_PATH="$OUTPUT_PATH/$SPACEDOCK_ZIP"

echo "-------------------------------------------------------"

if ! test -d "$OUTPUT_PATH"; then mkdir "$OUTPUT_PATH"; fi
rm -r $OUTPUT_PATH/*

echo "Building release zips..."
sh $RELEASE_SCRIPT -q

echo "Building spacedock release zip..."
cp $PATH_TO_README $OUTPUT_PATH
cp $PATH_TO_CHANGELOG $OUTPUT_PATH
WORKING_PATH="$PWD"
cd "$PWD/$OUTPUT_PATH"

if ! (test -e "$CORE_ZIP" && test -e "$CONFIG_ZIP"); then
	echo "Error : release zips not created"
	exit
fi

if ! (test -e "$PATH_TO_README"); then
	echo "Error : README not found"
	exit
fi

if ! (test -e "$PATH_TO_CHANGELOG"); then
	echo "Error : CHANGELOG not found"
	exit
fi

zip -r "$SPACEDOCK_ZIP" "$CORE_ZIP" 
zip -r "$SPACEDOCK_ZIP" "$CONFIG_ZIP"
zip -r "$SPACEDOCK_ZIP" "$PATH_TO_README"
zip -r "$SPACEDOCK_ZIP" "$PATH_TO_CHANGELOG"

rm $PATH_TO_README
rm $PATH_TO_CHANGELOG

cd $WORKING_PATH

echo "-------------------------------------------------------"
echo "CHECK THE DETECTED VERSIONS :"
echo "KSP version : $KSP_VERSION"
echo "Kerbalism version : $MOD_VERSION"
echo "-------------------------------------------------------"
read -p "Accept and upload the release to spacedock [y/n]?" yn
case $yn in
	[Yy]* )
		echo "-------------------------------------------------------"
		echo "Enter spacedock account credentials :"
		COOKIE_PATH="$OUTPUT_PATH/spacedockcookie"
		if test -e "$OUTPUT_PATH/cookies"; then rm -Rf "$COOKIE_PATH"; fi
		while true; do
			read -p "login:" SPACEDOCK_LOGIN
			read -p "password:" SPACEDOCK_PASS
			
			if (curl -F username="$SPACEDOCK_LOGIN" -F password="$SPACEDOCK_PASS" -c "$COOKIE_PATH" "https://spacedock.info/api/login" -s) | grep -q "false";
			then
				echo "Login successfull"
				break
			else
				read -p "Login failed. do you want to retry [y/n]?" yn
				case $yn in
					[Nn]* ) exit;;
				esac
			fi
		done
		echo "-------------------------------------------------------"
		echo "Uploading..."
		curl -b "$COOKIE_PATH" \
			-F "version=$MOD_VERSION" \
			-F "changelog=https://github.com/Kerbalism/Kerbalism/blob/master/CHANGELOG.md" \
			-F "game-version=$KSP_VERSION" \
			-F "notify-followers=no" \
			-F "zipball=@$SPACEDOCK_ZIP_PATH" \
			"https://spacedock.info/api/mod/$SPACEDOCK_MOD_ID/update"
		echo "  "
		echo "Upload finished."
esac
echo "Cleaning temp files..."
rm $COOKIE_PATH
rm $OUTPUT_PATH/$CORE_ZIP
rm $OUTPUT_PATH/$CONFIG_ZIP
echo "-------------------------------------------------------"