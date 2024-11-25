public class PaymentMessage
    {
        public string Id { get; set; }
        public decimal Amount { get; set; }
        public string ContainerNumber { get; set; }
        public DateTime Timestamp { get; set; }
    }