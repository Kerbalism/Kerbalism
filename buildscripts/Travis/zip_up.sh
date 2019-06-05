#!/bin/bash
export BASE_DIR=${TRAVIS_BUILD_DIR:-$PWD}
cd $BASE_DIR
rm "Kerbalism-Core.zip"
rm "Kerbalism-Config.zip"
zip -r "Kerbalism-Core.zip" "GameData/Kerbalism"
zip -r "Kerbalism-Config.zip" "GameData/KerbalismConfig"
