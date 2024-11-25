using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using System.Data.Common;

namespace EdiRetrieval.Services
{
    public class PaymentServiceBusProcessor
    {
        private readonly ILogger<PaymentServiceBusProcessor> _logger;
        private readonly IConfiguration _configuration;
        private ISubscriptionClient _subscriptionClient;
        private readonly ApplicationDbContext _context;
        private static readonly string ServiceBusConnectionString= Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString");
        private static readonly string TopicName =Environment.GetEnvironmentVariable("AzureServiceBus_QueueName");
        private static readonly string Subscription = Environment.GetEnvironmentVariable("AzureServiceBus_SubscriptionName");

        // Constructor
        public PaymentServiceBusProcessor(
            ILogger<PaymentServiceBusProcessor> logger,
            IConfiguration configuration,
            ApplicationDbContext context)
        {
            _logger = logger;
            _configuration = configuration;
            _context = context;
            
            // Load environment variables from .env file
            Env.Load();
        }

        public async Task StartProcessingMessagesAsync()
        {
            try
            {
                // Use IConfiguration to access the values loaded from .env or other configuration sources
                var serviceBusConnectionString = ServiceBusConnectionString;
                var topicName = TopicName;
                var subscriptionName = Subscription;

                // Check if any of the required configuration values are missing
                if (string.IsNullOrWhiteSpace(serviceBusConnectionString))
                {
                    throw new ArgumentException("The Azure Service Bus connection string is missing or empty.");
                }
                if (string.IsNullOrWhiteSpace(topicName))
                {
                    throw new ArgumentException("The Azure Service Bus topic name is missing or empty.");
                }
                if (string.IsNullOrWhiteSpace(subscriptionName))
                {
                    throw new ArgumentException("The Azure Service Bus subscription name is missing or empty.");
                }

                // Initialize the subscription client using the values from the configuration
                _subscriptionClient = new SubscriptionClient(serviceBusConnectionString, topicName, subscriptionName);

                // Register message handler to process messages from the subscription
                _subscriptionClient.RegisterMessageHandler(
                    async (message, token) =>
                    {
                        try
                        {
                            var messageBody = Encoding.UTF8.GetString(message.Body);
                            _logger.LogInformation($"Received message: {messageBody}");

                            // Process payment and update status
                            await ProcessPayment(messageBody);

                            // Mark the message as complete
                            await _subscriptionClient.CompleteAsync(message.SystemProperties.LockToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error processing message: {ex.Message}");
                        }
                    },
                    new MessageHandlerOptions(args => Task.CompletedTask) { MaxConcurrentCalls = 1, AutoComplete = false }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in Service Bus Processor: {ex.Message}");
            }
        }

        private async Task ProcessPayment(string messageBody)
        {
            try
            {
                // Deserialize payment message from the Service Bus
                var paymentMessage = System.Text.Json.JsonSerializer.Deserialize<PaymentMessage>(messageBody);

                if (paymentMessage == null)
                {
                    _logger.LogError("Failed to deserialize payment message.");
                    return;
                }

                _logger.LogInformation($"Processing payment for container {paymentMessage.ContainerNumber} with amount {paymentMessage.Amount}");

                // Simulate async payment processing (e.g., interact with payment gateway)
                await Task.Delay(1000);  // Simulating delay for payment processing

                // Retrieve payment request from the database
                var paymentRequest = await _context.PaymentRequest.FirstOrDefaultAsync(pr => pr.Id == paymentMessage.Id);

                if (paymentRequest != null)
                {
                    _logger.LogInformation($"Current status of PaymentRequest {paymentRequest.Id}: {paymentRequest.Status}");

                    // Update status to 'Paid' after successful payment
                    paymentRequest.Status = "Paid";  // Set status to 'Paid'

                    // Log before update
                    _logger.LogInformation($"Updating status for PaymentRequest {paymentRequest.Id}");

                    // Ensure the database update happens correctly
                    _context.PaymentRequest.Update(paymentRequest);
                    await _context.SaveChangesAsync();  // Save changes to the database

                    // Log after update
                    _logger.LogInformation($"Payment for container {paymentMessage.ContainerNumber} processed successfully. Status updated to 'Paid'.");
                }
                else
                {
                    _logger.LogWarning($"Payment request with ID {paymentMessage.Id} not found in the database.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing payment: {ex.Message}");
            }
        }
    }
}
