using FluentAssertions;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using Xunit;

namespace ProgesiRepositories.Rhino.Tests.AxisVar
{
  public sealed class AxisVarInterpolateStepTests
  {
    [Fact]
    public void EvaluateStepValue_StringType_Returns_Nearest_Keypoint_Label()
    {
      var axis = new ProgesiAxisVariable(
        1,
        "Axis-A",
        "Material",
        "System.String",
        10.0,
        keyPoints: new[] { 0.0, 1.0 });

      axis.SetLabel(0.0, "A");
      axis.SetLabel(1.0, "B");

      AxisVarGhSupport.EvaluateStepValue(axis, 0.4).Should().Be("A");
      AxisVarGhSupport.EvaluateStepValue(axis, 0.0).Should().Be("A");
      AxisVarGhSupport.EvaluateStepValue(axis, 1.0).Should().Be("B");
      AxisVarGhSupport.EvaluateStepValue(axis, 0.999).Should().Be("A");
    }
  }
}
