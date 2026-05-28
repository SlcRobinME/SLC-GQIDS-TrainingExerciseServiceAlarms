namespace ServiceAlarmsSCO
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Messages;

	public class AlarmEventHandler : IDisposable
	{
		private readonly IConnection _connection;
		private readonly string _setId = Guid.NewGuid().ToString();
		private readonly Dictionary<string, GQIRow> _rowCache = new Dictionary<string, GQIRow>();
		private readonly object _cacheLock = new object();
		private readonly IGQIUpdater _updater;
		private readonly GQIDMS _dms;
		private readonly int _viewId;

		public AlarmEventHandler(
			GQIDMS dms,
			int viewId,
			IGQIUpdater updater)
		{
			_dms = dms;
			_viewId = viewId;
			_updater = updater;
			_connection = dms.GetConnection();
			_connection.OnNewMessage += OnEvent;
			_connection.AddSubscription(_setId, new SubscriptionFilter(typeof(ServiceStateEventMessage)));
		}

		public bool IsEmpty
		{
			get
			{
				lock (_cacheLock)
				{
					return _rowCache.Count == 0;
				}
			}
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
				foreach (var msg in response)
				{
					if (!(msg is LiteServiceInfoEvent svc))
						continue;

					var key = Helpers.ServiceKey(svc.HostingAgentID, svc.ElementID);

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
							alarmState = Helpers.SeverityToLabel(state.Level);
							break;
						}
					}

					var row = new GQIRow(key, new GQICell[]
					{
					new GQICell { Value = svc.Name },
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
				_connection.RemoveSubscription(_setId, new SubscriptionFilter(typeof(ServiceStateEventMessage)));
				_connection.Dispose();
			}
		}

		public List<GQIRow> GetRows()
		{
			lock (_cacheLock)
			{
				return _rowCache.Values.ToList();
			}
		}

		private void OnEvent(object sender, NewMessageEventArgs e)
		{
			if (!(e.Message is ServiceStateEventMessage stateMsg))
				return;

			var key = Helpers.ServiceKey(stateMsg.HostingAgentID, stateMsg.ServiceID);
			var newAlarmState = Helpers.SeverityToLabel(stateMsg.Level);

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