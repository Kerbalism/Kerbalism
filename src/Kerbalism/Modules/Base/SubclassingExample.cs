using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM.Modules.Base
{
	// Base classes that derive from KsmPartModule and ModuleData.
	// Have to be abstract so the activator doesn't get confused.
	// Could exclude generics as an alternative, but this isn't meant to be instantiated anyway.
	public abstract class ModuleAnimal<TModule, TData> : KsmPartModule<TModule, TData>
	where TModule : ModuleAnimal<TModule, TData>
	where TData : AnimalData<TModule, TData>
	{
		public override void OnStart(StartState state) => Lib.Log($"This animal has {moduleData.legCount} legs");
	}

	public abstract class AnimalData<TModule, TData> : ModuleData<TModule, TData>
	where TModule : ModuleAnimal<TModule, TData>
	where TData : AnimalData<TModule, TData>
	{
		public int legCount;
	}

	// Non-generic version of the base classes, necessary so KSP can instantiate it as partmodules (poor KSP...)
	public class ModuleAnimal : ModuleAnimal<ModuleAnimal, AnimalData> { }
	public class AnimalData : AnimalData<ModuleAnimal, AnimalData> { }

	// derivative classes
	public class ModuleCat : ModuleAnimal<ModuleCat, CatData>
	{
		public override void OnStart(StartState state)
		{
			moduleData.legCount = 4;
			moduleData.isHungry = true;

			base.OnStart(state);

			if (moduleData.isHungry)
				Lib.Log($"But this is in fact a Cat ! And he's hungry !");
		}
	}

	public class CatData : AnimalData<ModuleCat, CatData>
	{
		public bool isHungry;
	}
}
