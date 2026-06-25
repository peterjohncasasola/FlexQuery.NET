using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlexQuery.NET.Samples.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Samples.WebApi.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        if (await context.Customers.AnyAsync())
        {
            return; // Database already seeded
        }

        var random = new Random(42); // Stable seed for reproducibility

        var firstNames = new[] { "John", "Jane", "Michael", "Emily", "David", "Sarah", "James", "Jessica", "Robert", "Ashley", "William", "Amanda", "Joseph", "Melissa", "Charles", "Stephanie", "Thomas", "Nicole", "Daniel", "Elizabeth" };
        var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin" };
        var cities = new[] { "New York", "Los Angeles", "Chicago", "Houston", "Phoenix", "Philadelphia", "San Antonio", "San Diego", "Dallas", "San Jose" };
        var customerStatuses = new[] { "Active", "Inactive", "Pending" };
        var orderStatuses = new[] { "Completed", "Pending", "Cancelled", "Shipped" };

        var customers = new List<Customer>();
        for (int i = 1; i <= 100; i++)
        {
            var firstName = firstNames[random.Next(firstNames.Length)];
            var lastName = lastNames[random.Next(lastNames.Length)];
            var email = $"{firstName.ToLower()}.{lastName.ToLower()}{i}@example.com";
            var city = cities[random.Next(cities.Length)];
            var status = random.Next(100) < 80 ? "Active" : customerStatuses[random.Next(customerStatuses.Length)];
            var createdDate = DateTime.UtcNow.AddDays(-random.Next(365));

            var salary = Math.Round((decimal)(random.NextDouble() * 110000 + 25000), 0);

            customers.Add(new Customer
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                City = city,
                Status = status,
                Salary = salary,
                CreatedDate = createdDate
            });
        }

        context.Customers.AddRange(customers);
        await context.SaveChangesAsync(); // Save customers to generate their IDs

        var orders = new List<Order>();
        for (int i = 1; i <= 500; i++)
        {
            var customer = customers[random.Next(customers.Count)];
            var orderNumber = $"ORD-{i:D5}";
            var totalAmount = Math.Round((decimal)(random.NextDouble() * 1990 + 10), 2); // $10 to $2000
            var orderStatus = orderStatuses[random.Next(orderStatuses.Length)];
            
            // Order date should be after or equal to the customer's creation date
            var maxDaysSinceCustomerCreation = (DateTime.UtcNow - customer.CreatedDate).Days;
            var orderDate = customer.CreatedDate.AddDays(random.Next(Math.Max(1, maxDaysSinceCustomerCreation)));

            orders.Add(new Order
            {
                CustomerId = customer.Id,
                OrderNumber = orderNumber,
                TotalAmount = totalAmount,
                OrderDate = orderDate,
                Status = orderStatus
            });
        }

        context.Orders.AddRange(orders);
        await context.SaveChangesAsync();
    }
}
