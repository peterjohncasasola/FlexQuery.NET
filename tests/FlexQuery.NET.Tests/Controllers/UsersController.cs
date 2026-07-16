using System.Data;
using System.Data.Common;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Configuration;
using FlexQuery.NET.Dapper.Metadata;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlexQuery.NET.Tests.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(IDbConnection connection) : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health() => Ok("Healthy");

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
    {
        try
        {
            var model = SharedFlexQueryModel.Instance;
            var result = await ((DbConnection)connection).FlexQueryAsync<Customer>(parameters, opt =>
            {
                opt.UseModel(model);
            });
            return Ok(result);
        }
        catch (QueryValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (QueryParseException ex)
        {
            return BadRequest(ex.Message);
        }
    }
    
}