namespace AIChallenge.Models;

public sealed class AppData
{
    public List<Customer> Customers { get; set; } = [];

    public List<PaymentMethod> PaymentMethods { get; set; } = [];

    public List<Product> Products { get; set; } = [];

    public List<Address> AddressCatalog { get; set; } = [];

    public List<PurchaseOrder> Orders { get; set; } = [];
}
