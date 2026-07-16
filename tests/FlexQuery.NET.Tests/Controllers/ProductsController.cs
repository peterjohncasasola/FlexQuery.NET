using System.Data;
using System.Data.Common;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlexQuery.NET.Tests.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(IDbConnection connection) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
    {
        var model = SharedFlexQueryModel.Instance;
        var result = await ((DbConnection)connection).FlexQueryAsync<OrderItem>(parameters, opt =>
        {
            opt.UseModel(model);
        });
        return Ok(result);
    }
}