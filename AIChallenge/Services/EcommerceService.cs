using AIChallenge.Models;
using AIChallenge.Repositories;

namespace AIChallenge.Services;

public sealed class EcommerceService : IEcommerceService
{
    private static readonly decimal MaxOrderTotal = 5000.00m;

    private readonly IEcommerceRepository _repository;
    private readonly IPurchaseLogger _purchaseLogger;
    private readonly TimeProvider _timeProvider;

    public EcommerceService(IEcommerceRepository repository, IPurchaseLogger purchaseLogger, TimeProvider timeProvider)
    {
        _repository = repository;
        _purchaseLogger = purchaseLogger;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResult<Customer>> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken cancellationToken = default)
    {
        AppData data = await _repository.ReadAsync(cancellationToken);
        string normalizedCurp = Validators.Normalize(request.Curp);

        if (data.Customers.Any(customer => string.Equals(customer.Curp, normalizedCurp, StringComparison.OrdinalIgnoreCase)))
        {
            return ApiResult<Customer>.Fail(ErrorCodes.DuplicateCustomer, "A customer with the same CURP already exists.");
        }

        if (!Validators.IsValidCurp(normalizedCurp))
        {
            return ApiResult<Customer>.Fail(ErrorCodes.InvalidCurp, "The CURP structure is invalid.");
        }

        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        if (!Validators.IsAdult(request.BirthDate, today))
        {
            return ApiResult<Customer>.Fail(ErrorCodes.UnderAge, "The customer must be at least 18 years old.");
        }

        if (!Validators.IsKnownAddress(request.Address, data.AddressCatalog))
        {
            return ApiResult<Customer>.Fail(ErrorCodes.InvalidAddress, "The neighborhood, postal code, municipality, and state combination is not supported.");
        }

        Customer customer = new(
            $"CUS-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            request.FullName.Trim(),
            normalizedCurp,
            request.BirthDate,
            request.Address,
            _timeProvider.GetUtcNow());

        data.Customers.Add(customer);
        await _repository.WriteAsync(data, cancellationToken);
        return ApiResult<Customer>.Ok(customer);
    }

    public async Task<ApiResult<PaymentMethod>> RegisterPaymentMethodAsync(RegisterPaymentMethodRequest request, CancellationToken cancellationToken = default)
    {
        AppData data = await _repository.ReadAsync(cancellationToken);
        if (data.Customers.All(customer => customer.Id != request.CustomerId))
        {
            return ApiResult<PaymentMethod>.Fail(ErrorCodes.CustomerNotFound, "The customer does not exist.");
        }

        if (!Validators.IsValidCardNumber(request.CardNumber))
        {
            return ApiResult<PaymentMethod>.Fail(ErrorCodes.InvalidCardNumber, "The card number is invalid.");
        }

        if (!Validators.MatchesCardType(request.CardNumber, request.CardType))
        {
            return ApiResult<PaymentMethod>.Fail(ErrorCodes.InvalidCardBrand, "The card number does not match the selected card type.");
        }

        if (!Validators.IsValidExpiration(request.Expiration))
        {
            return ApiResult<PaymentMethod>.Fail(ErrorCodes.InvalidExpiration, "The card expiration must use MM/yy and must not be expired.");
        }

        if (!Validators.IsValidCvv(request.Cvv, request.CardType))
        {
            return ApiResult<PaymentMethod>.Fail(ErrorCodes.InvalidCvv, "The CVV format is invalid for the selected card type.");
        }

        string fingerprint = Validators.FingerprintCardNumber(request.CardNumber);
        if (data.PaymentMethods.Any(payment => payment.CustomerId == request.CustomerId && payment.CardFingerprint == fingerprint))
        {
            return ApiResult<PaymentMethod>.Fail(ErrorCodes.DuplicatePaymentMethod, "The payment method is already registered for this customer.");
        }

        PaymentMethod paymentMethod = new(
            $"PAY-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            request.CustomerId,
            Validators.MaskCardNumber(request.CardNumber),
            fingerprint,
            request.CardType,
            request.CardholderName.Trim(),
            request.Expiration,
            _timeProvider.GetUtcNow());

        data.PaymentMethods.Add(paymentMethod);
        await _repository.WriteAsync(data, cancellationToken);
        return ApiResult<PaymentMethod>.Ok(paymentMethod);
    }

    public async Task<IReadOnlyList<Product>> ListProductsAsync(CancellationToken cancellationToken = default)
    {
        AppData data = await _repository.ReadAsync(cancellationToken);
        return data.Products;
    }

    public async Task<IReadOnlyList<Address>> ListAddressesByPostalCodeAsync(string postalCode, CancellationToken cancellationToken = default)
    {
        AppData data = await _repository.ReadAsync(cancellationToken);
        return data.AddressCatalog
            .Where(address => string.Equals(address.PostalCode, postalCode, StringComparison.OrdinalIgnoreCase))
            .GroupBy(address => $"{address.Neighborhood}|{address.Municipality}|{address.State}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(address => address.Neighborhood)
            .ToList();
    }

    public async Task<ApiResult<PurchaseOrder>> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        AppData data = await _repository.ReadAsync(cancellationToken);
        string authorizationCode = CreateAuthorizationCode();
        List<string> requestedSkus = request.Products.Select(product => product.Sku).ToList();

        ApiResult<PurchaseOrder>? validationFailure = await ValidateOrderRequestAsync(data, request, authorizationCode, requestedSkus, cancellationToken);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        List<OrderItem> orderItems = CreateOrderItems(data.Products, request.Products);
        decimal total = orderItems.Sum(item => item.UnitPrice * item.Quantity);
        if (total > MaxOrderTotal)
        {
            return await RejectOrderAsync(data, request, total, requestedSkus, authorizationCode, ErrorCodes.OrderLimitExceeded, "Orders cannot exceed 5,000 pesos.", cancellationToken);
        }

        PurchaseOrder order = new(
            $"ORD-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            request.CustomerId,
            _timeProvider.GetUtcNow(),
            total,
            orderItems,
            authorizationCode,
            OrderStatus.Accepted,
            new OrderStatusDetail(null, ["Order accepted", "Preparing shipment"]));

        data.Orders.Add(order);
        await _repository.WriteAsync(data, cancellationToken);
        await LogAttemptAsync(request.CustomerId, request.PaymentMethodId, total, requestedSkus, true, authorizationCode, null, cancellationToken);
        return ApiResult<PurchaseOrder>.Ok(order);
    }

    public async Task<ApiResult<PurchaseOrder>> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        AppData data = await _repository.ReadAsync(cancellationToken);
        PurchaseOrder? order = data.Orders.FirstOrDefault(order => string.Equals(order.Id, orderId, StringComparison.OrdinalIgnoreCase));
        return order is null
            ? ApiResult<PurchaseOrder>.Fail(ErrorCodes.OrderNotFound, "The order does not exist.")
            : ApiResult<PurchaseOrder>.Ok(order);
    }

    public async Task<IReadOnlyList<PurchaseOrder>> ListCustomerOrdersAsync(string customerId, CancellationToken cancellationToken = default)
    {
        AppData data = await _repository.ReadAsync(cancellationToken);
        return data.Orders
            .Where(order => string.Equals(order.CustomerId, customerId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(order => order.Date)
            .ToList();
    }

    private async Task<ApiResult<PurchaseOrder>?> ValidateOrderRequestAsync(AppData data, CreateOrderRequest request, string authorizationCode, IReadOnlyList<string> requestedSkus, CancellationToken cancellationToken)
    {
        if (data.Customers.All(customer => customer.Id != request.CustomerId))
        {
            return await RejectOrderAsync(data, request, 0, requestedSkus, authorizationCode, ErrorCodes.CustomerNotFound, "The customer does not exist.", cancellationToken);
        }

        if (data.PaymentMethods.All(payment => payment.Id != request.PaymentMethodId || payment.CustomerId != request.CustomerId))
        {
            return await RejectOrderAsync(data, request, 0, requestedSkus, authorizationCode, ErrorCodes.PaymentMethodNotFound, "The payment method does not exist for the customer.", cancellationToken);
        }

        if (request.Products.Count == 0 || request.Products.Any(product => product.Quantity <= 0))
        {
            return await RejectOrderAsync(data, request, 0, requestedSkus, authorizationCode, ErrorCodes.InvalidQuantity, "Every order item must have quantity greater than zero.", cancellationToken);
        }

        HashSet<string> productSkus = data.Products.Select(product => product.Sku).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (request.Products.Any(product => !productSkus.Contains(product.Sku)))
        {
            return await RejectOrderAsync(data, request, 0, requestedSkus, authorizationCode, ErrorCodes.ProductNotFound, "One or more products do not exist.", cancellationToken);
        }

        return null;
    }

    private async Task<ApiResult<PurchaseOrder>> RejectOrderAsync(AppData data, CreateOrderRequest request, decimal total, IReadOnlyList<string> requestedSkus, string authorizationCode, string errorCode, string reason, CancellationToken cancellationToken)
    {
        PurchaseOrder order = new(
            $"ORD-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            request.CustomerId,
            _timeProvider.GetUtcNow(),
            total,
            [],
            authorizationCode,
            OrderStatus.Rejected,
            new OrderStatusDetail(reason, []));

        data.Orders.Add(order);
        await _repository.WriteAsync(data, cancellationToken);
        await LogAttemptAsync(request.CustomerId, request.PaymentMethodId, total, requestedSkus, false, authorizationCode, reason, cancellationToken);
        return ApiResult<PurchaseOrder>.Fail(errorCode, reason);
    }

    private async Task LogAttemptAsync(string customerId, string? paymentMethodId, decimal total, IReadOnlyList<string> productSkus, bool accepted, string authorizationCode, string? rejectionReason, CancellationToken cancellationToken)
    {
        PurchaseAttemptLog log = new(
            $"LOG-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            _timeProvider.GetUtcNow(),
            customerId,
            paymentMethodId,
            total,
            productSkus,
            accepted,
            authorizationCode,
            rejectionReason);

        await _purchaseLogger.LogAsync(log, cancellationToken);
    }

    private static List<OrderItem> CreateOrderItems(IReadOnlyList<Product> products, IReadOnlyList<CreateOrderItemRequest> requestedProducts)
    {
        return requestedProducts
            .GroupBy(product => product.Sku, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                Product product = products.First(product => string.Equals(product.Sku, group.Key, StringComparison.OrdinalIgnoreCase));
                int quantity = group.Sum(item => item.Quantity);
                return new OrderItem(product.Sku, product.Name, product.Price, quantity);
            })
            .ToList();
    }

    private static string CreateAuthorizationCode()
    {
        return $"SIM-{Random.Shared.Next(100000, 999999)}";
    }
}
