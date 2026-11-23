using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;

namespace Taktos.API.Controllers;

public sealed class FalhasSqlFilter : IExceptionFilter
{
    public void OnException(ExceptionContext contexto)
    {
        if (contexto.Exception is not SqlException erro ||
            erro.Number is not (50002 or 50003 or 50004))
        {
            return;
        }

        var status = erro.Number switch { 50002 => 404, 50003 => 409, _ => 400 };
        contexto.Result = new ObjectResult(new { mensagem = erro.Message }) { StatusCode = status };
        contexto.ExceptionHandled = true;
    }
}
