# Nest.Jackdaw

Jackdaw is part of Nest's tools collection for self-hosted Unity CI built around the C# framework. Jackdaw is the user interface permitting to use Nest's functionalities directly inside the Unity Editor.

## Features

- Authentification to Nest.Hawk server (auto-connect/remember-me options stored as Editor User Preferences)
- Prepare and store multiple build configurations
- Send a build configuration to the connected Nest.Hawk server to ask for a build to be made
- Define a Nest.Hawk server addresse in project settings

### Planned

- Issue a specific revision of the project to be switched into before building (SVN first, git may be added after)
- Requesting Unity Tests to be launched with specific configurations (Edit Mode / Test Mode / some Assemblies only / etc...)
- Requesting builded files deployment to specified destination : local machine, Steam's server, Itch.io's server, etc...
- Show the realtime progress of a requested action (build/test/deployement)

## Requirement

Too use this tool you'll need an instance of Nest.Hawk running on a machine (can be the same one) and at least one worker with a Nest.Quelea instance running (should be another machine as it will try to launch the Unity project again).

## Installation

Jackdaw comes as a Unity Package installed with UPM.

### Install via Git URL (Recommended)

You need to add this line inside your project `/Packages/manifest.json` file.

``` json
{
	"dependencies": {
		...
		"com.studioblackflag.nest.jackdaw" : "https://github.com/Makorj/Nest.Jackdaw#master",
		...
	}
}
```

### Install via unitypackage

You can download the latest package in the Release section of this repository and install it inside your project as usual with .unitypackage.