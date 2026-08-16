using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using ProgesiCore;
using ProgesiRepositories.Rhino;
using ProgesiRepositories.Rhino.Tests.Support;
using Rhino;
using Xunit;

namespace ProgesiRepositories.Rhino.Tests
{
  public sealed class RhinoAxisVariableRepositoryTests : IDisposable
  {
    private readonly RhinoDoc _doc;
    private readonly RhinoAxisVariableRepository _repo;

    public RhinoAxisVariableRepositoryTests()
    {
      RhinoTestBootstrap.Require();
      _doc = RhinoDocTestHelper.CreateTestDoc();
      _repo = new RhinoAxisVariableRepository(_doc);
    }

    private static ProgesiAxisVariable MakeRichAxis(int id)
    {
      var fn = new ProgesiFunction(1, "law", new[]
      {
        new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Constant, constantValue: 1.0)
      });

      var axis = new ProgesiAxisVariable(
        id,
        "Axis-Repo",
        "Thickness",
        "System.Double",
        100.0,
        99,
        "curve-json-v1",
        AxisCurveMode.PlanXY,
        new[] { 0.0, 0.5, 1.0 },
        ProgesiFunctionRef.Embed(fn));

      var sig = new ProgesiAxisVariable.ProgesiVariableSignature(2, "Thickness", "System.Double");
      axis.Add(sig, 0.25);
      axis.Add(sig, 0.75);
      return axis;
    }

    [Fact]
    public async Task SaveAsync_Then_GetByIdAsync_RoundTrips_Axis()
    {
      var axis = MakeRichAxis(7);
      await _repo.SaveAsync(axis);
      var loaded = await _repo.GetByIdAsync(7);

      loaded.Should().NotBeNull();
      loaded!.Id.Should().Be(7);
      loaded.AxisName.Should().Be("Axis-Repo");
      loaded.Mode.Should().Be(AxisCurveMode.PlanXY);
      ProgesiHash.Compute(loaded).Should().Be(ProgesiHash.Compute(axis));
    }

    [Fact]
    public async Task SaveAsync_Deduplicates_By_ContentHash()
    {
      await _repo.SaveAsync(MakeRichAxis(1));
      var second = MakeRichAxis(2);
      var returned = await _repo.SaveAsync(second);

      returned.Id.Should().Be(1);
      (await _repo.GetAllAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteAsync_Removes_Existing_Axis()
    {
      await _repo.SaveAsync(MakeRichAxis(4));
      (await _repo.DeleteAsync(4)).Should().BeTrue();
      (await _repo.GetByIdAsync(4)).Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_RoundTrips_Labels_And_Side()
    {
      var nurbs = new ProgesiNurbsPayload(
        1,
        new[] { (0.0, 1.0), (1.0, 3.0) },
        new[] { 1.0, 1.0 },
        new[] { 0.0, 0.0, 1.0, 1.0 });

      var fn = new ProgesiFunction(1, "value-curve", new[]
      {
        new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Nurbs, nurbs: nurbs)
      });

      var axis = new ProgesiAxisVariable(8, "Axis-VC", "Thickness", "System.Double", functionRef: ProgesiFunctionRef.Embed(fn));
      axis.SetLabel(0.0, "Origin");
      var sigLeft = new ProgesiAxisVariable.ProgesiVariableSignature(11, "Thickness", "System.Double");
      var sigRight = new ProgesiAxisVariable.ProgesiVariableSignature(12, "Thickness", "System.Double");
      axis.Add(sigLeft, 0.5, ProgesiAxisStationSide.Left);
      axis.Add(sigRight, 0.5, ProgesiAxisStationSide.Right);

      await _repo.SaveAsync(axis);
      var loaded = await _repo.GetByIdAsync(8);

      loaded.Should().NotBeNull();
      loaded!.GetLabel(0.0).Should().Be("Origin");
      loaded.GetAt(0.5, ProgesiAxisStationSide.Left).Should().Contain(11);
      loaded.GetAt(0.5, ProgesiAxisStationSide.Right).Should().Contain(12);
    }

    public void Dispose()
    {
      _doc?.Dispose();
    }
  }
}
