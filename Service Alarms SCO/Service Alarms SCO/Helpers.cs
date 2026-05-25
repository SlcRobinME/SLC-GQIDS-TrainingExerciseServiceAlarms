namespace ServiceAlarmsSCO
{
	using Skyline.DataMiner.Net.Messages;

	public static class Helpers
	{
		public static string ServiceKey(int dmaId, int elementID) => $"{dmaId}/{elementID}";

		public static string SeverityToLabel(AlarmLevel severity)
		{
			switch (severity)
			{
				case AlarmLevel.Normal: return "Normal";
				case AlarmLevel.Warning: return "Warning";
				case AlarmLevel.Minor: return "Minor";
				case AlarmLevel.Major: return "Major";
				case AlarmLevel.Critical: return "Critical";
				default: return "Undefined";
			}
		}
	}
}
