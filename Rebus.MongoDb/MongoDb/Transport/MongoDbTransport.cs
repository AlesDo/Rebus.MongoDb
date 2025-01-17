﻿using Rebus.Config;
using Rebus.Messages;
using Rebus.MongoDb.Transport.Model;
using Rebus.Transport;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rebus.Logging;
using System;

namespace Rebus.MongoDb.Transport
{
   public class MongoDbTransport : AbstractRebusTransport
   {
      private readonly ILog _logger;
      readonly MongoDbTransportConfiguration _mongoDbTransportConfiguration;
      MongoDbMessageConsumer _mongoDbMessageConsumer;
      readonly MongoDbMessageProducer _mongoDbMessageProducer;
      /// <summary>
      /// When a message is sent to this address, it will be deferred into the future!
      /// </summary>
      public const string MagicExternalTimeoutManagerAddress = "##### MagicExternalTimeoutManagerAddress #####";

      public MongoDbTransport(IRebusLoggerFactory rebusLoggerFactory, MongoDbTransportOptions mongoDbTransportOptions) : base(mongoDbTransportOptions.InputQueueName)
      {
         _logger = rebusLoggerFactory.GetLogger<MongoDbTransport>();
         try
         {
            _mongoDbTransportConfiguration = new MongoDbTransportConfiguration(mongoDbTransportOptions.ConnectionString, "messages");
            _mongoDbMessageProducer = _mongoDbTransportConfiguration.CreateProducer();
            if (!string.IsNullOrWhiteSpace(Address))
            {
               _mongoDbMessageConsumer = _mongoDbTransportConfiguration.CreateConsumer(Address);
            }
         }
         catch (Exception ex)
         {
            _logger.Error(ex, "MongoDB transport creation failed.");
            throw;
         }
      }

      public override void CreateQueue(string address)
      {
      }

      public override async Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken)
      {
         if (_mongoDbMessageConsumer != null)
         {
            MongoDbReceivedMessage receivedMessage = await _mongoDbMessageConsumer.GetNextAsync();
            if (receivedMessage != null && cancellationToken.IsCancellationRequested)
            {
               await receivedMessage.Nack();
               return null;
            }
            if (receivedMessage != null && context != null)
            {
               context.OnAck((context) => receivedMessage.Ack());
               context.OnNack((context) => receivedMessage.Nack());
            }
            return receivedMessage;
         }
         else
         {
            _logger.Debug("Receive will always return null because no input queue has been specified.");
         }
         return null;
      }

      protected override async Task SendOutgoingMessages(IEnumerable<OutgoingTransportMessage> outgoingMessages, ITransactionContext context)
      {
         await _mongoDbMessageProducer.SendOutgoingMessagesWithoutTransaction(outgoingMessages);
      }
   }
}
