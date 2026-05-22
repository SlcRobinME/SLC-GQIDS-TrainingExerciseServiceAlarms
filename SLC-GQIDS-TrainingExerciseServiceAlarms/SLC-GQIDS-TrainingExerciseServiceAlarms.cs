namespace SLCGQIDSTrainingExerciseServiceAlarms
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
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

		private static readonly ConcurrentDictionary<string, ServiceCache> _groupToCache
			= new ConcurrentDictionary<string, ServiceCache>(StringComparer.OrdinalIgnoreCase);

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
			_dms = DmsFactory.CreateDms(new GqiDmsConnection(_dMS));
			return default;
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			var cache = GetCache();
			cache.EnsureInitialized(_dms, _logger);
			cache.EnsureUpdated(_dms, _logger);

			var rows = cache.Services
				.Where(svc => IsServiceInView(svc, _viewId))
				.Select(svc =>
				{
					var cells = new GQICell[]
					{
						new GQICell { Value = svc.Name },
						new GQICell { Value = svc.Alarm.ToString() },
					};
					return new GQIRow($"{svc.AgentId}/{svc.Id}", cells);
				})
				.ToArray();

			_logger?.Information($"Returning {rows.Length} rows for viewId={_viewId}");

			return new GQIPage(rows) { HasNextPage = false };
		}

		private bool IsServiceInView(Service svc, int viewId)
		{
			if (viewId == 0)
				return true;
			return svc.ViewIds != null && svc.ViewIds.Contains(viewId);
		}

		private ServiceCache GetCache()
		{
			var securityGroupKey = GetSecurityGroupKey();

			if (!_groupToCache.TryGetValue(securityGroupKey, out var cache))
			{
				lock (_groupToCache)
				{
					cache = new ServiceCache(securityGroupKey);
					_groupToCache[securityGroupKey] = cache;
				}
			}

			return cache;
		}

		private string GetSecurityGroupKey()
		{
			try
			{
				var responses = _dMS.SendMessages(
					new GetUserFullNameMessage(),
					new GetInfoMessage(InfoType.SecurityInfo));

				var userName = responses?.OfType<GetUserFullNameResponseMessage>()
					.FirstOrDefault()?.User;

				if (string.IsNullOrEmpty(userName))
				{
					_logger?.Error("User not found.");
					return "default";
				}

				var securityResponse = responses?.OfType<GetUserInfoResponseMessage>().FirstOrDefault();
				var userGroups = securityResponse?.Users?
					.Where(u => string.Equals(u.Name, userName, StringComparison.InvariantCultureIgnoreCase))
					.FirstOrDefault()?.Groups?.OrderBy(x => x).ToArray() ?? new int[0];

				if (userGroups.Length == 0)
				{
					_logger?.Error("User is not part of any group.");
					return "default";
				}

				return string.Join(";", userGroups);
			}
			catch (Exception ex)
			{
				_logger?.Error($"GetSecurityGroupKey failed: {ex.Message}");
				return "default";
			}
		}

		public void OnStartUpdates(IGQIUpdater updater)
		{
			_updater = updater;
		}

		public void OnStopUpdates()
		{
			_updater = null;
		}

		public OnDestroyOutputArgs OnDestroy(OnDestroyInputArgs args)
		{
			_updater = null;
			return default;
		}
	}
}