#!/bin/bash
cat $WORKSPACE/GameData/*/*.version | jq '.VERSION.MAJOR' | read MAJOR
cat $WORKSPACE/GameData/*/*.version | jq '.VERSION.MINOR' | read MINOR
cat $WORKSPACE/GameData/*/*.version | jq '.VERSION.PATCH' | read PATCH
cat $WORKSPACE/GameData/*/*.version | jq '.VERSION.BUILD' | read BUILD
export MOD_VERSION=$MAJOR.$MINOR.$PATCH.$BUILD