using System.Collections.Generic;

namespace KERBALISM
{
	internal sealed class FusionModeData
	{
		public float powerGeneration;
		public List<ResourceRatio> inputs = new List<ResourceRatio>();

		public FusionModeData(ConfigNode node)
		{
			if (node == null)
				return;

			node.TryGetValue("powerGeneration", ref powerGeneration);

			ConfigNode[] inputNodes = node.GetNodes("INPUT_RESOURCE");
			for (int i = 0; i < inputNodes.Length; i++)
			{
				ResourceRatio ratio = new ResourceRatio();
				ratio.Load(inputNodes[i]);
				inputs.Add(ratio);
			}
		}
	}
}
