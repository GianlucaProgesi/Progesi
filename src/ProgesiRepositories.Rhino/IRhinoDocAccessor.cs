using Rhino;

namespace ProgesiRepositories.Rhino
{
  public interface IRhinoDocAccessor
  {
    RhinoDoc GetActiveDoc();
  }

  public sealed class DefaultRhinoDocAccessor : IRhinoDocAccessor
  {
    public RhinoDoc GetActiveDoc() => RhinoDoc.ActiveDoc;
  }
}
