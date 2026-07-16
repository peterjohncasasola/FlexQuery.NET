using FlexQuery.NET.Tests.Shared.Fixtures;

namespace FlexQuery.NET.Tests.Shared.Fixtures;

public static class SampleData
{
    public static void Seed(SharedTestDbContext ctx)
    {
        if (ctx.Customers.Any()) return;

            var customers = new List<Customer>();
            var profiles = new List<Profile>();
            var orders = new List<Order>();
            var orderItems = new List<OrderItem>();

            var alice = new Customer()
            {
                Id = 1,
                Name = "Alice Johnson",
                Age = 30,
                City = "New York",
                Email = "alice@example.com",
                CreatedAt = new DateTime(2023, 1, 1),
                Status = nameof(Status.Active),
                Orders = []
            };
            var aliceProfile = new Profile { Id = 1, Bio = "Developer" };
            alice.Profile = aliceProfile;
            profiles.Add(aliceProfile);

            var order1 = new Order { Id = 10001, Total = 150.0m, Status = "Shipped", Number = "SO-001", OrderItems = [] };
            orderItems.AddRange([
                new OrderItem { Id = 1, Quantity = 2, Price = 25.0m, Sku = "SKU-AAA" },
                new OrderItem { Id = 2, Quantity = 1, Price = 10.0m, Sku = "SKU-BBB" }
            ]);
            order1.OrderItems.AddRange(orderItems);
            orders.Add(order1);

            var order2 = new Order { Id = 10002, Total = 25.0m, Status = "Pending", Number = "SO-002", OrderItems = [] };
            var orderItem3 = new OrderItem { Id = 3, Quantity = 3, Price = 5.0m, Sku = "SKU-CCC" };
            order2.OrderItems.Add(orderItem3);
            orders.Add(order2);

            alice.Orders.AddRange([order1, order2]);

            var bob = new Customer()
            {
                Id = 2,
                Name = "Bob Smith",
                Age = 25,
                City = "London",
                Email = "bob@example.com",
                CreatedAt = new DateTime(2023, 2, 1),
                Status = nameof(Status.Inactive),
                Orders = []
            };
            var bobProfile = new Profile { Id = 2, Bio = "Designer" };
            bob.Profile = bobProfile;
            profiles.Add(bobProfile);
            var order3 = new Order { Id = 10003, Total = 200.0m, Status = "Delivered", OrderItems = [] };
            orders.Add(order3);
            bob.Orders.Add(order3);

            var carol = new Customer
            {
                Id = 3,
                Name = "Carol White",
                Age = 35,
                City = "New York",
                CreatedAt = new DateTime(2023, 3, 1),
                Status = nameof(Status.Pending),
                Profile = new Profile { Id = 3, Bio = "Manager" },
                Orders = []
            };
            profiles.Add(carol.Profile!);

            var order4 = new Order { Id = 10004, Total = 300.0m, Status = "Cancelled", OrderItems = [] };
            orders.Add(order4);
            carol.Orders.Add(order4);

            var david = new Customer
            {
                Id = 4,
                Name = "David Brown",
                Age = 28,
                City = "Paris",
                CreatedAt = new DateTime(2023, 4, 1),
                Status = nameof(Status.Active),
                Profile = null,
                Orders = []
            };

            var eve = new Customer
            {
                Id = 5,
                Name = "Eve Davis",
                Age = 22,
                City = "London",
                CreatedAt = new DateTime(2023, 5, 1),
                Status = nameof(Status.Inactive),
                Orders = []
            };

            var frank = new Customer
            {
                Id = 6,
                Name = "Frank Miller",
                Age = 40,
                City = "Berlin",
                CreatedAt = new DateTime(2023, 6, 1),
                Status = nameof(Status.Active),
                Orders = []
            };

            var grace = new Customer
            {
                Id = 7,
                Name = "Grace Wilson",
                Age = 19,
                City = "Paris",
                CreatedAt = new DateTime(2023, 7, 1),
                Status = nameof(Status.Pending),
                Orders = []
            };

            var hank = new Customer
            {
                Id = 8,
                Name = "Hank Moore",
                Age = 45,
                City = "New York",
                CreatedAt = new DateTime(2023, 8, 1),
                Status = nameof(Status.Active),
                Orders = []
            };

            var ivy = new Customer
            {
                Id = 9,
                Name = "Ivy Taylor",
                Age = 33,
                City = "Berlin",
                CreatedAt = new DateTime(2023, 9, 1),
                Status = nameof(Status.Inactive),
                Orders = []
            };

            var jack = new Customer
            {
                Id = 10,
                Name = "Jack Anderson",
                Email = "bob2@example.com",
                Age = 27,
                City = "London",
                CreatedAt = new DateTime(2023, 10, 1),
                Status = nameof(Status.Active),
                Orders = []
            };

            ctx.Customers.AddRange(alice, bob, carol, david, eve, frank, grace, hank, ivy, jack);
            ctx.Profiles.AddRange(profiles);
            ctx.Orders.AddRange(orders);
            ctx.OrderItems.AddRange(orderItems);
            ctx.SaveChanges();
    }
}
