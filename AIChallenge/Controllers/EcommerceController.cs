using AIChallenge.Models;
using AIChallenge.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIChallenge.Controllers;

[ApiController]
[Route("api")]
public sealed class EcommerceController : ControllerBase
{
    private readonly IEcommerceService _service;

    public EcommerceController(IEcommerceService service)
    {
        _service = service;
    }

    [HttpPost("customers", Name = "RegisterCustomer")]
    public async Task<IActionResult> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken cancellationToken)
    {
        ApiResult<Customer> result = await _service.RegisterCustomerAsync(request, cancellationToken);
        return ToHttpResult(result, StatusCodes.Status201Created);
    }

    [HttpPost("payment-methods", Name = "RegisterPaymentMethod")]
    public async Task<IActionResult> RegisterPaymentMethodAsync(RegisterPaymentMethodRequest request, CancellationToken cancellationToken)
    {
        ApiResult<PaymentMethod> result = await _service.RegisterPaymentMethodAsync(request, cancellationToken);
        return ToHttpResult(result, StatusCodes.Status201Created);
    }

    [HttpGet("products", Name = "ListProducts")]
    [ProducesResponseType<IReadOnlyList<Product>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProductsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Product> products = await _service.ListProductsAsync(cancellationToken);
        return Ok(products);
    }

    [HttpGet("address-catalog/{postalCode}", Name = "ListAddressesByPostalCode")]
    [ProducesResponseType<IReadOnlyList<Address>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAddressesByPostalCodeAsync(string postalCode, CancellationToken cancellationToken)
    {
        IReadOnlyList<Address> addresses = await _service.ListAddressesByPostalCodeAsync(postalCode, cancellationToken);
        return Ok(addresses);
    }

    [HttpPost("orders", Name = "CreateOrder")]
    public async Task<IActionResult> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        ApiResult<PurchaseOrder> result = await _service.CreateOrderAsync(request, cancellationToken);
        return ToHttpResult(result, StatusCodes.Status201Created);
    }

    [HttpGet("orders/{orderId}", Name = "GetOrder")]
    public async Task<IActionResult> GetOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        ApiResult<PurchaseOrder> result = await _service.GetOrderAsync(orderId, cancellationToken);
        return ToHttpResult(result);
    }

    [HttpGet("customers/{customerId}/orders", Name = "ListCustomerOrders")]
    public async Task<IActionResult> ListCustomerOrdersAsync(string customerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<PurchaseOrder> orders = await _service.ListCustomerOrdersAsync(customerId, cancellationToken);
        return Ok(orders);
    }

    private IActionResult ToHttpResult<T>(ApiResult<T> result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.Success)
        {
            return StatusCode(successStatusCode, result.Data);
        }

        int statusCode = result.Error?.Code switch
        {
            ErrorCodes.CustomerNotFound or ErrorCodes.PaymentMethodNotFound or ErrorCodes.ProductNotFound or ErrorCodes.OrderNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.OrderLimitExceeded => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status400BadRequest
        };

        return StatusCode(statusCode, result.Error);
    }
}
