using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/*
NFE reactor partmodule :
- thermals
- core damage
- flow restriction

double
double heatproduced
double fuelamount
floatcurve cooldowncurve // residual heat production after the module is disabled
bool explodeOnOverheat

*/



namespace KERBALISM
{
	public class RadiatorFixer : PartModule
	{
		private ModuleActiveRadiator stockRadiatorModule;
		private ModuleDeployablePart deployModule;

		[KSPField]
		public bool useStockRadiatorConfig = true;

		[KSPField]
		public bool canBeDisabled = false;

		[KSPField(isPersistant = true)]
		public string inputResource = "ElectricCharge";

		[KSPField(isPersistant = true)]
		public double inputResourceRate = 1.0;

		[KSPField(isPersistant = true)]
		public string outputResource = "Coolant";

		/// <summary> output in kW </summary>
		[KSPField(isPersistant = true)]
		public double outputResourceRate = 1000.0;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Coolant pump")]
		public bool radiatorEnabled = true;

		public override void OnStart(StartState startState)
		{
			if (!PartResourceLibrary.Instance.resourceDefinitions.Contains(outputResource))
			{
				Lib.Log("WARNING : The output resource '" + outputResource  + "' doesn't exist, disabling RadiatorFixer module on '" + part.partInfo.title + "'..." );
				enabled = isEnabled = moduleIsEnabled = false;
				return;
			}


			foreach (PartModule pm in part.Modules)
			{
				if (pm is ModuleActiveRadiator) stockRadiatorModule = (ModuleActiveRadiator)pm;
				if (pm is ModuleDeployablePart) deployModule = (ModuleDeployablePart)pm;
			}

			if (useStockRadiatorConfig)
			{
				if (stockRadiatorModule == null)
				{
					Lib.Log("WARNING : Could not find a ModuleActiveRadiator on '" + part.partInfo.title + "', disabling RadiatorFixer module...");
					enabled = isEnabled = moduleIsEnabled = false;
					return;
				}

				// get max heat dissipation capacity in kW
				outputResourceRate = stockRadiatorModule.maxEnergyTransfer / 50.0; // KSP hardcoded value. Nice.

				// get consumed resource name and rate for max heat dissipation capacity
				ModuleResource input = resHandler.inputResources.FirstOrDefault();
				if (input != null)
				{
					inputResource = input.name;
					inputResourceRate = input.rate;
				}
			}

			Fields["radiatorEnabled"].guiActive = canBeDisabled;
		}

		public void Update()
		{
			if (!canBeDisabled && deployModule != null)
				radiatorEnabled = deployModule.deployState == ModuleDeployablePart.DeployState.EXTENDED;
		}

		public void FixedUpdate()
		{
			if (Lib.IsEditor()) return;

			if (!radiatorEnabled) return;

			if (inputResourceRate > 0.0)
			{
				Recipe recipe = new Recipe("radiator");
				recipe.AddInput(inputResource, inputResourceRate * Kerbalism.elapsed_s);
				recipe.AddOutput(outputResource, outputResourceRate * Kerbalism.elapsed_s, false);
				ResourceCache.AddRecipe(vessel, recipe);
			}
			else
			{
				ResourceCache.Produce(vessel, outputResource, outputResourceRate * Kerbalism.elapsed_s, "radiator");
			}
		}

		public static void BackgroundUpdate(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, RadiatorFixer prefab, double elapsed_s)
		{
			if (!Lib.Proto.GetBool(m, "radiatorEnabled")) return;

			if (prefab.inputResourceRate > 0.0)
			{
				Recipe recipe = new Recipe("radiator");
				recipe.AddInput(prefab.inputResource, prefab.inputResourceRate * elapsed_s);
				recipe.AddOutput(prefab.outputResource, prefab.outputResourceRate * elapsed_s, false);
				ResourceCache.AddRecipe(v, recipe);
			}
			else
			{
				ResourceCache.Produce(v, prefab.outputResource, prefab.outputResourceRate * elapsed_s, "radiator");
			}

		}
	}
}
