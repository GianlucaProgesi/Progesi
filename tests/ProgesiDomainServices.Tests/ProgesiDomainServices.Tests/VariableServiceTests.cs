using FluentAssertions;
using Progesi.DomainServices.Services;
using Progesi.DomainServices.Models;
using System;
using Xunit;

namespace Progesi.DomainServices.Tests
{
  public class VariableServiceTests
  {
    [Fact]
    public void Create_Get_Update_Delete_Works()
    {
      var svc = new InMemoryVariableService();
      var created = svc.CreateOrUpdate(new ProgesiVariable
      {
        Name = "T1",
        Type = "real",
        Unit = "m",
        Value = 1.23
      });
      var read = svc.GetById(created.Id);
      Assert.Equal("T1", read.Name);

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
