using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

ILogger<Program> logger = loggerFactory.CreateLogger<Program>();
Console.WriteLine(" Проверка данных товара ");

var badProduct = new Product { Id = 0, Name = "", Price = -50, Quantity = -3, ExpirationDate = null };
var context = new ValidationContext(badProduct);
var results = new List<ValidationResult>();
bool isValid = Validator.TryValidateObject(badProduct, context, results, validateAllProperties: true);
if (!isValid)
{
    foreach (var error in results)
        Console.WriteLine(error.ErrorMessage);
}

var service = new SaleService(logger);

Console.WriteLine();
Console.WriteLine(" Успешная продажа ");
service.MakeSale(1, 2);

Console.WriteLine();
Console.WriteLine(" Товар не найден ");
service.MakeSale(999, 1);

Console.WriteLine();
Console.WriteLine(" Срок годности истёк ");
service.MakeSale(2, 1);

Console.WriteLine();
Console.WriteLine("Не хватает товара на складе ");
service.MakeSale(3, 10);

public class Product
{
    [Range(1, int.MaxValue, ErrorMessage = "Не указан номер товара")]
    public int Id { get; set; }

    [Required(ErrorMessage = "Не указано название товара")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Название должно быть от 2 до 100 символов")]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, 1000000.0, ErrorMessage = "Цена должна быть больше нуля")]
    public decimal Price { get; set; }

    [Range(0, 100000, ErrorMessage = "Количество не может быть отрицательным")]
    public int Quantity { get; set; }

    [Required(ErrorMessage = "Не указан срок годности")]
    public DateTime? ExpirationDate { get; set; }
}
public class ProductNotFoundException : Exception
{
    public int ProductId { get; }

    public ProductNotFoundException(int productId)
        : base($"Товар {productId} не найден")
    {
        ProductId = productId;
    }
}
public class ProductExpiredException : Exception
{
    public int ProductId { get; }

    public ProductExpiredException(int productId)
        : base($"У товара {productId} истёк срок годности")
    {
        ProductId = productId;
    }
}

public class NotEnoughStockException : Exception
{
    public int ProductId { get; }
    public int Requested { get; }
    public int Available { get; }

    public NotEnoughStockException(int productId, int requested, int available)
        : base($"Товара {productId} не хватает: нужно {requested}, есть {available}")
    {
        ProductId = productId;
        Requested = requested;
        Available = available;
    }
}
public class SaleService
{
    private readonly ILogger _logger;
    private readonly Dictionary<int, Product> _products = new();

    public SaleService(ILogger logger)
    {
        _logger = logger;

        _products[1] = new Product { Id = 1, Name = "Корм для собак", Price = 3500, Quantity = 20, ExpirationDate = DateTime.Today.AddDays(180) };
        _products[2] = new Product { Id = 2, Name = "Лакомство для кошек", Price = 150, Quantity = 40, ExpirationDate = DateTime.Today.AddDays(-10) };
        _products[3] = new Product { Id = 3, Name = "Корм для рыб", Price = 220, Quantity = 2, ExpirationDate = DateTime.Today.AddDays(90) };
    }

    public void MakeSale(int productId, int quantity)
    {
        try
        {
            if (!_products.ContainsKey(productId))
                throw new ProductNotFoundException(productId);

            Product product = _products[productId];

            if (product.ExpirationDate < DateTime.Today)
                throw new ProductExpiredException(productId);

            if (product.Quantity < quantity)
                throw new NotEnoughStockException(productId, quantity, product.Quantity);

            product.Quantity -= quantity; 
            Console.WriteLine($"Продажа оформлена. Остаток товара {productId}: {product.Quantity}");
            _logger.LogInformation("Продажа оформлена: товар {ProductId}, количество {Quantity}", productId, quantity);
        }
        catch (ProductNotFoundException ex)
        {
            Console.WriteLine("Ошибка: " + ex.Message);
            _logger.LogWarning(ex, "Товар {ProductId} не найден", ex.ProductId);
        }
        catch (ProductExpiredException ex)
        {
            Console.WriteLine("Ошибка: " + ex.Message);
            _logger.LogWarning(ex, "Попытка продать просроченный товар {ProductId}", ex.ProductId);
        }
        catch (NotEnoughStockException ex)
        {
            Console.WriteLine("Ошибка: " + ex.Message);
            _logger.LogWarning(ex, "Не хватает товара {ProductId}: нужно {Requested}, есть {Available}",
                ex.ProductId, ex.Requested, ex.Available);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непредвиденная ошибка при продаже товара {ProductId}", productId);
            throw;
        }
        finally
        {
            _logger.LogInformation("Попытка продажи товара {ProductId} завершена", productId);
        }
    }
}
