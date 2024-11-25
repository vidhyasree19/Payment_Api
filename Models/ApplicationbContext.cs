using EdiRetrieval.Controllers;
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<PaymentMessage> PaymentMessage { get; set; }
        public DbSet<PaymentRequest> PaymentRequest {get;set;}
    }