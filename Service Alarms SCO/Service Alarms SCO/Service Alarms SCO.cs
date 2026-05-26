namespace ServiceAlarmsSCO
{
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;

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
		private readonly GQIIntArgument _viewIdArg = new GQIIntArgument("View ID")
        {
            IsRequired = false,
            DefaultValue = 0,
        };

		private readonly GQIStringColumn _nameColumn = new GQIStringColumn("Name");
		private readonly GQIStringColumn _alarmState = new GQIStringColumn("Alarm state");
		private readonly Dictionary<string, GQIRow> _rowCache = new Dictionary<string, GQIRow>();
		private readonly object _cacheLock = new object();

		private GQIDMS _dms;
		private int _viewId;
		private IGQIUpdater _updater;
		private AlarmEventHandler _watcher;

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
        {
            _dms = args.DMS;
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
			_watcher = new AlarmEventHandler(_dms, _viewId, _rowCache, _cacheLock,updater);
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
        {
			if (_rowCache.Count == 0 && _watcher != null)
				_watcher.LoadServicesFromDms();

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
			_watcher?.Dispose();
			_watcher = null;
			_updater = null;
		}

		public OnDestroyOutputArgs OnDestroy(OnDestroyInputArgs args)
        {
            return new OnDestroyOutputArgs();
        }
	}
}