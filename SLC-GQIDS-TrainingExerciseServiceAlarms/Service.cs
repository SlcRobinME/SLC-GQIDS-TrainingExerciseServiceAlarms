namespace SLCGQIDSTrainingExerciseServiceAlarms
{
	using System.Collections.Generic;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;

	public class Service
	{
		public int Id { get; set; }

		public int AgentId { get; set; }

		public string Name { get; set; }

		public AlarmLevel Alarm { get; set; }

		public List<int> ViewIds { get; set; }
	}
}