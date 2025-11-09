using System;
using System.Collections.Generic;
using UnityEngine;
using static KERBALISM.Habitat;

namespace KERBALISM
{
	public class SunShieldingPartData
	{
		public double distance = 1.0;
		public double thickness = 1.0;

		public SunShieldingPartData(double distance, double thickness)
		{
			this.distance = distance;
			this.thickness = thickness;
		}

		public SunShieldingPartData(ConfigNode node)
		{
			distance = Lib.ConfigValue<double>(node, "distance", 1.0);
			thickness = Lib.ConfigValue<double>(node, "thickness", 1.0);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("distance", distance);
			node.AddValue("thickness", thickness);
		}
	}

	public class VesselHabitatInfo
	{
		private enum ObjectState { Uninitialized, Loaded, Unloaded  };

		public Dictionary<uint, List<SunShieldingPartData>> habitatShieldings = new Dictionary<uint, List<SunShieldingPartData>>();
		private int habitatIndex = -1;

		private ObjectState state = ObjectState.Uninitialized;
		private List<HabitatWrapper> habitatWrappers;

		public bool Ready => state != ObjectState.Uninitialized;

		/// <summary> Enabled habitats volume in m^3</summary>
		public double HabTotalVolume { get; private set; }
		/// <summary> Enabled habitats surface in m^2</summary>
		public double HabTotalSurface { get; private set; }
		/// <summary> Enabled habitats average normalized pressure </summary>
		public double HabNormalizedPressure { get; private set; }
		/// <summary> Enabled habitats average wasteAtmo (CO2) level</summary>
		public double HabPoisoning { get; private set; }
		/// <summary> Enabled habitats average radiation shielding level</summary>
		public double HabShieldingFactor { get; private set; }
		/// <summary> Amount of kerbals on the vessel</summary>
		public double CrewCount { get; private set; }
		/// <summary> Volume per crew normalized against an "ideal" value and clamped in an acceptable range</summary>
		public double HabLivingSpace { get; private set; }
		/// <summary> Enabled habitats volume per kerbal in m^3</summary>
		public double HabVolumePerCrew { get; private set; }

		public VesselHabitatInfo(ConfigNode node)
		{
			if (node == null)
				return;

			if (!node.HasNode("sspi")) return;
			foreach (var n in node.GetNode("sspi").GetNodes())
			{
				uint partId = uint.Parse(n.name);
				List<SunShieldingPartData> sspiList = new List<SunShieldingPartData>();
				foreach (var p in n.GetNodes())
				{
					sspiList.Add(new SunShieldingPartData(p));
				}
				habitatShieldings[partId] = sspiList;
			}
		}

		internal void Reset()
		{
            state = ObjectState.Uninitialized;
			habitatIndex = -1;
			habitatWrappers?.Clear();
			habitatShieldings?.Clear();
        }

		internal void Save(ConfigNode node)
		{
			var sspiNode = node.AddNode("sspi");
			foreach (var entry in habitatShieldings)
			{
				var n = sspiNode.AddNode(entry.Key.ToString());
				for (var i = 0; i < entry.Value.Count; i++)
				{
					entry.Value[i].Save(n.AddNode(i.ToString()));
				}
			}
		}

		internal void Update(Vessel vessel, VesselData vd, double elapsedSeconds)
		{
			if (vessel == null)
				return;

			if (vessel.loaded)
			{
				if (state != ObjectState.Loaded)
				{
					habitatWrappers = new List<HabitatWrapper>();
					for (int i = vessel.parts.Count; i-- > 0;)
					{
						PartModuleList modules = vessel.parts[i].Modules;
						for (int j = modules.Count; j-- > 0;)
						{
							if (modules[j] is Habitat habitat)
							{
								// we can hit edge cases where this runs while the hab resources don't exist yet.
								// Just try again latter, the default values should be safe.
								if (!LoadedHabitat.TryCreate(habitat, out LoadedHabitat wrapper))
									return;

								habitatWrappers.Add(wrapper);
								break;
							}
						}
					}

					state = ObjectState.Loaded;
				}
			}
			else if (state != ObjectState.Unloaded)
			{
				habitatWrappers = new List<HabitatWrapper>();
				List<Background.BackgroundPM> backgroundPMs = Background.Background_PMs(vessel);
				for (int i = backgroundPMs.Count; i-- > 0;)
				{
					if (backgroundPMs[i].type == Background.Module_type.Habitat)
					{
						Background.BackgroundPM bgHab = backgroundPMs[i];
						HabitatWrapper hab = new UnloadedHabitat(bgHab.p, bgHab.m, (Habitat)bgHab.module_prefab);
						habitatWrappers.Add(hab);
					}
				}

				state = ObjectState.Unloaded;
			}

			if (state == ObjectState.Uninitialized || !vd.IsSimulated)
				return;

			if (habitatWrappers.Count > 0)
			{
				HabitatsUpdate(vessel, vd, elapsedSeconds);
				if (state == ObjectState.Loaded && Features.Radiation)
					HabitatsRadiationUpdate(vessel);
			}
		}

		private static List<HabitatWrapper> pressurizingHabs = new List<HabitatWrapper>();

		internal void HabitatsUpdate(Vessel vessel, VesselData vd, double elapsedSeconds)
		{
			double atmoLeaksPerSurface;
			if (Profile.atmoLeaksRate > 0.0 && !vd.EnvBreathable)
				atmoLeaksPerSurface = Profile.atmoLeaksRate * elapsedSeconds;
			else
				atmoLeaksPerSurface = 0.0;

			double enaAtmoAmount = 0.0, enaAtmoCapacity = 0.0;
			double enaWasteAmount = 0.0, enaWasteCapacity = 0.0;
			double preAtmoAmount = 0.0, preAtmoCapacity = 0.0;
			double npAtmoCapacity = 0.0;

			// first loop : gather amounts / capacities
			for (int i = habitatWrappers.Count; i-- > 0;)
			{
				HabitatWrapper hab = habitatWrappers[i];
				switch (hab.State)
				{
					case State.enabled:
					case State.evaKerbal:
						if (hab.NonPressurizable)
						{
							npAtmoCapacity += hab.AtmoResource.MaxAmount;
						}
						else
						{
							enaAtmoAmount += hab.AtmoResource.Amount;
							enaAtmoCapacity += hab.AtmoResource.MaxAmount;
							enaWasteAmount += hab.WasteAtmoResource.Amount;
							enaWasteCapacity += hab.WasteAtmoResource.MaxAmount;
						}
						break;
					case State.disabled:
						// apply atmo leaks on disabled habitats
						if (atmoLeaksPerSurface > 0.0)
						{
							double atmoAmount = hab.AtmoResource.Amount;
							if (atmoAmount > 0.0)
								hab.AtmoResource.Amount = Math.Max(0.0, atmoAmount - (atmoLeaksPerSurface * hab.Surface));
						}
						break;
					case State.inflatingAndEqualizing:
					case State.waitingForPressureAndEqualizing:
						pressurizingHabs.Add(hab);
						break;
				}
			}

			// equalization, doesn't need to run if no atmo
			if (enaAtmoAmount != 0.0 || enaWasteAmount != 0.0)
			{
				// compute how much atmo is transferred from enabled habs toward pressurizing habs
				double preAtmoNeeded = 0.0;
				double preAtmoTransferred = 0.0;
				if (pressurizingHabs.Count > 0)
				{
					double enaAtmoLevel = enaAtmoAmount / enaAtmoCapacity;
					for (int i = pressurizingHabs.Count; i-- > 0;)
					{
						HabitatWrapper preHab = pressurizingHabs[i];
						double amount = preHab.AtmoResource.Amount;
						double maxAmount = preHab.AtmoResource.MaxAmount;
						double level = maxAmount > 0.0 ? amount / maxAmount : 0.0;
						// exclude pressurizing habs whose pressure level is higher than the enabled habs pressure level
						if (level > enaAtmoLevel)
						{
							pressurizingHabs.RemoveAt(i);
							continue;
						}
						preAtmoAmount += amount;
						preAtmoCapacity += maxAmount;
					}

					if (pressurizingHabs.Count > 0)
					{
						double equAtmoLevel = preAtmoAmount / preAtmoCapacity;
						// rate is EqualizationRateFactor (% / second), scaled down by pressure difference
						preAtmoTransferred = (enaAtmoLevel - equAtmoLevel) * preAtmoCapacity * Settings.EqualizationRateFactor * elapsedSeconds;
						// clamp by what is actually needed
						preAtmoNeeded = Math.Max(0.0, (preAtmoCapacity * Settings.PressureThreshold) - preAtmoAmount);
						preAtmoTransferred = Lib.Clamp(preAtmoTransferred, 0.0, preAtmoNeeded);
						// remove it from enabled habs
						enaAtmoAmount -= preAtmoTransferred;
					}
				}

				// if more than one enabled hab, equalize atmo/waste amongst them
				if (habitatWrappers.Count > 1)
				{
					for (int i = habitatWrappers.Count; i-- > 0;)
					{
						HabitatWrapper hab = habitatWrappers[i];
						if ((hab.State == State.enabled || hab.State == State.evaKerbal) && !hab.NonPressurizable)
						{
							hab.AtmoResource.Amount = enaAtmoAmount * (hab.AtmoResource.MaxAmount / enaAtmoCapacity);
							hab.WasteAtmoResource.Amount = enaWasteAmount * (hab.WasteAtmoResource.MaxAmount / enaWasteCapacity);
						}
					}
				}

				// distribute transferred atmo toward pressurizing habs
				if (preAtmoTransferred > 0.0 && preAtmoNeeded > 0.0)
				{
					for (int i = pressurizingHabs.Count; i-- > 0;)
					{
						HabitatWrapper preHab = pressurizingHabs[i];
						double needed = Math.Max(0.0, (preHab.AtmoResource.MaxAmount * Settings.PressureThreshold) - preHab.AtmoResource.Amount);
						preHab.AtmoResource.Amount += preAtmoTransferred * (needed / preAtmoNeeded);
					}
				}
			}

			pressurizingHabs.Clear();

			// all resources have been updated, call the state update on unloaded vessels
			if (state == ObjectState.Unloaded)
				for (int i = habitatWrappers.Count; i-- > 0;)
					BackgroundUpdate(habitatWrappers[i]);

			// compute vessel-wide habitat stats
			ResourceInfo vesselShieldingRes = ResourceCache.GetResource(vessel, ShieldingResName);
			double totAtmoCapacity = enaAtmoCapacity + npAtmoCapacity;
			HabTotalVolume = totAtmoCapacity / 1e3;
			HabTotalSurface = vesselShieldingRes.Capacity;
			HabNormalizedPressure = totAtmoCapacity > 0.0 ? enaAtmoAmount / totAtmoCapacity : 0.0;
			HabPoisoning = HabTotalVolume > 0.0 ? enaWasteAmount / totAtmoCapacity : 0.0;
			HabShieldingFactor = Radiation.ShieldingEfficiency(vesselShieldingRes.Level);
			CrewCount = Lib.CrewCount(vessel);
			HabVolumePerCrew = HabTotalVolume / Math.Max(1, CrewCount);
			HabLivingSpace = Lib.Clamp(HabVolumePerCrew / PreferencesComfort.Instance.livingSpace, 0.1, 1.0);
		}

		internal void EVARadiationUpdate(Vessel v)
		{
			RaytraceToSun(v.rootPart);
		}

		internal void HabitatsRadiationUpdate(Vessel v)
		{
			if (habitatIndex < 0)
			{
				// first run, do them all
				foreach (var habitat in habitatWrappers)
				{
					// only calculate radiation for enabled habitats
					if (habitat.State == State.enabled || habitat.State == State.evaKerbal)
						RaytraceToSun(habitat.LoadedPart);
					else
						// remove disabled habitat from shielding calculations
						habitatShieldings.Remove(habitat.LoadedPart.flightID);
				}
				habitatIndex = 0;
			}
			else
			{
				// only do one habitat at a time to preserve some performance
				// and check that part still exists
				if (habitatWrappers[habitatIndex].LoadedPart == null)
					habitatWrappers.RemoveAt(habitatIndex);
				else
				{
					// only calculate radiation for enabled habitats
					if (habitatWrappers[habitatIndex].State == State.enabled || habitatWrappers[habitatIndex].State == State.evaKerbal)
						RaytraceToSun(habitatWrappers[habitatIndex].LoadedPart);
					else
						// remove disabled habitat from shielding calculations
						habitatShieldings.Remove(habitatWrappers[habitatIndex].LoadedPart.flightID);
				}

				if (habitatWrappers.Count == 0)
					habitatIndex = -1;
				else
					habitatIndex = (habitatIndex + 1) % habitatWrappers.Count;
			}
		}

		private void RaytraceToSun(Part habitat)
		{
			if (!Features.Radiation) return;

			var habitatPosition = habitat.transform.position;
			var vd = habitat.vessel.KerbalismData();
			List<SunShieldingPartData> sunShieldingParts = new List<SunShieldingPartData>();

			Ray r = new Ray(habitatPosition, vd.EnvMainSun.Direction);
			var hits = Physics.RaycastAll(r, 200);

			foreach (var hit in hits)
			{
				if (hit.collider != null && hit.collider.gameObject != null)
				{
					Part blockingPart = Part.GetComponentUpwards<Part>(hit.collider.gameObject);
					if (blockingPart == null || blockingPart == habitat) continue;
					var mass = blockingPart.mass + blockingPart.GetResourceMass();
					mass *= 1000; // KSP masses are in tons

					// divide part mass by the mass of aluminium (2699 kg/m³), cubic root of that
					// gives a very rough approximation of the thickness, assuming it's a cube.
					// So a 40.000 kg fuel tank would be equivalent to 2.45m aluminium.

					var thickness = Math.Pow(mass / 2699.0, 1.0 / 3.0);

					sunShieldingParts.Add(new SunShieldingPartData(hit.distance, thickness));
				}
			}

			// sort by distance, in reverse
			sunShieldingParts.Sort((a, b) => b.distance.CompareTo(a.distance));

			habitatShieldings[habitat.flightID] = sunShieldingParts;
		}

		public double AverageHabitatRadiation(double radiation)
		{
			if (habitatShieldings.Count < 1) return radiation;

			var result = 0.0;

			foreach (var shieldingPartsList in habitatShieldings.Values)
			{
				var remainingRadiation = radiation;

				foreach (var shieldingInfo in shieldingPartsList)
				{
					// for a 500 keV gamma ray, halfing thickness for aluminium is 3.05cm. But...
					// Solar energetic particles (SEP) are high-energy particles coming from the Sun.
					// They consist of protons, electrons and HZE ions with energy ranging from a few tens of keV
					// to many GeV (the fastest particles can approach the speed of light, as in a
					// "ground-level event"). This is why they are such a big problem for interplanetary space travel.

					// Beer-Lambert law: Remaining radiation = radiation * e^-ux.  Not exact for SEP, but close enough to loosely fit observed curves.
					// linear attenuation coefficent u.  Asssuming an average CME event energy Al shielding 10 ~= 30 g/cm^2.
					// Averaged from NASA plots of large CME events vs Al shielding projections.
					var linearAttenuation = 10;

					// However, what you lose in particle radiation you gain in gamma radiation (Bremsstrahlung)

					var incomingRadiation = remainingRadiation;
					remainingRadiation *= Math.Exp(shieldingInfo.thickness * linearAttenuation * -1);
					var bremsstrahlung = incomingRadiation - remainingRadiation;

					result += Radiation.DistanceRadiation(bremsstrahlung, Math.Max(1, shieldingInfo.distance)) / 10; //Gamma radiation has 1/10 the quality factor of SEP
				}

				result += remainingRadiation;
			}

			return result / habitatShieldings.Count;
		}
    }

	public abstract class HabitatWrapper
	{
		public abstract class ResourceWrapper
		{
			public abstract double Amount { get; set; }
			public abstract double MaxAmount { get; set; }
			public abstract bool FlowState { get; set; }
		}

		public sealed class LoadedResource : ResourceWrapper
		{
			PartResource res;
			public override double Amount { get => res.amount; set => res.amount = value; }
			public override double MaxAmount { get => res.maxAmount; set => res.maxAmount = value; }
			public override bool FlowState { get => res.flowState; set => res.flowState = value; }

			public LoadedResource(PartResource res) => this.res = res;
		}

		public sealed class UnloadedResource : ResourceWrapper
		{
			ProtoPartResourceSnapshot res;
			public override double Amount { get => res.amount; set => res.amount = value; }
			public override double MaxAmount { get => res.maxAmount; set => res.maxAmount = value; }
			public override bool FlowState { get => res.flowState; set => res.flowState = value; }

			public UnloadedResource(ProtoPartResourceSnapshot res) => this.res = res;
		}

		public ResourceWrapper AtmoResource { get; protected set; }
		public ResourceWrapper WasteAtmoResource { get; protected set; }
		public ResourceWrapper ShieldingResource { get; protected set; }
		public Part LoadedPart { get; protected set; }

		public abstract State State { get; set; }
		public abstract double PerctDeployed { get; set; }
		public abstract bool NonPressurizable { get; }
		public abstract bool InflateRequiresPressure { get; }
		public abstract double Surface { get; }
	}

	public sealed class LoadedHabitat : HabitatWrapper
	{
		private Habitat hab;

		public override State State { get => hab.state; set => hab.state = value; }
		public override double PerctDeployed { get => hab.perctDeployed; set => hab.perctDeployed = value; }
		public override bool NonPressurizable => hab.nonPressurizable;
		public override bool InflateRequiresPressure => hab.inflateRequiresPressure;
		public override double Surface => hab.surface;

		public static bool TryCreate(Habitat hab, out LoadedHabitat wrapper)
		{
			wrapper = new LoadedHabitat();
			wrapper.LoadedPart = hab.part;
			wrapper.hab = hab;
			foreach (PartResource res in hab.part.Resources)
			{
				switch (res.resourceName)
				{
					case AtmoResName:
						wrapper.AtmoResource = new LoadedResource(res); break;
					case WasteAtmoResName:
						wrapper.WasteAtmoResource = new LoadedResource(res); break;
					case ShieldingResName:
						wrapper.ShieldingResource = new LoadedResource(res); break;
				}
			}

			return wrapper.AtmoResource != null && wrapper.WasteAtmoResource != null;
		}
	}

	public sealed class UnloadedHabitat : HabitatWrapper
	{
		private ProtoPartModuleSnapshot hab;
		private Habitat habPrefab;
		private State _cachedState;
		private double _cachedPerctDeployed;

		public override State State
		{
			get => _cachedState;
			set
			{
				_cachedState = value;
				Lib.Proto.Set(hab, nameof(Habitat.state), value);
			}
		}
		public override double PerctDeployed
		{
			get => _cachedPerctDeployed;
			set
			{
				_cachedPerctDeployed = value;
				Lib.Proto.Set(hab, nameof(Habitat.perctDeployed), value);
			}
		}

		public override bool NonPressurizable => habPrefab.nonPressurizable;
		public override bool InflateRequiresPressure => habPrefab.inflateRequiresPressure;
		public override double Surface => habPrefab.surface;

		public UnloadedHabitat(ProtoPartSnapshot habPart, ProtoPartModuleSnapshot hab, Habitat habPrefab)
		{
			this.hab = hab;
			this.habPrefab = habPrefab;
			_cachedState = Lib.Proto.GetEnum<State>(hab, nameof(Habitat.state));
			_cachedPerctDeployed = Lib.Proto.GetDouble(hab, nameof(Habitat.state));
			for (int i = habPart.resources.Count; i-- > 0;)
			{
				ProtoPartResourceSnapshot res = habPart.resources[i];
				switch (res.resourceName)
				{
					case AtmoResName:
						AtmoResource = new UnloadedResource(res); break;
					case WasteAtmoResName:
						WasteAtmoResource = new UnloadedResource(res); break;
					case ShieldingResName:
						ShieldingResource = new UnloadedResource(res); break;
				}
			}
		}
	}
}
