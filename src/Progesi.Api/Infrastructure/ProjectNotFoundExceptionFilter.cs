using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Progesi.Api.Projects;

namespace Progesi.Api.Infrastructure;

public sealed class ProjectNotFoundExceptionFilter : IExceptionFilter
{
  public void OnException(ExceptionContext context)
  {
    if (context.Exception is not ProjectNotFoundException ex)
      return;

    context.Result = new NotFoundObjectResult(ex.Message);
    context.ExceptionHandled = true;
  }
}
