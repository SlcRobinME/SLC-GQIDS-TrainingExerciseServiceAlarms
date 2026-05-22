namespace SLCGQIDSTrainingExerciseServiceAlarms
{
	using System;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net.Messages;

	[GQIMetaData(Name = "SLC-GQIDS-TrainingExerciseServiceAlarms")]
	public sealed class SLCGQIDSTrainingExerciseServiceAlarms :
		IGQIDataSource,
		IGQIInputArguments,
		IGQIOnInit,
		IGQIOnDestroy,
		IGQIUpdateable
	{
		private static readonly GQIStringColumn _colName = new GQIStringColumn("Name");
		private static readonly GQIStringColumn _colAlarmState = new GQIStringColumn("AlarmState");

		// Single shared cache for all instances
		private static readonly ServiceCache _cache = new ServiceCache();

		private readonly GQIIntArgument _argViewId = new GQIIntArgument("ViewId")
		{
			IsRequired = false,
			DefaultValue = 0,
		};

		private GQIDMS _dMS;
		private IDms _dms;
		private int _viewId;
		private IGQIUpdater _updater;
		private IGQILogger _logger;
		private ServiceWatcher _watcher;

		public GQIColumn[] GetColumns() => new GQIColumn[] { _colName, _colAlarmState };

		public GQIArgument[] GetInputArguments() => new GQIArgument[] { _argViewId };

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			_viewId = args.GetArgumentValue(_argViewId);
			return default;
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_logger = args.Logger;
			_dMS = args.DMS;
			_dms = _dMS.GetConnection().GetDms();
			return default;
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			_cache.EnsureInitialized(_dms, _logger);
			_cache.EnsureUpdated(_dms, _logger);

			var rows = _cache.Services
				.Where(svc => IsServiceInView(svc, _viewId))
				.Select(svc => BuildRow(svc))
				.ToArray();

			_logger?.Information($"Returning {rows.Length} rows for viewId={_viewId}");

			return new GQIPage(rows) { HasNextPage = false };
		}

		public void OnStartUpdates(IGQIUpdater updater)
		{
			_updater = updater;
			_watcher = new ServiceWatcher(_dMS);
			_watcher.OnChanged += Watcher_OnChanged;
		}

		public void OnStopUpdates()
		{
			if (_watcher != null)
			{
				_watcher.OnChanged -= Watcher_OnChanged;
				_watcher.Dispose();
				_watcher = null;
			}

			_updater = null;
		}

		public OnDestroyOutputArgs OnDestroy(OnDestroyInputArgs args)
		{
			OnStopUpdates();
			return default;
		}

		private void Watcher_OnChanged(object sender, ServiceStateEventMessage e)
		{
			var serviceKey = $"{e.DataMinerID}/{e.ElementID}";

			var existing = _cache.Services.FirstOrDefault(s => $"{s.AgentId}/{s.Id}" == serviceKey);
			if (existing == null || existing.Alarm.ToString() == e.Level.ToString())
				return;

			_logger?.Debug($"{nameof(Watcher_OnChanged)}: {serviceKey} - {e.Level}");

			_cache.UpdateAlarm(e.DataMinerID, e.ElementID, (Skyline.DataMiner.Core.DataMinerSystem.Common.AlarmLevel)e.Level);

			var updated = _cache.Services.FirstOrDefault(s => $"{s.AgentId}/{s.Id}" == serviceKey);
			if (updated == null || !IsServiceInView(updated, _viewId))
				return;

			var updatedRow = BuildRow(updated);
			_updater?.UpdateRow(updatedRow);
		}

		private GQIRow BuildRow(Service svc)
		{
			var cells = new GQICell[]
			{
				new GQICell { Value = svc.Name },
				new GQICell { Value = svc.Alarm.ToString() },
			};
			return new GQIRow($"{svc.AgentId}/{svc.Id}", cells);
		}

		private bool IsServiceInView(Service svc, int viewId)
		{
			if (viewId == 0)
				return true;
			return svc.ViewIds != null && svc.ViewIds.Contains(viewId);
		}
	}
}