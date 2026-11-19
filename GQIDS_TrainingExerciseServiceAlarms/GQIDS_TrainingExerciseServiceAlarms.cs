namespace GQIDSTrainingExerciseServiceAlarms
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Messages;

	/// <summary>
	/// Represents a data source.
	/// See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = "GQIDS_TrainingExerciseServiceAlarms")]
	public sealed class GqidsTrainingExerciseServiceAlarms : IGQIDataSource
		, IGQIOnInit
		, IGQIInputArguments
		, IGQIUpdateable
		, IGQIOnDestroy
	{
		private readonly GQIIntArgument viewIdArg = new GQIIntArgument("View ID") { DefaultValue = 0, IsRequired = false };

		private readonly GQIStringColumn nameColumn = new GQIStringColumn("Name");
		private readonly GQIStringColumn alarmStateColumn = new GQIStringColumn("Alarm State");

		private GQIDMS dms;
		private int viewId;
		private IGQIUpdater updater;
		private IConnection connection;
		private string subscriptionSetId;

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			dms = args.DMS;
			return default;
		}

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[] { viewIdArg };
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			viewId = args.GetArgumentValue(viewIdArg);

			if (viewId < 0)
				throw new ArgumentException("View ID cannot be negative.");

			return default;
		}

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				nameColumn,
				alarmStateColumn,
			};
		}

		public void OnStartUpdates(IGQIUpdater updater)
		{
			this.updater = updater;
			subscriptionSetId = "ServiceAlarms_" + Guid.NewGuid();

			connection = dms.GetConnection();
			connection.OnNewMessage += OnServiceStateChanged;
			connection.AddSubscription(subscriptionSetId, new SubscriptionFilter(typeof(LiteServiceStateEvent)));
			connection.Subscribe();
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			var request = new GetLiteServiceInfo();

			if (viewId > 0)
				request.ViewID = viewId;

			var services = dms.SendMessages(request).OfType<LiteServiceInfoEvent>();

			var stateRequest = new GetAlarmStateAllMessage { Elements = false, Services = true };
			var states = new Dictionary<string, string>();
			foreach (var state in dms.SendMessages(stateRequest).OfType<GetAlarmStateResponseMessage>())
			{
				states[CreateKey(state.DataMinerID, state.ElementID)] = state.Level.ToString();
			}

			var rows = new List<GQIRow>();
			foreach (var service in services)
			{
				var name = service.Name;
				var key = CreateKey(service.DataMinerID, service.ID);
				string alarmState;
				if (!states.TryGetValue(key, out alarmState))
					alarmState = "Undefined";

				rows.Add(
					new GQIRow(
						key,
						new GQICell[]
						{
							new GQICell
							{
								Value = name,
							},
							new GQICell
							{
								Value = alarmState,
							},
						}));
			}

			return new GQIPage(rows.ToArray());
		}

		public void OnStopUpdates()
		{
			StopListening();
		}

		public OnDestroyOutputArgs OnDestroy(OnDestroyInputArgs args)
		{
			StopListening();
			return default;
		}

		private static string CreateKey(int dataMinerId, int serviceId)
		{
			return dataMinerId + "/" + serviceId;
		}

		private void OnServiceStateChanged(object sender, NewMessageEventArgs e)
		{
			if (!e.FromSet(subscriptionSetId))
				return;

			var stateEvent = e.Message as LiteServiceStateEvent;
			if (stateEvent == null)
				return;

			try
			{
				var key = CreateKey(stateEvent.DataMinerID, stateEvent.ServiceID);
				updater?.UpdateCell(key, alarmStateColumn, stateEvent.Level.ToString());
			}
			catch (Exception)
			{
				// The row can be outside the view or the query can be closing; the next event will correct the cell.
			}
		}

		private void StopListening()
		{
			var currentConnection = connection;
			connection = null;
			updater = null;

			if (currentConnection == null)
				return;

			currentConnection.OnNewMessage -= OnServiceStateChanged;
			currentConnection.ClearSubscriptions(subscriptionSetId);
		}
	}
}
