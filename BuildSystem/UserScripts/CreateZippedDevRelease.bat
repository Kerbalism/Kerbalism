cd..
msbuild BuildSystemTargets.xml -v:m -target:CreateZippedRelease -property:IsDevRelease=true
PAUSE