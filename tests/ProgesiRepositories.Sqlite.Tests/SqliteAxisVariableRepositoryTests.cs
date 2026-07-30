using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using ProgesiCore;
using ProgesiRepositories.Sqlite;
using Xunit;

namespace ProgesiRepositories.Sqlite.Tests
{
  public sealed class SqliteAxisVariableRepositoryTests : IDisposable
  {
    private readonly string _dbPath;
    private readonly SqliteAxisVariableRepository _repo;

    public SqliteAxisVariableRepositoryTests()
    {
      SqliteTestBootstrap.EnsureInitialized();
      _dbPath = Path.Combine(Path.GetTempPath(), $"progesi_axis_{Guid.NewGuid():N}.sqlite");
      _repo = new SqliteAxisVariableRepository(_dbPath, resetSchema: true);
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
      loaded.Name.Should().Be("Thickness");
      loaded.CurvePayload.Should().Be("curve-json-v1");
      loaded.Mode.Should().Be(AxisCurveMode.PlanXY);
      loaded.KeyPoints.Should().Equal(0.0, 0.5, 1.0);
      loaded.FunctionRef.Embedded.Should().NotBeNull();
      loaded.Hashtag.Should().Be(axis.Hashtag);
      ProgesiHash.Compute(loaded).Should().Be(ProgesiHash.Compute(axis));
    }

    [Fact]
    public async Task GetByHashtagAsync_Finds_Saved_Axis_By_Current_Hashtag()
    {
      var axis = MakeRichAxis(3);
      await _repo.SaveAsync(axis);

      var loaded = await _repo.GetByHashtagAsync(axis.Hashtag);

      loaded.Should().NotBeNull();
      loaded!.Id.Should().Be(3);
    }

    [Fact]
    public async Task GetByHashtagAsync_Finds_Saved_Axis_By_ContentHash()
    {
      var axis = MakeRichAxis(5);
      await _repo.SaveAsync(axis);

      var loaded = await _repo.GetByHashtagAsync(axis.ContentHash);

      loaded.Should().NotBeNull();
      loaded!.Id.Should().Be(5);
    }

    [Fact]
    public async Task GetByHashtagAsync_Returns_Null_For_Empty_Or_Whitespace_Hashtag()
    {
      await _repo.SaveAsync(MakeRichAxis(1));

      (await _repo.GetByHashtagAsync("")).Should().BeNull();
      (await _repo.GetByHashtagAsync("   ")).Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_Deduplicates_By_ContentHash()
    {
      var first = MakeRichAxis(1);
      await _repo.SaveAsync(first);

      var second = MakeRichAxis(2);
      var returned = await _repo.SaveAsync(second);

      returned.Id.Should().Be(1);

      var all = await _repo.GetAllAsync();
      all.Should().HaveCount(1);
      all[0].Id.Should().Be(1);
    }

    [Fact]
    public async Task GetAllAsync_Returns_Axes_Ordered_By_Id()
    {
      var a1 = MakeRichAxis(1);
      var a2 = MakeRichAxis(2);
      a2.SetCurvePayload("curve-json-v2");
      await _repo.SaveAsync(a1);
      await _repo.SaveAsync(a2);

      var all = await _repo.GetAllAsync();

      all.Should().HaveCount(2);
      all.Select(a => a.Id).Should().Equal(1, 2);
    }

    [Fact]
    public async Task DeleteAsync_Removes_Existing_Axis()
    {
      await _repo.SaveAsync(MakeRichAxis(4));

      (await _repo.DeleteAsync(4)).Should().BeTrue();
      (await _repo.GetByIdAsync(4)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_Returns_False_When_Not_Found()
    {
      (await _repo.DeleteAsync(999)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteManyAsync_Removes_Only_Existing_Ids()
    {
      var a1 = MakeRichAxis(1);
      var a2 = MakeRichAxis(2);
      a2.SetCurvePayload("curve-json-v2");
      await _repo.SaveAsync(a1);
      await _repo.SaveAsync(a2);

      var removed = await _repo.DeleteManyAsync(new[] { 1, 99, 2 });

      removed.Should().Be(2);
      (await _repo.GetAllAsync()).Should().BeEmpty();
    }

    public void Dispose()
    {
      try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }
  }
}
