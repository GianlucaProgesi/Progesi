using System;
using FluentAssertions;
using Progesi.DomainServices.Models;
using Progesi.DomainServices.Services;
using Xunit;

namespace Progesi.DomainServices.Tests
{
  public class VariableServiceNameRequiredTests
  {
    [Fact]
    public void CreateOrUpdate_WithoutName_Throws()
    {
      var svc = new InMemoryVariableService();
      var v = new ProgesiVariable { Name = "   ", Type = "double", Unit = "m", Value = 1.23 };

      Action act = () => svc.CreateOrUpdate(v);
      act.Should().Throw<ArgumentException>()
         .WithMessage("*Name is required*");
    }
  }
}
