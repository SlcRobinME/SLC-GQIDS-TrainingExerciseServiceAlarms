namespace ServiceAlarmsSCO
{
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Messages;

	/// <summary>
	/// Represents a data source.
	/// See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = "Service Alarms SCO")]
	public sealed class ServiceAlarmsSCO : IGQIDataSource
        , IGQIOnInit
        , IGQIInputArguments
        , IGQIUpdateable
        , IGQIOnDestroy
    {
        private static string ServiceKey(int dmaId, int elementID) => $"{dmaId}/{elementID}";

        private static string SeverityToLabel(AlarmLevel severity)
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

        private readonly GQIIntArgument _viewIdArg = new GQIIntArgument("View ID")
        {
            IsRequired = false,
            DefaultValue = 0,
        };

        private readonly GQIStringColumn _nameColumn = new GQIStringColumn("Name");
        private readonly GQIStringColumn _alarmState = new GQIStringColumn("Alarm state");

        private GQIDMS _dms;
        private int _viewId;
        private IGQIUpdater _updater;
        private IGQILogger _logger;
        private readonly Dictionary<string, GQIRow> _rowCache = new Dictionary<string, GQIRow>();
        private readonly object _cacheLock = new object();

        public OnInitOutputArgs OnInit(OnInitInputArgs args)
        {
            _dms = args.DMS;
            _logger = args.Logger;
            return new OnInitOutputArgs();
        }

        public GQIArgument[] GetInputArguments()
        {
            return new GQIArgument[] { _viewIdArg };

        }

        public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
        {
            _viewId = args.GetArgumentValue(_viewIdArg);
            return new OnArgumentsProcessedOutputArgs();
        }

        public GQIColumn[] GetColumns() => new GQIColumn[]
        {
            _nameColumn,
            _alarmState,
        };

        public void OnStartUpdates(IGQIUpdater updater)
        {
			_updater = updater;

			var connection = _dms.GetConnection();
			connection.OnNewMessage += OnEvent;
			connection.Subscribe(new SubscriptionFilter(typeof(ServiceStateEventMessage)));
		}

        public GQIPage GetNextPage(GetNextPageInputArgs args)
        {
            if (_rowCache.Count == 0)
            {
                LoadServicesFromDms();
            }

            List<GQIRow> rows;

            lock (_cacheLock)
            {
                rows = _rowCache.Values.ToList();
            }

            return new GQIPage(rows.ToArray())
            {
                HasNextPage = false,
            };
        }

        public void OnStopUpdates()
        {
			var connection = _dms.GetConnection();
			connection.OnNewMessage -= OnEvent;
			connection.Unsubscribe();
			_updater = null;
		}

        public OnDestroyOutputArgs OnDestroy(OnDestroyInputArgs args)
        {
            return new OnDestroyOutputArgs();
        }

        private void LoadServicesFromDms()
		{
			var request = new GetLiteServiceInfo
			{
				ViewID = _viewId == 0 ? int.MaxValue : _viewId,
			};
			var response = _dms.SendMessages(request);

			lock (_cacheLock)
			{
				_rowCache.Clear();
				foreach (var msg in response)
				{
					if (!(msg is LiteServiceInfoEvent svc))
						continue;

					var key = ServiceKey(svc.DataMinerID, svc.ElementID);

					var stateRequest = new GetServiceStateMessage
					{
						DataMinerID = svc.DataMinerID,
						ServiceID = svc.ElementID,
					};

					var stateResponse = _dms.SendMessages(stateRequest);
					var alarmState = "Undefined";

					foreach (var stateMsg in stateResponse)
						{
						if (stateMsg is ServiceStateEventMessage state)
							{
							alarmState = SeverityToLabel(state.Level);
							break;
							}
						}

					var row = new GQIRow(key, new GQICell[]
					{
						new GQICell {Value = svc.Name},
						new GQICell {Value = alarmState},
					});
					_rowCache[key] = row;
				}
			}
		}

        private void OnEvent(object sender, NewMessageEventArgs e)
		{
			if (!(e.Message is ServiceStateEventMessage stateMsg))
				return;

			var key = ServiceKey(stateMsg.DataMinerID, stateMsg.ServiceID);
			var newAlarmState = SeverityToLabel(stateMsg.Level);

			lock (_cacheLock)
			{
				if (_rowCache.TryGetValue(key, out var existingRow))
				{
					var updatedRow = new GQIRow(existingRow.Key, new GQICell[]
					{
						new GQICell { Value = existingRow.Cells[0].Value },
						new GQICell { Value = newAlarmState },
					});

					_rowCache[key] = updatedRow;
					_updater?.UpdateRow(updatedRow);
				}
			}
		}
	}
}
