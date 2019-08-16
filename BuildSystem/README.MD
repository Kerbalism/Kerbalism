# How to build Kerbalism and setup your IDE

Kerbalism uses a custom build and deployment system. 
From your IDE, don't try to change the project references and don't change anything in the project properties, 
that will break the build system.

## Supported IDE / C# versions

You will need **Visual Studio 2017** or later on Windows or an IDE that work with **Mono 6.0** or later on Linux/Mac.

We currently target the C# version 7.1.

## Quick setup guide

Before you can build Kerbalism, your IDE has to know where the Unity and KSP assemblies are, 
and what version of KSP you are using. 

This is done by creating a configuration file :

  - Close your IDE (Visual Studio, Monodevelop...)
  - In your file explorer, go to the `BuildSystem` folder
  - Make a copy of the `UserConfigDevEnv.xml.CopyMe` file and rename it to `UserConfigDevEnv.xml`
  - Edit this file in a text editor and follow the instructions in the comments
  - Open your IDE and make sure that you are in the **Debug** configuration
  - Make sure the active / start project is **KerbalismBuild** (should be in bold in Visual Studio)
  - Hit the "Start" button. This should trigger a build and run KSP with the just built dlls.

Notes :

  - It is recommended to only build the whole solution, don't build the individual projects
  - Building the solution will copy the dlls (and on Windows, the *.mdb files) to your KSP installation, no need to do it manually.
  - The generated dlls are located in `BuildSystem\BinariesDebug`
  - After editing the `UserConfigDevEnv.xml`, **always close and reload the solution** in your IDE. 
    The "reload" option in VS won't work. Not doing it will result in weird things happening.

The release configuration that generate the *.kbin files require a special setup, 
ask us first if you really need to build it.

## OSX

To build on OSX, you need to download and install Unity first. Then look into setup_mac.sh and change the installation
locations of Unity and KSP. Once you're sure the locations suit your needs, run setup_mac.sh (from any folder). It will
create a folder with symbolic links to the (hopefully) correct DLL files you need to build Kerbalism on OSX.

## I want to use the command line !

**On Windows** MSBuild is installed by default with Visual Studio. 
Note that to get MSBuild, you don't need to install the full IDE, 
there is a standalone package available in the Visual Studio download manager / installer. 

To find the path to MSBuild.exe, use the `BuildSystem\UserScripts\FindMSBuild.bat`
Add the folder that contains it to your `PATH` environment variable to be able to use the `msbuild` command directly.

**On Linux/Mac** the `msbuild` command is available if you have installed Mono.

**Basic usage :**

Build the debug configuration : 

`msbuild Kerbalism.sln  -v:m`

Build the release configuration : 

`msbuild Kerbalism.sln  -v:m -property:Configuration=Release`

Build a specific target : 

`msbuild BuildSystemTargets.xml -v:m -target:TargetName -property:prop1=value1;prop2=value2`

`-v:m` is short for `-verbosity:minimal` Recommended to be able to keep track of the Kerbalism build system messages, 
but you can remove it if needed.

## How does it work ?

The Kerbalism build system rely on the MSBuild functionality to provide easy cross-platform deployment.

The MSBuild engine behavior is changed trough **XML configuration files**. 
You are already familiar with these files if you ever edited a `*.csproj` file, 
which use the "default" MSBuild configuration for C# projects.

While using a data structure syntax, MSbuild XML files are used like a script language. Quick overview :
 - **`Property`** : string variables. Defined in a `PropertyGroup` and evaluated with the `$(property)` syntax.
 - **`Item`** : array/list-like string variable structure. Each item can have additional `metadata`. 
   Defined in a `ItemGroup` and evaluated with the `@(item)` or `%(item)` syntax.
 - **`Target`** : targets can be considered like independant scripts, 
   they usually contain a list of `Tasks` that are executed sequentially. 
   Targets can be called directly from the command line with the `-target:` argument.
 - **`Task`** are method-like functions. They can have inputs and ouput parameters. 
   There is a set of predefined tasks available, but you can also define your own with inline C# code.

**Important notions :**
  - `targets` run in the context of the file they are defined in, they have access 
    to all `properties` and `items`, excepted those defined in other `targets`
  - Even outside `targets`, the file is "executed" sequentially. 
    Assigning a value to a `property` twice will result in the `property` 
    having the first value until the second assignation.

# Build System documentation

## Targets

There are a handfull of targets defined in `BuildSystemTargets.xml` :
  - Build targets : compile the code and create binaries (*.dll files)
  - Deployment targets : package and upload releases
  - Utility targets : specialized targets for performing common actions

### Build targets

#### `BuildRelease` (and `KBinGenerator`)

**`msbuild BuildSystemTargets.xml -v:m -target:BuildRelease -property:[see below]`**

[**`IsDevRelease`**=`true`/`false`, default `false`] : if true, will compile the release with the "DEVBUILD" constant, 
making the dev build number appear in the Kerbalism UI. The build number is autogenerated by the compiler when 
KerbalismBootstrap is built (because it's assemblyInfo version use the wilcard "X.X.*" format)

This target will build Kerbalism and KerbalismBootstrap in the release configuration :
- Clean/create the `BuildSystem\BinariesRelease` folder
- Update the `AssemblyInfo.cs` files to match the Kerbalism version defined in `Gamedata\Kerbalism\VersionConfig.xml`
- Update the `Kerbalism.version` AVC file to match the Kerbalism version defined in `Gamedata\Kerbalism\VersionConfig.xml`
- Extract the KSP Dll from the 7z archive (it uses the lastest supported version for that)
- Build `KerbalismBootstrap` in release configuration using those Dlls
- For each `KBinVersionConstant` item in `Gamedata\Kerbalism\VersionConfig.xml`, run the `KBinGenerator` target :
  - Create a temporary folder for the kbin build
  - Extract the corresponding KSP Dlls defined in the `KSPBuildVersion` metadata entry
  - Build Kerbalism with the corresponding version constant
  - Copy and rename `Kerbalism.dll` to `Kerbalism<KBinVersionConstant>.kbin`
  
To generate the kbin files for each KSP version, it generate Kerbalism with a matching 
set of reference Dlls stored in password-protected archives in `BuildSystem\References\KSPReferenceZips`.
  
To extract these archives, you need to correctly set the `ContributorConfigFolder` property in your `UserConfigDevEnv.xml` file
and the `KSPDllArchivesPassword` property in your `UserConfigRelease.xml`
  
As an alternative, you can pass the `KSPDllArchivesPassword` property to the target from the command line.

#### `BuildDebug`

**`msbuild BuildSystemTargets.xml -v:m -target:BuildDebug -property:[see below]`**

[**`DefineConstants`**=semicolon separated values, default to what is defined in `UserConfigDevEnv.xml`] Compilation constants

This target will build Kerbalism and KerbalismBootstrap in the debug configuration :
- Clean/create the `BuildSystem\BinariesDebug` folder
- Build `KerbalismBootstrap` in debug configuration :
  - Using the KSP dlls from the KSP installation defined in `UserConfigDevEnv.xml`
  - With the `DEBUG` and `TRACE` constants
  - With the KSPXX version constant defined in `UserConfigDevEnv.xml`
  - With the additional debug constants defined in `UserConfigDevEnv.xml`
- On windows only, it will generate the *.mdb debug symbols to use with the *Visual Studio Tools for Unity* debugger
- If the `CopyGameDataOnBuild` is set to `true` in the `UserConfigDevEnv.xml` file, it will copy 
  the whole Kerbalism\KerbalismConfig folders from the repository to your KSP installation GameData.
- It will copy Kerbalism.dll, KerbalismBootstrap.dll and the *.mdb files to your KSP installation GameData.

### Deployement targets

#### `CreateZippedRelease`

**`msbuild BuildSystemTargets.xml -v:m -target:CreateZippedRelease -property:[see below]`**

[**`IsDevRelease`**=`true`/`false`, default `false`] : will pass the same property the `BuildRelease` target

Create zipped releases in the `BuildSystem\DeploymentOutput` folder :
- Create 2 zips, one containing `GameData\Kerbalism`, the other `GameData\KerbalismConfig`
- Format the zip name as follow : `[Kerbalism-Core/Kerbalism-Config]_[Version]_for_KSP[VersionMin]-[VersionMax]`
- [Version] will be formatted as `dev-[BuildNumber]` if this is a dev build
- Copy `KerbalismBootstrap.dll` and the `*.kbins` files in the `Kerbalism-Core` zip
- Copy the repo `GameData\Kerbalism\Kerbalism.version` to the `Kerbalism-Config` zip, in `GameData\KerbalismConfig`
  Why : The Kerbalism.version file is also needed in the KerbalismConfig zip because these 2 zips are separate mods on CKAN
- Copy the README.MD and CHANGELOG.MD files at the root of each zip

#### `UploadGitHubDevRelease`

**`msbuild BuildSystemTargets.xml -v:m -target:UploadGitHubDevRelease -property:[see below]`**

[**`GithubOAuthToken`**=string, **required**] GitHub OAuth Token for your github account, to generate it, 
go to your guthub account, then `Settings > Developer settings > Personal access tokens`
To avoid having to put this every time, it can be defined in `UserConfigRelease.xml`, 
whose path is defined in `UserConfigDevEnv.xml`
  
[**`PushVersionCommit`**=`true`/`false`, default `true`] : by default, this target will push an empty
commit to the active branch with the name of the release. This is used by this script to find 
the previous release and generate the commit changelog. Set to false to prevent this commit
from being created.
  
[**`DraftRelease`**=`true`/`false`, default `false`] : if true, create a release with the "draft" 
status on GitHub. In this case, the `PushVersionCommit` paroperty will be forced to `false`.
  
[**`LastVersionCommitGrep`**=string, default : `DevBuild.*BuildSystem`) : git-grep style regular 
expression used to find the commit corresponding to the previous version and generate
the changelog history. You can change this if you want to generate the changelog history
from a specific commit (must match the text of the commit).

This target build and zip a dev release, prompt for confirmation and upload it to the "DevBuilds" GitHub repo releases.

It use a custom task class for the calls to the GitHub API (rely on API V3). 
The code can be found in `BuildSystem\Utility\UploadGitHubRelease.cs`

As this rely on a commit for version tagging, **make sure your are synchronized with the remote branch** before using it,
or use `PushVersionCommit=false`

### Utility targets

#### `UpdateVersionFile`

**`msbuild BuildSystemTargets.xml -v:m -target:UpdateVersionFile`**

Update the `GameData\Kerbalism\kerbalism.version` file to match the version defined in `GameData\Kerbalism\VersionConfig.xml`

#### `UpdateAssembliesVersion`

**`msbuild BuildSystemTargets.xml -v:m -target:UpdateAssembliesVersion`**

Update the `assemblyInfo.cs` files in the Kerbalism and KerbalismBootstrap projects to match the version defined in `GameData\Kerbalism\VersionConfig.xml`

#### `ExtractKSPDlls`

**`msbuild BuildSystemTargets.xml -v:m -target:ExtractKSPDlls -property:[see below]`**

[**`KSPVersion`**=X.X.X, **required**] KSP version property formated `X.X.X` matching the 7zip archive name.

Extract the KSP dlls from the 7zip stored in `BuildSystem\References\KSPReferenceZips` to the `BuildSystem\References\KSPReferenceDlls` folder

#### `CreateKSPDllProtectedZip`

**`msbuild BuildSystemTargets.xml -v:m -target:CreateKSPDllProtectedZip -property:[see below]`**

[**`KSPVersion`**=X.X.X, **required**] KSP version you want to create an archive for

[**`KSPDllPath`**=full path, **required**] Absolute path to folder containing the KSP dlls

Create a password-protected 7zip archive named KSP-`KSPVersion`.7z from the specified 
KSP installation and copy it to `BuildSystem\References\KSPReferenceZips`

## Build system technical documentation

#### `Kerbalism.sln`

The solution file is configured to make `KerbalismBuild` the default project and the only one that is generated when MSBuild is called on the solution.

#### The `KerbalismBuild` project 

Defined in `src\Kerbalism\KerbalismBuild.csproj`, this empty project is a "wrapper" for the build system, 
making it available from your IDE and from the `Kerbalism.sln` file.

Specifically, this project allow you to :
  - Use the build functionality from your IDE, by calling the `BuildRelease` or `BuildDebug` custom targets from `BuildSystemTargets.xml`.
  - Run KSP from the "start" button of your IDE, using the executable defined in `UserConfigDevEnv.xml`.

#### `Kerbalism.csproj` and `KerbalismBootstrap.csproj`

The project files are mostly untouched and they import the default `Microsoft.CSharp.targets` file. 

Notable differences from a "vanilla" csproj files :
  - Common properties (platform, .NET and C# versions...) are imported from `ProjectsCommonProperties.xml`
  - The `OutputPath` and `IntermediateOutputPath` are imported from `ProjectsCommonProperties.xml`
  - The `DefineConstants` (compilation constants) are imported from `UserConfigDevEnv.xml`
  - For the debug configuration, references path is imported from `UserConfigDevEnv.xml` (Installed KSP dlls)
  - For the release configuration, references path is imported from `ProjectsCommonProperties.xml` (Zipped dlls)

#### `BuildSystem\ProjectsCommonProperties.xml`

This file is imported by `Kerbalism.csproj` and `KerbalismBootstrap.csproj`, it contains 
properties and paths that are common to both projects.

#### `BuildSystem\UserConfigDevEnv.xml`

This file is imported by `Kerbalism.csproj`, `KerbalismBootstrap.csproj`, `KerbalismBuild.csproj` and `BuildSystemTargets.xml`.
It contains user specific properties (KSP installation path and version, compilation constants...)

This file is git-ignored and is available to copy as a `*.CopyMe` template

#### `BuildSystem\UserConfigRelease.xml`

Contains KSP dll archives password and upload locations credentials.

This file is only needed to build release versions of Kerbalism and to upload releases.

It is git-ignored but it is highly recommended to put it outside the repository to make sure it is never committed
The path to this file must be set in `UserConfigDevEnv.xml`

#### `BuildSystem\BuildSystemTargets.xml`

This file is the core of the build system, it contain the global `properties`, all the `targets` and the custom C# `tasks`.

It can be called from the msbuild command line directly to use the targets.



#### `BuildSystem\Utility`

This folder contain dependencies of the build system, mostly command line executable for windows.

Some C# tasks defined in `BuildSystemTargets.xml` are defined in external `.cs` files, they are also stored here.
