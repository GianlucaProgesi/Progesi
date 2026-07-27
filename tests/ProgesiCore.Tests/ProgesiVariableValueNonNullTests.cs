using System;
using FluentAssertions;
using ProgesiCore;
using ProgesiRepositories.InMemory;
using Xunit;

namespace ProgesiCore.Tests
{
  public class ProgesiVariableValueNonNullTests
  {
    [Fact]
    public void Constructor_Throws_When_Value_Is_Null()
    {
      Action act = () => new ProgesiVariable(1, "A", null!);

      act.Should().Throw<ArgumentNullException>().WithParameterName("value");
    }

    [Fact]
    public void WithValue_Throws_When_Value_Is_Null()
    {
      var original = new ProgesiVariable(1, "A", 42);

      Action act = () => original.WithValue(null);

      act.Should().Throw<ArgumentNullException>().WithParameterName("value");
    }

    [Fact]
    public void Empty_String_Value_Is_Allowed()
    {
      var v = new ProgesiVariable(1, "Blank", "");

      v.Value.Should().Be("");
    }

    [Fact]
    public async System.Threading.Tasks.Task Empty_String_Value_RoundTrips_InMemory()
    {
      var repo = new InMemoryVariableRepository();
      var original = new ProgesiVariable(9, "Blank", "", metadataIds: new[] { 1 });

      await repo.SaveAsync(original);
      var loaded = await repo.GetByIdAsync(9);

      loaded.Should().NotBeNull();
      loaded!.Value.Should().Be("");
    }
  }
}
