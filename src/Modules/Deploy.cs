using System;
using System.Collections.Generic;
using ModuleWheels;
using KSP.Localization;

namespace KERBALISM
{
	public class Deploy : PartModule
	{
		[KSPField] public string type;                      // component name
		[KSPField] public double extra_Cost = 0;            // extra energy cost to keep the part active
		[KSPField] public double extra_Deploy = 0;          // extra eergy cost to do a deploy(animation)

		// Support Reliability
		[KSPField(isPersistant = true, guiName = "IsBroken", guiUnits = "", guiFormat = "")]
		public bool isBroken;                               // is it broken
		public bool lastBrokenState;                        // broken state has changed since last update?
		public bool lastFixedBrokenState;                   // broken state has changed since last fixed update?

		[KSPField(guiName = "EC Usage", guiUnits = "/s", guiFormat = "F3")]
		public double actualCost = 0;                       // Energy Consume

		// Vessel info
		public bool hasEnergy;                              // Check if vessel has energy, otherwise will disable animations and functions
		public bool isConsuming;                            // Module is consuming energy
		public bool hasEnergyChanged;                       // Energy state has changed since last update?
		public bool hasFixedEnergyChanged;                  // Energy state has changed since last fixed update?
		public Resource_info resources;

		public PartModule module;                           // component cache, the Reliability.cs is one to many, instead the Deploy will be one to one
		public KeyValuePair<bool, double> modReturn;        // Return from DeviceEC

		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios & do something only in Flight scenario
			if (Lib.DisableScenario(this) || !Lib.IsFlight()) return;

			// cache list of modules
			module = part.FindModulesImplementing<PartModule>().FindLast(k => k.moduleName == type);

			// get energy from cache
			resources = ResourceCache.Info(vessel, "ElectricCharge");
			hasEnergy = resources.amount > double.Epsilon;

			// Force the update to run at least once
			lastBrokenState = !isBroken;
			hasEnergyChanged = !hasEnergy;
			hasFixedEnergyChanged = !hasEnergy;

#if DEBUG
			// setup UI
			Fields["actualCost"].guiActive = true;
			Fields["isBroken"].guiActive = true;
#endif
		}

		public override void OnUpdate()
		{
			if (!Lib.IsFlight() || module == null) return;

			// get energy from cache
			resources = ResourceCache.Info(vessel, "ElectricCharge");
			hasEnergy = resources.amount > double.Epsilon;

			// Update UI only if hasEnergy has changed or if is broken state has changed
			if (isBroken)
			{
				if (isBroken != lastBrokenState)
				{
					lastBrokenState = isBroken;
					Update_UI(!isBroken);
				}
			}
			else if (hasEnergyChanged != hasEnergy)
			{
				Lib.Debug("Energy state has changed: {0}", hasEnergy);

				hasEnergyChanged = hasEnergy;
				lastBrokenState = false;
				// Update UI
				Update_UI(hasEnergy);
			}
			// Constantly Update UI for special modules
			if (isBroken) Constant_OnGUI(!isBroken);
			else Constant_OnGUI(hasEnergy);

			if (!hasEnergy || isBroken)
			{
				actualCost = 0;
				isConsuming = false;
			}
			else
			{
				isConsuming = GetIsConsuming();
			}
		}

		public virtual void FixedUpdate()
		{
			if (!Lib.IsFlight() || module == null) return;

			if (isBroken)
			{
				if (isBroken != lastFixedBrokenState)
				{
					lastFixedBrokenState = isBroken;
					FixModule(!isBroken);
				}
			}
			else if (hasFixedEnergyChanged != hasEnergy)
			{
				hasFixedEnergyChanged = hasEnergy;
				lastFixedBrokenState = false;
				// Update module
				FixModule(hasEnergy);
			}

			// If isConsuming
			if (isConsuming && resources != null) resources.Consume(actualCost * Kerbalism.elapsed_s);
		}

		public virtual bool GetIsConsuming()
		{
			try
			{
				switch (type)
				{
					case "ModuleWheelDeployment":
						modReturn = new LandingGearEC(module as ModuleWheelDeployment, extra_Deploy).GetConsume();
						actualCost = modReturn.Value;
						return modReturn.Key;
				}
			}
			catch (Exception e)
			{
				Lib.Log("'" + part.partInfo.title + "' : " + e.Message);
			}
			actualCost = extra_Deploy;
			return true;
		}

		public virtual void Update_UI(bool isEnabled)
		{
			try
			{
				switch (type)
				{
					case "ModuleWheelDeployment":
						new LandingGearEC(module as ModuleWheelDeployment, extra_Deploy).GUI_Update(isEnabled);
						break;
				}
			}
			catch (Exception e)
			{
				Lib.Log("'" + part.partInfo.title + "' : " + e.Message);
			}
		}

		public virtual void FixModule(bool isEnabled)
		{
			try
			{
				switch (type)
				{
					case "ModuleWheelDeployment":
						new LandingGearEC(module as ModuleWheelDeployment, extra_Deploy).FixModule(isEnabled);
						break;
				}
			}
			catch (Exception e)
			{
				Lib.Log("'" + part.partInfo.title + "' : " + e.Message);
			}
		}

		// Some modules need to constantly update the UI 
		public virtual void Constant_OnGUI(bool isEnabled)
		{
			try
			{
			}
			catch (Exception e)
			{
				Lib.Log("'" + part.partInfo.title + "' : " + e.Message);
			}
		}

		public void ToggleActions(PartModule partModule, bool value)
		{
			Lib.Debug("Part '{0}'.'{1}', setting actions to {2}", partModule.part.partInfo.title, partModule.moduleName, value ? "ON" : "OFF");
			foreach (BaseAction ac in partModule.Actions)
			{
				ac.active = value;
			}
		}

		public static void BackgroundUpdate(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, Deploy deploy, Resource_info ec, double elapsed_s)
		{
			if (deploy.isConsuming) ec.Consume(deploy.extra_Cost * elapsed_s);
		}
	}
}