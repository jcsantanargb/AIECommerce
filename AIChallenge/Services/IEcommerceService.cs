using AIChallenge.Models;

namespace AIChallenge.Services;

public interface IEcommerceService
{
    Task<ApiResult<Customer>> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken cancellationToken = default);

    Task<ApiResult<PaymentMethod>> RegisterPaymentMethodAsync(RegisterPaymentMethodRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> ListProductsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Address>> ListAddressesByPostalCodeAsync(string postalCode, CancellationToken cancellationToken = default);

    Task<ApiResult<PurchaseOrder>> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);

    Task<ApiResult<PurchaseOrder>> GetOrderAsync(string orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseOrder>> ListCustomerOrdersAsync(string customerId, CancellationToken cancellationToken = default);
}
