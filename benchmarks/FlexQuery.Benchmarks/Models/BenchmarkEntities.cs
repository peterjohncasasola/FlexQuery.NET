using Sieve.Attributes;

namespace FlexQuery.Benchmarks.Models;

/// <summary>
/// Realistic entity graph for benchmarking.
/// Mirrors a typical e-commerce domain with 1:N and N:1 relationships.
///
/// Sieve attributes are added to enable Sieve benchmarks.
/// This does not affect the other libraries — they ignore unknown attributes.
/// </summary>
public class User
{
    [Sieve(CanFilter = true, CanSort = true)]
    public int Id { get; set; }

    [Sieve(CanFilter = true, CanSort = true)]
    public string Name { get; set; } = string.Empty;

    [Sieve(CanFilter = true, CanSort = true)]
    public string Email { get; set; } = string.Empty;

    [Sieve(CanFilter = true, CanSort = true)]
    public string Status { get; set; } = "active";

    [Sieve(CanFilter = true, CanSort = true)]
    public int Age { get; set; }

    [Sieve(CanFilter = true, CanSort = true)]
    public string City { get; set; } = string.Empty;

    [Sieve(CanFilter = true, CanSort = true)]
    public DateTime CreatedAt { get; set; }

    // 1:N
    public List<Order> Orders { get; set; } = new();
}

public class Order
{
    [Sieve(CanFilter = true, CanSort = true)]
    public int Id { get; set; }

    [Sieve(CanFilter = true, CanSort = true)]
    public int UserId { get; set; }

    [Sieve(CanFilter = true, CanSort = true)]
    public decimal Total { get; set; }

    [Sieve(CanFilter = true, CanSort = true)]
    public string Status { get; set; } = "pending";

    [Sieve(CanFilter = true, CanSort = true)]
    public DateTime OrderDate { get; set; }

    // N:1
    public User User { get; set; } = null!;

    // 1:N
    public List<OrderItem> Items { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

public class Product
{
    [Sieve(CanFilter = true, CanSort = true)]
    public int Id { get; set; }

    [Sieve(CanFilter = true, CanSort = true)]
    public string Name { get; set; } = string.Empty;

    [Sieve(CanFilter = true, CanSort = true)]
    public decimal Price { get; set; }

    [Sieve(CanFilter = true, CanSort = true)]
    public string Category { get; set; } = string.Empty;

    [Sieve(CanFilter = true, CanSort = true)]
    public bool IsActive { get; set; } = true;
}

public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public DateTime PaidAt { get; set; }

    public Order Order { get; set; } = null!;
}
