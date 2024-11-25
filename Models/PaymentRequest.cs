public class PaymentRequest
    {
        public string Id { get; set; }
        public decimal Fees { get; set; }
        public string ContainerNumber { get; set; }
        public string Status{get;set;}
        public DateTime DateCreated { get; set; }
    }