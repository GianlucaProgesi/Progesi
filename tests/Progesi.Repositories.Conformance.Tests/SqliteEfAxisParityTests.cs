using FluentAssertions;
using ProgesiCore;
using Progesi.Repositories.Conformance.Tests.Support;

namespace Progesi.Repositories.Conformance.Tests;

public sealed class SqliteEfAxisParityTests
{
  private static ProgesiAxisVariable MakeRichAxis(int id)
  {
    var fn = new ProgesiFunction(1, "law", new[]
    {
      new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Constant, constantValue: 1.0)
    });

    var axis = new ProgesiAxisVariable(
      id,
      "Axis-Parity",
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
  public async Task SaveAndRead_Axis_Produces_Equivalent_Results()
  {
    using var stores = new SqliteEfAxisParityStores();
    var original = MakeRichAxis(7);

    await stores.Sqlite.SaveAsync(original);
    await stores.Ef.SaveAsync(original);

    var sqliteLoaded = await stores.Sqlite.GetByIdAsync(7);
    var efLoaded = await stores.Ef.GetByIdAsync(7);

    ParityAssertions.AxisShouldMatch(sqliteLoaded!, efLoaded!, original);
  }

  [Fact]
  public async Task Save_Deduplicates_ContentDuplicate_With_Identical_Behaviour()
  {
    using var stores = new SqliteEfAxisParityStores();
    var a1 = MakeRichAxis(1);
    var a2 = MakeRichAxis(2);

    await stores.Sqlite.SaveAsync(a1);
    await stores.Ef.SaveAsync(a1);

    var sqliteDeduped = await stores.Sqlite.SaveAsync(a2);
    var efDeduped = await stores.Ef.SaveAsync(a2);

    sqliteDeduped.Id.Should().Be(1);
    efDeduped.Id.Should().Be(1);
    sqliteDeduped.Id.Should().Be(efDeduped.Id);
    sqliteDeduped.Hashtag.Should().Be(efDeduped.Hashtag);

    var sqliteByTag = await stores.Sqlite.GetByHashtagAsync(a1.Hashtag);
    var efByTag = await stores.Ef.GetByHashtagAsync(a1.Hashtag);

    ParityAssertions.AxisShouldMatch(sqliteByTag!, efByTag!, a1);
  }

  [Fact]
  public async Task SaveAndRead_ValueCurve_Axis_Produces_Equivalent_Results()
  {
    using var stores = new SqliteEfAxisParityStores();
    var original = MakeValueCurveAxis(11);

    await stores.Sqlite.SaveAsync(original);
    await stores.Ef.SaveAsync(original);

    var sqliteLoaded = await stores.Sqlite.GetByIdAsync(11);
    var efLoaded = await stores.Ef.GetByIdAsync(11);

    ParityAssertions.AxisShouldMatch(sqliteLoaded!, efLoaded!, original);
  }

  private static ProgesiAxisVariable MakeValueCurveAxis(int id)
  {
    var nurbs = new ProgesiNurbsPayload(
      1,
      new[] { (0.0, 0.0), (1.0, 2.0) },
      new[] { 1.0, 1.0 },
      new[] { 0.0, 0.0, 1.0, 1.0 });

    var fn = new ProgesiFunction(1, "parity-curve", new[]
    {
      new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Nurbs, nurbs: nurbs)
    });

    var axis = new ProgesiAxisVariable(
      id,
      "Axis-ValueCurve",
      "Thickness",
      "System.Double",
      50.0,
      ruleId: 7,
      curvePayload: "curve-vc",
      AxisCurveMode.PlanXY,
      new[] { 0.0, 1.0 },
      ProgesiFunctionRef.Embed(fn));

    axis.SetLabel(0.0, "A");
    var sigLeft = new ProgesiAxisVariable.ProgesiVariableSignature(3, "Thickness", "System.Double");
    var sigRight = new ProgesiAxisVariable.ProgesiVariableSignature(4, "Thickness", "System.Double");
    axis.Add(sigLeft, 0.5, ProgesiAxisStationSide.Left);
    axis.Add(sigRight, 0.5, ProgesiAxisStationSide.Right);
    return axis;
  }

  [Fact]
  public async Task GetByHashtagAsync_Returns_Equivalent_Axis_On_Both_Providers()
  {
    using var stores = new SqliteEfAxisParityStores();
    var original = MakeRichAxis(3);

    await stores.Sqlite.SaveAsync(original);
    await stores.Ef.SaveAsync(original);

    var sqliteLoaded = await stores.Sqlite.GetByHashtagAsync(original.Hashtag);
    var efLoaded = await stores.Ef.GetByHashtagAsync(original.Hashtag);

    ParityAssertions.AxisShouldMatch(sqliteLoaded!, efLoaded!, original);
  }
}
