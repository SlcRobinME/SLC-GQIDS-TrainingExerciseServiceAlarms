namespace SLCGQIDSTrainingExerciseServiceAlarms
{
	using System;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Messages;

	internal sealed class ServiceWatcher : IDisposable
	{
		private readonly IConnection _connection;
		private readonly string _setId = Guid.NewGuid().ToString();

		internal ServiceWatcher(GQIDMS dms)
		{
			_connection = dms.GetConnection() ?? throw new GenIfException("Could not create a connection.");
			var subscriptionFilter = new SubscriptionFilter(typeof(ServiceStateEventMessage));
			_connection.OnNewMessage += Connection_OnNewMessage;
			_connection.AddSubscription(_setId, subscriptionFilter);
		}

		internal event EventHandler<ServiceStateEventMessage> OnChanged;

		public void Dispose()
		{
			try
			{
				if (_connection != null)
				{
					_connection.OnNewMessage -= Connection_OnNewMessage;
					_connection.ClearSubscriptions(_setId);
					_connection.Unsubscribe();
					_connection.Dispose();
				}
			}
			catch (Exception)
			{
				// Ignore
			}
		}

		private void Connection_OnNewMessage(object sender, NewMessageEventArgs e)
		{
			if (e.Message is ServiceStateEventMessage change)
				OnChanged?.Invoke(this, change);
		}
	}
}