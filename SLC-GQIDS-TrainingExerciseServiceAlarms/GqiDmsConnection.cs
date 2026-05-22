namespace SLCGQIDSTrainingExerciseServiceAlarms
{
	using System;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Messages;

	public class GqiDmsConnection : ICommunication
	{
		private readonly GQIDMS _gqiDms;

		public GqiDmsConnection(GQIDMS gqiDms)
		{
			_gqiDms = gqiDms;
		}

		public IConnection SlNetConnection => throw new NotImplementedException();

		public void AddSubscriptionHandler(NewMessageEventHandler handler)
		{
			throw new NotImplementedException();
		}

		public void AddSubscriptions(NewMessageEventHandler handler, string setId, string internalHandleIdentifier, SubscriptionFilter[] subscriptions)
		{
			throw new NotImplementedException();
		}

		public void AddSubscriptions(NewMessageEventHandler handler, string setId, string internalHandleIdentifier, SubscriptionFilter[] subscriptions, TimeSpan subscribeTimeout)
		{
			throw new NotImplementedException();
		}

		public void ClearSubscriptionHandler(NewMessageEventHandler handler)
		{
			throw new NotImplementedException();
		}

		public void ClearSubscriptions(string setId, string internalHandleIdentifier, SubscriptionFilter[] subscriptions, bool force = false)
		{
			throw new NotImplementedException();
		}

		public void ClearSubscriptions(string setId, string internalHandleIdentifier, SubscriptionFilter[] subscriptions, TimeSpan subscribeTimeout, bool force = false)
		{
			throw new NotImplementedException();
		}

		public DMSMessage[] SendMessage(DMSMessage message)
		{
			return _gqiDms.SendMessages(message);
		}

		public DMSMessage[] SendMessages(params DMSMessage[] messages)
		{
			return _gqiDms.SendMessages(messages);
		}

		public DMSMessage SendSingleRawResponseMessage(DMSMessage message)
		{
			return _gqiDms.SendMessages(message)?.FirstOrDefault();
		}

		public DMSMessage SendSingleResponseMessage(DMSMessage message)
		{
			return _gqiDms.SendMessages(message)?.FirstOrDefault();
		}
	}
}