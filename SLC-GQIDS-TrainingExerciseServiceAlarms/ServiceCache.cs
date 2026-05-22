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

		private readonly string _securityKey;
		private readonly object _lock = new object();
		private readonly ConcurrentDictionary<string, Service> _services
			= new ConcurrentDictionary<string, Service>(StringComparer.OrdinalIgnoreCase);

		private bool _isInitialized;

		public ServiceCache(string securityKey)
		{
			if (string.IsNullOrWhiteSpace(securityKey))
				throw new ArgumentException("SecurityKey can't be empty.");
			_securityKey = securityKey;
		}

		public DateTime LastUpdate { get; private set; }

		public IEnumerable<Service> Services => _services.Values;

		public void EnsureInitialized(IDms dms, IGQILogger logger)
		{
			if (_isInitialized)
				return;

			lock (_lock)
			{
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

		private void Initialize(IDms dms, IGQILogger logger)
		{
			logger?.Information($"{_securityKey} - Initializing.");

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
			logger?.Information($"{_securityKey} - Initializing done. Services: {_services.Count}");
		}

		private void UpdateData(IDms dms, IGQILogger logger)
		{
			logger?.Information($"{_securityKey} - Fetching update.");

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
					var newService = new Service
					{
						Id = svc.Id,
						AgentId = svc.AgentId,
						Name = svc.Name,
						Alarm = svc.GetState().Level,
						ViewIds = svc.Views.Select(v => v.Id).ToList(),
					};

					if (!_services.TryGetValue(svc.Id.ToString(), out var existing) || !ServiceEquals(existing, newService))
						_services[svc.Id.ToString()] = newService;
				}

				LastUpdate = DateTime.UtcNow;
				logger?.Information($"{_securityKey} - Update done. Services: {_services.Count}");
			}
			catch (Exception ex)
			{
				logger?.Error($"{_securityKey} - Error during update: {ex.Message}");
			}
		}

		private bool ServiceEquals(Service s1, Service s2)
		{
			if (s1 == null || s2 == null)
				return false;
			if (s1.Id != s2.Id)
				return false;
			if (!string.Equals(s1.Name, s2.Name, StringComparison.Ordinal))
				return false;
			if (s1.Alarm != s2.Alarm)
				return false;
			if (s1.AgentId != s2.AgentId)
				return false;
			if (s1.ViewIds == null && s2.ViewIds == null)
				return true;
			if (s1.ViewIds == null || s2.ViewIds == null)
				return false;
			return s1.ViewIds.SequenceEqual(s2.ViewIds);
		}
	}
}