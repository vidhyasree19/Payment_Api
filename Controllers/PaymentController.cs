using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace EdiRetrieval.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly ILogger<PaymentController> _logger;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly string _serviceBusConnectionString;
        private readonly string _topicName;
        private ITopicClient _topicClient;

        public PaymentController(ILogger<PaymentController> logger, IConfiguration configuration, ApplicationDbContext context)
        {
            _logger = logger;
            _configuration = configuration;
            _context = context;
            _serviceBusConnectionString = _configuration["AzureServiceBus:ConnectionString"];
            _topicName = _configuration["AzureServiceBus:QueueName"];
        }

        [HttpPost("initiate")]
        public async Task<IActionResult> InitiatePayment([FromBody] PaymentRequest paymentRequest)
        {
            try
            {
                if (paymentRequest == null || string.IsNullOrEmpty(paymentRequest.Id) || paymentRequest.Fees <= 0)
                {
                    _logger.LogError("Invalid payment request received.");
                    return BadRequest("Invalid payment request.");
                }

                _logger.LogInformation($"Processing payment request: {paymentRequest.Id} with amount: {paymentRequest.Fees}");

                // Check if the payment request ID already exists in the database
                var existingRequest = await _context.PaymentRequest
                                                     .FirstOrDefaultAsync(pr => pr.Id == paymentRequest.Id);

                if (existingRequest != null)
                {
                    _logger.LogError($"Payment request with ID: {paymentRequest.Id} already exists.");
                    return BadRequest("Payment request with the same ID already exists.");
                }

                // Set status to 'Pending' initially and save the payment request to the database
                paymentRequest.Status = "Pending";
                paymentRequest.DateCreated = DateTime.UtcNow;

                _context.PaymentRequest.Add(paymentRequest);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Payment request with ID: {paymentRequest.Id} saved to database.");

                // Create and send PaymentMessage to Azure Service Bus
                var paymentMessage = new PaymentMessage
                {
                    Id = paymentRequest.Id,
                    Amount = paymentRequest.Fees,
                    ContainerNumber = paymentRequest.ContainerNumber,
                    Timestamp = DateTime.UtcNow
                };

                var messageBody = System.Text.Json.JsonSerializer.Serialize(paymentMessage);
                var message = new Message(Encoding.UTF8.GetBytes(messageBody));

                _topicClient = new TopicClient(_serviceBusConnectionString, _topicName);
                await _topicClient.SendAsync(message);

                _logger.LogInformation("Payment message sent to Azure Service Bus.");

                return Ok(new { Message = "Payment initiation successful.", TransactionId = paymentRequest.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during payment initiation: {ex.Message}");
                return StatusCode(500, "An error occurred while initiating payment.");
            }
            finally
            {
                if (_topicClient != null)
                {
                    await _topicClient.CloseAsync();
                }
            }
        }

        [HttpPost("update-status/{paymentRequestId}")]
        public async Task<IActionResult> UpdatePaymentStatus(string paymentRequestId)
        {
            var paymentRequest = await _context.PaymentRequest.FirstOrDefaultAsync(pr => pr.Id == paymentRequestId);

            if (paymentRequest != null)
            {
                // Simulate payment processing and determine status
                bool paymentSuccessful = true; // This should be determined based on actual payment gateway response.

                // Update status to 'Paid' after successful payment
                paymentRequest.Status = paymentSuccessful ? "Paid" : "Failed";

                _context.PaymentRequest.Update(paymentRequest);
                await _context.SaveChangesAsync();

                // Optionally, reset fees or add other business logic after successful payment
                if (paymentSuccessful)
                {
                    // For example: Reset fees to 0 after payment is completed
                    var container = await _context.PaymentRequest
                                                  .FirstOrDefaultAsync(c => c.Id == paymentRequest.ContainerNumber);
                    if (container != null)
                    {
                        container.Fees = 0;
                        _context.PaymentRequest.Update(container);
                        await _context.SaveChangesAsync();
                    }
                }

                return Ok(new { Status = paymentRequest.Status, TransactionId = paymentRequest.Id });
            }

            return NotFound("Payment request not found.");
        }
    }
}
