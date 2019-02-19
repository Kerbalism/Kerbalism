namespace KERBALISM.Planner
{

	/// <summary> Offers a view on a single resource in the planners simulator,
	/// hides the difference between vessel wide resources that can flow through the entire vessel
	/// and resources that are restricted to a single part </summary>
	public abstract class SimulatedResourceView
	{
		protected SimulatedResourceView() { }

		public abstract double amount { get; }
		public abstract double capacity { get; }
		public abstract double storage { get; }

		public abstract void AddPartResources(Part p);
		public abstract void Produce(double quantity, string name);
		public abstract void Consume(double quantity, string name);
		public abstract void Clamp();
	}


} // KERBALISM
