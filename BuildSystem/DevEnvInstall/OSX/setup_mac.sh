#!/bin/bash

# This script will setup your KSP installation folder to work with the Kerbalism build system.
# First, you need to download and install Unity.
# Next, change the installation locations below and then run the script.


##########################
# INSTALLATION LOCATIONS #
##########################
KSP_DIR=/Applications/KSP_osx
UNITY_DIR=/Applications/Unity


############################
# NOTHING TO DO BELOW THIS #
############################

CURRENT_DIR=`pwd`

cd $KSP_DIR
#ln -s $UNITY_DIR/Unity.app/Contents/PlaybackEngines/MacStandaloneSupport/Variations/macosx64_development_mono/UnityPlayer.app KSP_Debug.app
rm -rf KSP_x64_Data
rm -f KSP_x64_Dbg_Data
mkdir -p KSP_x64_Data/Managed
ln -s KSP_x64_Data KSP_x64_Dbg_Data
cd KSP_x64_Data/Managed

ln -s $UNITY_DIR/Unity.app/Contents/Managed/* .
ln -s ../../KSP.app/Contents/Resources/Data/Managed/* . 2>/dev/null

cd $CURRENT_DIR
#cp PlayerConnectionConfigFile.dms $KSPDEVDIR/KSP_x64_Data
