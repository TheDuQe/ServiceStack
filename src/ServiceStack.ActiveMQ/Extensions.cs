﻿using Apache.NMS;
using ServiceStack.Logging;
using ServiceStack.Text;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServiceStack.ActiveMq
{
	public static class ActiveMqExtensions
    {
		public static ILog Logger = Logging.LogManager.LogFactory.GetLogger(typeof(ActiveMqExtensions).Namespace);

		private static Server GetActiveMqServer()
		{
			if (HostContext.AppHost == null)
				return null;

			return HostContext.TryResolve<Messaging.IMessageService>() as Server;
		}

		public static Messaging.IMessage<T> ToMessage<T>(this Apache.NMS.IObjectMessage msgResult)
		{
			if (msgResult == null)
				return null;

			msgResult.Body = (T)ServiceStack.Text.JsonSerializer.DeserializeFromString(msgResult.Body.ToString(), typeof(T));

			Guid outValue = Guid.Empty;
			var message = new Messaging.Message<T>((T)msgResult.Body);

			message.Meta = new Dictionary<string, string>();

			if (Guid.TryParse(msgResult.NMSMessageId, out outValue)) message.Id = outValue;
			message.Meta.Add("Origin", msgResult.NMSDestination.ToString());
			message.CreatedDate = msgResult.NMSTimestamp;
			message.Priority = (long)msgResult.NMSPriority;
			message.ReplyTo = msgResult.NMSReplyTo==null?"": msgResult.NMSReplyTo.ToString();
			message.Tag = msgResult.NMSDeliveryMode.ToString();
			message.RetryAttempts = msgResult.NMSRedelivered ? 1 : 0;

			if(msgResult is Apache.NMS.ActiveMQ.Commands.ActiveMQMessage)
			{
				message.RetryAttempts = ((Apache.NMS.ActiveMQ.Commands.ActiveMQMessage)msgResult).NMSXDeliveryCount;
			}

			if(Guid.TryParse(msgResult.NMSCorrelationID, out outValue)) message.ReplyId = outValue;


			// Add metadata (properties) to message
			var keyEnumerator = msgResult.Properties.Keys.GetEnumerator();
			while(keyEnumerator.MoveNext())
			{
				message.Meta[keyEnumerator.Current.ToString()] = msgResult.Properties[keyEnumerator.Current.ToString()] == null ? null :
					Text.JsonSerializer.SerializeToString(msgResult.Properties[keyEnumerator.Current.ToString()]);
			}
			

			return message;
		}

		public static IObjectMessage CreateMessage (this IMessageProducer producer, ServiceStack.Messaging.IMessage message)
		{
			IObjectMessage msg = producer.CreateObjectMessage(message.Body);
			string guid = !message.ReplyId.HasValue ? null : message.ReplyId.Value.ToString();
			msg.NMSMessageId = message.Id.ToString();
			msg.NMSCorrelationID = guid;

			if (message.Meta != null)
			{
				foreach (var entry in message.Meta)
				{
					var converter = System.ComponentModel.TypeDescriptor.GetConverter(entry.Value.GetType());
					var result = converter.ConvertFrom(entry.Value);
					msg.Properties.SetString(entry.Key, entry.Value);
				}
			}
			return msg;
		}

	}
}