cd..
msbuild BuildSystemTargets.xml -v:m -target:BuildRelease -property:IsDevRelease=true
PAUSE