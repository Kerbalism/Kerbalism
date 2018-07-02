if [ -f $TRAVIS_BUILD_DIR/buildscripts/Patches/$KSPVER.patch ]; then
	cd $TRAVIS_BUILD_DIR
	git apply "$TRAVIS_BUILD_DIR/buildscripts/Patches/$KSPVER.patch"
else
	echo "No patch found for KSP $KSPVER. Continuing."
fi