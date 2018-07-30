#!/bin/sh
# KSP AVC .version file editor [PiezPiedPy]

echo Setting $2 for KSP$1


# Split passed version
[[ $1 =~ (([[:digit:]]+)\.([[:digit:]]+)\.([[:digit:]]+)) ]]



# Injections for KSP 1.3.1
if [ ${BASH_REMATCH[3]} = 3 ] 
then

	sed -i "s/\"KSP_VERSION\": {\"MAJOR\": ., \"MINOR\": ., \"PATCH\": .}/\"KSP_VERSION\": {\"MAJOR\": 1, \"MINOR\": 3, \"PATCH\": 1}/g" $2
	sed -i "s/\"KSP_VERSION_MIN\": {\"MAJOR\": ., \"MINOR\": ., \"PATCH\": .}/\"KSP_VERSION_MIN\": {\"MAJOR\": 1, \"MINOR\": 3, \"PATCH\": 1}/g" $2
	sed -i "s/\"KSP_VERSION_MAX\": {\"MAJOR\": ., \"MINOR\": ., \"PATCH\": .}/\"KSP_VERSION_MAX\": {\"MAJOR\": 1, \"MINOR\": 3, \"PATCH\": 1}/g" $2



# Injections for KSP x.x.x
else

	sed -i "s/\"KSP_VERSION\": {\"MAJOR\": ., \"MINOR\": ., \"PATCH\": .}/\"KSP_VERSION\": {\"MAJOR\": ${BASH_REMATCH[2]}, \"MINOR\": ${BASH_REMATCH[3]}, \"PATCH\": ${BASH_REMATCH[4]}}/g" $2

	sed -i "s/\"KSP_VERSION_MIN\": {\"MAJOR\": ., \"MINOR\": ., \"PATCH\": .}/\"KSP_VERSION_MIN\": {\"MAJOR\": ${BASH_REMATCH[2]}, \"MINOR\": ${BASH_REMATCH[3]}, \"PATCH\": 0}/g" $2

	sed -i "s/\"KSP_VERSION_MAX\": {\"MAJOR\": ., \"MINOR\": ., \"PATCH\": .}/\"KSP_VERSION_MAX\": {\"MAJOR\": ${BASH_REMATCH[2]}, \"MINOR\": ${BASH_REMATCH[3]}, \"PATCH\": ${BASH_REMATCH[4]}}/g" $2

fi
