using FlexQuery.Benchmarks.Infrastructure.Database;
using FlexQuery.Benchmarks.Models;

namespace FlexQuery.Benchmarks.Infrastructure.Seed;

/// <summary>
/// Seeds realistic data into the benchmark database.
/// 
/// Dataset philosophy:
/// - Small (100 users): warm-up, quick iteration
/// - Medium (1,000 users): realistic API scenario
/// - Large (10,000 users): stress/allocation analysis
///
/// Each user has 0–5 orders, each order has 1–3 items and 0–2 payments.
/// This creates a realistic cardinality for JOIN and EXISTS benchmarks.
/// </summary>
public static class DataSeeder
{
    private static readonly string[] Cities = { "London", "Berlin", "Paris", "Tokyo", "New York", "Sydney", "Toronto", "Mumbai" };
    private static readonly string[] Statuses = { "active", "inactive", "suspended" };
    private static readonly string[] OrderStatuses = { "pending", "completed", "cancelled", "shipped" };
    private static readonly string[] PaymentMethods = { "card", "paypal", "bank_transfer", "crypto" };
    private static readonly string[] Categories = { "Electronics", "Books", "Clothing", "Food", "Sports" };
    private static readonly string[] ProductNames = { "Widget", "Gadget", "Thingamajig", "Doohickey", "Gizmo" };

    public static void Seed(BenchmarkDbContext db, int userCount = 1000)
    {
        var rng = new Random(42); // deterministic for reproducibility

        // Products (shared across all orders)
        var products = new List<Product>();
        for (int p = 1; p <= 50; p++)
        {
            products.Add(new Product
            {
                Name = $"{ProductNames[rng.Next(ProductNames.Length)]} {p}",
                Price = Math.Round((decimal)(rng.NextDouble() * 200 + 5), 2),
                Category = Categories[rng.Next(Categories.Length)],
                IsActive = rng.NextDouble() > 0.1
            });
        }
        db.Products.AddRange(products);
        db.SaveChanges(); // Save to get IDs

        for (int u = 1; u <= userCount; u++)
        {
            var user = new User
            {
                Name = $"User_{u}",
                Email = $"user{u}@bench.test",
                Status = Statuses[rng.Next(Statuses.Length)],
                Age = rng.Next(18, 65),
                City = Cities[rng.Next(Cities.Length)],
                CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(1, 730))
            };

            int orderCount = rng.Next(0, 6);
            for (int o = 0; o < orderCount; o++)
            {
                var order = new Order
                {
                    Total = Math.Round((decimal)(rng.NextDouble() * 2000 + 10), 2),
                    Status = OrderStatuses[rng.Next(OrderStatuses.Length)],
                    OrderDate = DateTime.UtcNow.AddDays(-rng.Next(1, 365))
                };

                int itemCount = rng.Next(1, 4);
                for (int i = 0; i < itemCount; i++)
                {
                    var product = products[rng.Next(products.Count)];
                    order.Items.Add(new OrderItem
                    {
                        Product = product,
                        Quantity = rng.Next(1, 5),
                        UnitPrice = product.Price
                    });
                }

                int paymentCount = rng.Next(0, 3);
                for (int p = 0; p < paymentCount; p++)
                {
                    order.Payments.Add(new Payment
                    {
                        Amount = Math.Round(order.Total / (paymentCount > 0 ? paymentCount : 1), 2),
                        Method = PaymentMethods[rng.Next(PaymentMethods.Length)],
                        PaidAt = order.OrderDate.AddDays(rng.Next(0, 7))
                    });
                }

                user.Orders.Add(order);
            }

            db.Users.Add(user);

            if (u % 1000 == 0)
            {
                db.SaveChanges();
                db.ChangeTracker.Clear();
                // Re-attach products so they are tracked as existing entities
                foreach (var p in products)
                {
                    db.Products.Attach(p);
                }
                Console.WriteLine($"Seeded {u} users...");
            }
        }

        try
        {
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Seeding failed: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"Inner: {ex.InnerException.Message}");
            throw;
        }
    }
}
