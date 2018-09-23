#!/bin/bash

# This script will setup your KSP installation folder to work with the visual studio project.

# Modify these to point to the installation directories of the KSP and Unity.
# These are the default locations if you used the installer.
MY_KSP_DIR=/Applications/KSP_osx
UNITY_DIR=/Applications/Unity

CURRENT_DIR=`pwd`

echo "export KSPDEVDIR=$MY_KSP_DIR" >> ~/.bash_profile
. ~/.bash_profile

cd $KSPDEVDIR
rm -rf KSP_x64_Data
mkdir -p KSP_x64_Data/Managed
cd KSP_x64_Data/Managed

ln -s $UNITY_DIR/Unity.app/Contents/Managed/* .
ln -s ../../KSP.app/Contents/Resources/Data/Managed/* . 2>/dev/null

cd $CURRENT_DIR
