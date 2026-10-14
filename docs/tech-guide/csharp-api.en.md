**This page is for coders who wish to make their PartModule or mod compatible with Kerbalism.**

# Current API

As of Kerbalism **3.40**, the public reflection API lives in [`src/Kerbalism/System/API.cs`](https://github.com/Kerbalism/Kerbalism/blob/master/src/Kerbalism/System/API.cs) (`KERBALISM.API`). There is a **single** `Kerbalism.dll` for KSP 1.12.x (multi-version bootstrap DLLs were removed). Prefer optional reflection so your mod still loads without Kerbalism.

Major areas exposed by `API`:

- Messages / `Kill` / `Breakdown` / `DisableKerbal` / `InjectRadiation`
- Environment: sunlight, breathable atmosphere
- Radiation fields, storms, blackout, belt/magnetopause visibility
- Reliability: malfunction / critical / broken / repair
- Habitat: volume, surface, pressure, poisoning, shielding, living space, comfort
- Resources: consume / produce / brokers / amounts / rates
- Science hooks (`preventScienceCrediting`, `onSubjectsReceived`, experiment state events)
- Comm / `AntennaInfo` handlers for custom communication mods

For background resource production on your own PartModules, implement Kerbalism’s module hooks (`IKerbalismModule` / background update pattern) described later on this page — do not rely on stock resource APIs for unloaded vessels.

# General API

## Checking if Kerbalism is present

```
using System;
using System.Reflection;

public class DetectKerbalism
{
	private static bool didScan = false;
	private static bool kerbalismFound = false;

	public static bool Found()
	{
		if (didScan)
			return kerbalismFound;

		foreach (var a in AssemblyLoader.loadedAssemblies)
		{
			// Match the assembly name from the csproj (not the file name).
			AssemblyName nameObject = new AssemblyName(a.assembly.FullName);
			if (nameObject.Name.Equals("Kerbalism"))
			{
				kerbalismFound = true;
				break;
			}
		}

		didScan = true;
		return kerbalismFound;
	}
}
```

## Importing API methods
This is an example of how you can import these methods to use in your plugin : 

```
public static class KerbalismAPI
{
	// delegate for the following API method (return value -> use Func) :
	// public static bool IsOuterBeltVisible(CelestialBody body)
	public static Func<CelestialBody, bool> IsOuterBeltVisible;

	// delegate for the following API method (void return value -> use Action) : 
	// public static void SetMagnetopauseVisible(CelestialBody body, bool visible)
	public static Action<CelestialBody, bool> SetMagnetopauseVisible;

	// You will need to call this method only once
	public static void Init()
	{
		Type apiType = Type.GetType("KERBALISM.API");
		IsOuterBeltVisible = (Func<CelestialBody, bool>)Delegate.CreateDelegate(typeof(Func<CelestialBody, bool>), apiType.GetMethod("IsOuterBeltVisible"));
		SetMagnetopauseVisible = (Action<CelestialBody, bool>)Delegate.CreateDelegate(typeof(Action<CelestialBody, bool>), apiType.GetMethod("SetMagnetopauseVisible"));
	}

	// Then from your code, call these delegate like static methods :
	// bool outerBeltVisible = KerbalismAPI.IsOuterBeltVisible(FlightGlobals.currentMainBody);
	// KerbalismAPI.SetMagnetopauseVisible(FlightGlobals.currentMainBody, true);
}
```

# PartModule Resource system API

## Limitations and stability considerations

The Kerbalism resource simulation **doesn't work like the stock system**. Every production/consumption call is deferred and processed vessel-wide at the end of the frame, unlike the stock simulation executing each request immediately. There are a few implications :
- The stock `double RequestResource(string name, double request)` method has no equivalent in Kerbalism, in the sense that you **cannot** know the returned effective consumption / production. Kerbalism has internal systems for those use cases, but they aren't exposed through the API.
- All consumption / production is vessel-wide using a similar behavior as the stock `ALL_VESSEL_BALANCED` flow mode, but the stock flow modes / priorities / crossfeed restrictions are entirely ignored. Only the part local `FlowState` is honored.

Also, if you are using the following pattern : 
```
if (resourceAmount > 0.0)
{
  ConsumeResource();
  DoThings();
}
```
Aside from not working correctly at high timewarp rates (independently of using it with the stock or Kerbalism simulation), that pattern will destabilize the whole simulation when the resource is input-starved.

## Consuming resources at a fixed rate

In many cases, it is not necessary to write any code to make mods Kerbalism aware, a lot of it can be done with configuration files. For instance, consuming resources at a constant rate, or at a rate depending on an [environment condition Kerbalism knows about](profile.md), can be done by writing a ModuleManager config file that adds a [ProcessController](part-modules/processcontroller.md) part module to the part.

## Producing resources

Part modules that generate resources will possibly inhibit time warping at high speeds - unless the resource is generated using the kerbalism resource system. The reason is that KSP handles resources poorly at high time warp, and with a little bit of bad luck this could quickly result in unexpected (and unwarranted) death of kerbals. This is why we implemented a separate resource system that works for time warp - and, most importantly, works on unloaded vessels, too.

## Consuming resources at dynamic rates

If you need to be dynamic with resource consumption, like using resources at rates that depend on a setting in your part module, you will need to write a bit of code.

## Planner support

Kerbalism comes with a planner that displays estimated resource consumption and production rates. For that to work, it will look at all part modules on the vessel in the editor and sum up all resource production and consumption rates.

If you add the following function, the Kerbalism planner will call it via reflection every time it updates the planner window. Expect to be called a few times every time the vessel changes in the editor, or after `GameEvents.onEditorShipModified` was triggered.

Add this method to your part module:

```
/ <summary>
/// This will be called by Kerbalism in the editor (VAB/SPH), possibly several times after a change to the vessel.
///
/// The Kerbalism Planner allows to select different situations and bodies, and will update the simulated environment accordingly. This simulated
/// environment is passed into this method:
///
/// - body: the currently selected body
/// - environment: a string to double dictionary, currently containing:
///   - altitude: the altitude of the vessel above the body
///   - orbital_period: the duration of a circular equitorial orbit at the given altitude
///   - shadow_period: the duration of that orbit that will be in the planets shadow
///   - albedo_flux
///   - solar_flux
///   - sun_dist: distance to the sun
///   - temperature
///   - total_flux
/// </summary>
/// <param name="resources">A list of resource names and production/consumption rates.
/// Production is a positive rate, consumption is negatvie. Add all resources your module is going to produce/consume.</param>
/// <param name="body">The currently selected body in the Kerbalism planner</param>
/// <param name="environment">Environment variables guesstimated by Kerbalism, based on the current selection of body and vessel situation. See above.</param>
/// <returns>The title to display in the tooltip of the planner UI.</returns>
public string PlannerUpdate(List<KeyValuePair<string, double>> resources, CelestialBody body, Dictionary<string, double> environment)
{
	if (running)
	{
		// consume the resource if running
		resources.Add(new KeyValuePair<string, double>("ElectricCharge", -0.5));
	}
	return title;
}
```

`List<KeyValuePair<string, double>> resources` will be empty, your module should add every resource consumed or produced. Entries in the list are pairs of internal resource names and a flow rate in unts per second. Production rates are postivie, consumption rates are negative (f.i. `resources.Add(new KeyValuePair<string, double>("ElectricCharge", -0.1));`)

`CelestialBody body` will be the body currently selected in the planner

`Dictionary<string, double> environment` is a key-value pair, filled with environment variables that are estimated based on the currently selected situation. Currently, we have:
* altitude: 0 if landed
* orbital_period: duration of a full circular equitorial orbit at the given altitude. If altitude is 0, this will be the body rotation period.
* shadow_period: duration of orbital_period that will be spent on the night side
* shadow_time: proportion of orbit that is in shadow
* albedo_flux: solar flux reflected from the body
* solar_flux: flux received from the sun (considering atmospheric absorption)
* sun_dist: distance from the sun
* temperature: vessel temperature
* total_flux: total flux at vessel position
* sunlight: 1 when in sunlight, 0 when in shadow

`return` a short string that indicates what the resource is used for. Something like "comms" or "terraformer", will appear in the planner tooltip when you hover the mouse over the resource.

## Background simulation

While your vessel is unloaded, Kerbalism will continue to simulate all processes that are happening on that vessel. However, since unloaded vessels are treated very differently by KSP (namely: not at all), most of what you normally could access from within a part module won't be available at runtime. This starts with the part module itself.

During background simulation, Kebalism will call a static function on part modules of unloaded vessels.

The method must have exactly this signature:

```
public static string BackgroundUpdate(Vessel v,
	ProtoPartSnapshot part_snapshot, ProtoPartModuleSnapshot module_snapshot,
	PartModule proto_part_module, Part proto_part,
	Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest,
	double elapsed_s)
{
    // replicate part module behavior here
}
```

`vessel` will always be unloaded.

`proto_part` and `proto_module` are _proto snapshots_ of your part and your part module. Proto snapshots contain the persisted state, which means all variables that are declared with `[KSPField(isPersistant = true)]`. You can read and write those values using `Proto.GetBool` and `Proto.Set` methods from the helper class (see below).

`part_module` and `part` are _prototypes_ of your part and your part module. You can read all values that are declared with `[KSPField]` - and _only those_, not the persisted values (see above). Other members will not be initialized as your part module will not have gone through a full life cycle. The prototype _part_ has gone through an `Awake()` and an `OnLoad()` call during the LOADING game phase, and you can reference non-KSPField values IF you initialized them at that time. However, all values are from the LOADING scene and may have no bearing on the current scene/environment/settings.

`elapsed_s` is the number of game-time seconds that passed since the last time the vessel was updated. Kerbalism will update only one unloaded vessel per update, so depending on the amount of vessels and especially the current time warp speed, this can be a very long time. You have to expect to be called only once every couple of days of game-time.

During background update, the stock resource interface for that vessel does not exist because the vessel will remain unloaded. To manipulate resources on the vessel, you absolutely must use the Kerbalism interface to do so. There is a helper class implementation available that contains the code needed to access proto  modules or the Kerbalism API without introducing a dependency, you can [get the latest version from here](https://github.com/Kerbalism/KerbalismContracts/blob/master/src/KerbalismContracts/Modules/KerbalismUtils.cs).

Example implementation using KerbalismUtils (contains Proto):

```
/// <summary>
/// We're always going to call you for resource handling.  You tell us what to produce or consume.  Here's how it'll look when your vessel is NOT loaded
/// </summary>
/// <param name="v">the vessel (unloaded)</param>
/// <param name="part_snapshot">proto part snapshot (contains all non-persistant KSPFields)</param>
/// <param name="module_snapshot">proto part module snapshot (contains all non-persistant KSPFields)</param>
/// <param name="proto_part_module">proto part module snapshot (contains all non-persistant KSPFields)</param>
/// <param name="proto_part">proto part snapshot (contains all non-persistant KSPFields)</param>
/// <param name="availableResources">key-value pair containing all available resources and their currently available amount on the vessel. if the resource is not in there, it's not available</param>
/// <param name="resourceChangeRequest">key-value pair that contains the resource names and the units per second that you want to produce/consume (produce: positive, consume: negative)</param>
/// <param name="elapsed_s">how much time elapsed since the last time. note this can be very long, minutes and hours depending on warp speed</param>
/// <returns>the title to be displayed in the resource tooltip</returns>
public static string BackgroundUpdate(Vessel v,
	ProtoPartSnapshot part_snapshot, ProtoPartModuleSnapshot module_snapshot,
	PartModule proto_part_module, Part proto_part,
	Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest,
	double elapsed_s)
{
	KerbalismContractEquipment module = proto_part_module as KerbalismContractEquipment;

	bool running = Proto.GetBool(module_snapshot, "running", false);
	if (running)
	{
		double ec = 0;
		availableResources.TryGetValue("ElectricCharge", out ec);
		if(ec <= 0) do_stuff_when_there_is_no_ec_available();

		// tell Kerbalism to use 0.5 EC/s
		resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -0.5));
	}

	return "my module"; // be short, this needs to fit in a tiny tooltip
}
```

## Resources on loaded vessels

It's not an absolute must, but if you can please use the Kerbalism resource system on loaded vessels, too.

Example:

```
/// <summary>
/// We're also always going to call you when you're loaded.  Since you're loaded, this will be your PartModule, just like you'd expect in KSP. Will only be called while in flight, not in the editor
/// </summary>
/// <param name="availableResources">key-value pair containing all available resources and their currently available amount on the vessel. if the resource is not in there, it's not available</param>
/// <param name="resourceChangeRequest">key-value pair that contains the resource names and the units per second that you want to produce/consume (produce: positive, consume: negative)</param>
/// <returns></returns>
public virtual string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
{
	kerbalismDetected = true; // use this to disable the stock resource consumption

	if (running)
	{
		double ec = 0;
		availableResources.TryGetValue(resourceName, out ec);
		if(ec <= 0) do_stuff_when_there_is_no_ec_available();
		// tell Kerbalism to consume 0.5 EC/s
		resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -0.5));
	}

	return "my module"; // be short, this needs to fit in a tiny tooltip
}
```
