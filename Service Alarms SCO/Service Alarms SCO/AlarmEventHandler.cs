namespace ServiceAlarmsSCO
{
	using System;
	using System.Collections.Generic;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Messages;

	public class AlarmEventHandler : IDisposable
	{
		private readonly IConnection _connection;
		private readonly string _setId = Guid.NewGuid().ToString();
		private readonly Dictionary<string, GQIRow> _rowCache;
		private readonly object _cacheLock;
		private readonly IGQIUpdater _updater;
		private readonly GQIDMS _dms;
		private readonly int _viewId;
		private readonly Dictionary<string, string> _elementToServiceMap = new Dictionary<string, string>();

		public AlarmEventHandler(
			GQIDMS dms,
			int viewId,
			Dictionary<string, GQIRow> rowCache,
			object cacheLock,
			IGQIUpdater updater)
		{
			_dms = dms;
			_viewId = viewId;
			_rowCache = rowCache;
			_cacheLock = cacheLock;
			_updater = updater;
			_connection = dms.GetConnection();
			_connection.OnNewMessage += OnEvent;
			_connection.AddSubscription(_setId, new SubscriptionFilter(typeof(AlarmEventMessage)));
		}

		public void LoadServicesFromDms()
		{
			var request = new GetLiteServiceInfo
			{
				ViewID = _viewId == 0 ? int.MaxValue : _viewId,
			};
			var response = _dms.SendMessages(request);

			lock (_cacheLock)
			{
				_rowCache.Clear();
				_elementToServiceMap.Clear();

				foreach (var message in response)
				{
					if (!(message is LiteServiceInfoEvent service))
						continue;

					var key = ServiceKey(service.HostingAgentID, service.ElementID);

					if(service.Children != null)
					{
						foreach (var child in service.Children)
						{
							var elementKey = ServiceKey(child.DataMinerID, child.ElementID);
							_elementToServiceMap[elementKey] = key;
						}
					}

					var stateRequest = new GetServiceStateMessage
					{
						DataMinerID = service.DataMinerID,
						ServiceID = service.ElementID,
					};

					var stateResponse = _dms.SendMessages(stateRequest);
					var alarmState = "Undefined";

					foreach (var stateMsg in stateResponse)
					{
						if (stateMsg is ServiceStateEventMessage state)
						{
							alarmState = state.Level.ToString();
							break;
						}
					}

					var row = new GQIRow(key, new GQICell[]
					{
						new GQICell { Value = service.Name },
						new GQICell { Value = alarmState },
					});
					_rowCache[key] = row;
				}
			}
		}

		public void Dispose()
		{
				if (_connection != null)
				{
					_connection.OnNewMessage -= OnEvent;
					_connection.RemoveSubscription(_setId, new SubscriptionFilter(typeof(AlarmEventMessage)));
					_connection.Dispose();
				}
		}

		private static string ServiceKey(int dmaId, int elementID) => $"{dmaId}/{elementID}";

		private void OnEvent(object sender, NewMessageEventArgs e)
		{
			if (!(e.Message is AlarmEventMessage alarmMessage))
			{
				return;
			}

			var key = ServiceKey(alarmMessage.HostingAgentID, alarmMessage.ElementID);
			var newAlarmState = string.IsNullOrEmpty(alarmMessage.Severity) ? "Undefined" : alarmMessage.Severity;

			lock (_cacheLock)
			{
				if (!_elementToServiceMap.TryGetValue(key, out var serviceKey))
				{
					return;
				}

				if (!_rowCache.TryGetValue(serviceKey, out var existingRow))
				{
					return;
				}

				var updatedRow = new GQIRow(existingRow.Key, new GQICell[]
				{
					new GQICell { Value = existingRow.Cells[0].Value },
					new GQICell { Value = newAlarmState },
				});

				_rowCache[serviceKey] = updatedRow;
				_updater?.UpdateRow(updatedRow);
			}
		}
	}
}