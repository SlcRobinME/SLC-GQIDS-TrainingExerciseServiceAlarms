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
		private const int TIMEOUT_SECONDS = 60;

		private readonly object _lock = new object();

		private readonly ConcurrentDictionary<string, Service> _services
			= new ConcurrentDictionary<string, Service>(StringComparer.OrdinalIgnoreCase);

		private bool _isInitialized;

		public DateTime LastUpdate { get; private set; }

		public IEnumerable<Service> Services => _services.Values;

		public bool TryGetService(int agentId, int serviceId, out Service service)
				=> _services.TryGetValue($"{agentId}/{serviceId}", out service);

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
			var key = $"{agentId}/{serviceId}";
			if (!_services.TryGetValue(key, out var existing))
				return;

			_services[key] = new Service
			{
				Id = existing.Id,
				AgentId = existing.AgentId,
				Name = existing.Name,
				ViewIds = existing.ViewIds,
				Alarm = newLevel,
			};
		}

		private void Initialize(IDms dms, IGQILogger logger)
		{
			logger?.Information("ServiceCache - Initializing.");

			var services = dms.GetServices()
				.Where(s => !s.AdvancedSettings.IsTemplate)
				.ToList();

			foreach (var svc in services)
			{
				string key = $"{svc.AgentId}/{svc.Id}";
				_services[key] = new Service
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

				var currentKeys = _services.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
				var updatedKeys = updatedServices.Values
					.Select(s => $"{s.AgentId}/{s.Id}")
					.ToHashSet(StringComparer.OrdinalIgnoreCase);

				foreach (var keyToRemove in currentKeys.Except(updatedKeys))
					_services.TryRemove(keyToRemove, out _);

				foreach (var svc in updatedServices.Values)
				{
					string key = $"{svc.AgentId}/{svc.Id}";
					_services[key] = new Service
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