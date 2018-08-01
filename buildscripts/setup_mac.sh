#!/bin/bash

# This script will setup your KSP installation folder to work with the visual studio project.

# Modify this variable to point to the installation directory of the KSP folder you want to use
# for development. This is the default location if you used the installer.
MY_KSP_DIR=/Applications/KSP_osx

echo "export KSPDEVDIR=$MY_KSP_DIR" >> ~/.bash_profile
. ~/.bash_profile

cd $KSPDEVDIR
mkdir -p KSP_x64_Data/Managed
cd KSP_x64_Data/Managed

ln -s ../../Launcher.app/Contents/Resources/Data/Managed/UnityEngine.dll .
ln -s ../../Launcher.app/Contents/Resources/Data/Managed/UnityEngine.UI.dll .
ln -s ../../KSP.app/Contents/Resources/Data/Managed/* . 2>/dev/null

cd $KSPDEVDIR
