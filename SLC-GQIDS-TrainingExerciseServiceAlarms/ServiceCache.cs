namespace SLCGQIDSTrainingExerciseServiceAlarms
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;

	public class ServiceCache
	{
		private const int TIMEOUT_SECONDS = 30;

		private readonly object _lock = new object();
		private readonly ConcurrentDictionary<string, Service> _services
			= new ConcurrentDictionary<string, Service>(StringComparer.OrdinalIgnoreCase);

		private bool _isInitialized;

		public DateTime LastUpdate { get; private set; }

		public IEnumerable<Service> Services => _services.Values;

		public void EnsureInitialized(IDms dms, IGQILogger logger)
		{
			if (_isInitialized)
				return;

			lock (_lock)
			{
				if (_isInitialized)
					return;

				Initialize(dms, logger);
			}
		}

		public void EnsureUpdated(IDms dms, IGQILogger logger)
		{
			if (!_isInitialized)
				return;

			if (LastUpdate > DateTime.UtcNow - TimeSpan.FromSeconds(TIMEOUT_SECONDS))
				return;

			lock (_lock)
			{
				if (LastUpdate > DateTime.UtcNow - TimeSpan.FromSeconds(TIMEOUT_SECONDS))
					return;

				UpdateData(dms, logger);
			}
		}

		public void UpdateAlarm(int agentId, int serviceId, AlarmLevel newLevel)
		{
			var key = serviceId.ToString();
			if (_services.TryGetValue(key, out var existing))
			{
				_services[key] = new Service
				{
					Id = existing.Id,
					AgentId = existing.AgentId,
					Name = existing.Name,
					Alarm = newLevel,
					ViewIds = existing.ViewIds,
				};
			}
		}

		private void Initialize(IDms dms, IGQILogger logger)
		{
			logger?.Information("ServiceCache - Initializing.");

			var services = dms.GetServices()
				.Where(s => !s.AdvancedSettings.IsTemplate)
				.ToList();

			foreach (var svc in services)
			{
				_services[svc.Id.ToString()] = new Service
				{
					Id = svc.Id,
					AgentId = svc.AgentId,
					Name = svc.Name,
					Alarm = svc.GetState().Level,
					ViewIds = svc.Views.Select(v => v.Id).ToList(),
				};
			}

			_isInitialized = true;
			LastUpdate = DateTime.UtcNow;
			logger?.Information($"ServiceCache - Initializing done. Services: {_services.Count}");
		}

		private void UpdateData(IDms dms, IGQILogger logger)
		{
			logger?.Information("ServiceCache - Fetching update.");

			try
			{
				var updatedServices = dms.GetServices()
					.Where(s => !s.AdvancedSettings.IsTemplate)
					.ToDictionary(s => s.Id);

				var currentIds = _services.Keys.Select(int.Parse).ToHashSet();
				var updatedIds = updatedServices.Keys.ToHashSet();

				foreach (var id in currentIds.Except(updatedIds))
					_services.TryRemove(id.ToString(), out _);

				foreach (var svc in updatedServices.Values)
				{
					_services[svc.Id.ToString()] = new Service
					{
						Id = svc.Id,
						AgentId = svc.AgentId,
						Name = svc.Name,
						Alarm = svc.GetState().Level,
						ViewIds = svc.Views.Select(v => v.Id).ToList(),
					};
				}

				LastUpdate = DateTime.UtcNow;
				logger?.Information($"ServiceCache - Update done. Services: {_services.Count}");
			}
			catch (Exception ex)
			{
				logger?.Error($"ServiceCache - Error during update: {ex.Message}");
			}
		}
	}
}