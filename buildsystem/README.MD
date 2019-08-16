## How to build Kerbalism and setup your IDE

Kerbalism uses a custom build and deployment system. From your IDE, don't try to change the project references and don't change anything in the project properties, that won't work.

#### Supported IDE / C# versions

You will need **Visual Studio 2017** or later on Windows or an IDE that work with **Mono 6.0** or later on Linux/Mac.

We currently target the C# version 7.1.

#### Quick setup guide

Before you can build Kerbalism, your IDE has to know where the Unity and KSP assemblies are, and what version of KSP you are using. 

This is done by creating a configuration file :

  - Close your IDE (Visual Studio, Monodevelop)
  - In you file explorer, go to the **BuildSystem** folder
  - Make a copy of the **UserConfigDevEnv.xml.CopyMe** file and rename it to **UserConfigDevEnv.xml**
  - Open this file in a text editor and follow the instructions in the comments.
  - Save the file
  - Open you IDE and make sure you are in the **Debug** configuration
  - Make sure the active / default project is **KerbalismBuild** (should be in bold in Visual Studio)
  - It is recommended to only build the whole solution, don't build the indivdual projects
  - Building the solution will copy the dlls to your KSP installation, no need to do it manually.
  - The generated dlls are located in **BuildSystem\BinariesDebug**
  
The release configuration that generate the *.kbin files require a special setup, ask us first if you really need to build it.

#### How to build from the command line

You can build from the command line with :

`msbuild Kerbalism.sln -verbosity:minimal`

## Build System documentation

Soon™