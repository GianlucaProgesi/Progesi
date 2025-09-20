using System;
using FluentAssertions;
using Progesi.DomainServices.Models;
using Progesi.DomainServices.Services;
using Xunit;

namespace ProgesiDomainServices.Tests
{
  public class VariableServiceTests
  {
    [Fact]
    public void Create_Get_Update_Delete_Works()
    {
      var svc = new InMemoryVariableService();

      var v = new ProgesiVariable { Name = "alpha", Type = "double", Unit = "m", Value = 1.23 };
      var saved = svc.CreateOrUpdate(v);

      saved.Id.Should().NotBe(Guid.Empty);
      svc.GetByName("alpha")!.Value.Should().Be(1.23);

      saved.Value = 4.56;
      svc.CreateOrUpdate(saved);
      svc.GetById(saved.Id)!.Value.Should().Be(4.56);

      svc.Delete(saved.Id).Should().BeTrue();
      svc.GetById(saved.Id).Should().BeNull();
    }
  }
}
