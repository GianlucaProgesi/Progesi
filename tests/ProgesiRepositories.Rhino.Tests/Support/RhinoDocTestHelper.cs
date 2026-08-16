using System;
using Rhino;

namespace ProgesiRepositories.Rhino.Tests.Support
{
  internal static class RhinoDocTestHelper
  {
    public static RhinoDoc CreateTestDoc()
    {
      RhinoTestBootstrap.Require();
      var doc = RhinoDoc.CreateHeadless(null);
      if (doc == null)
        throw new InvalidOperationException("RhinoDoc.CreateHeadless returned null.");
      return doc;
    }
  }
}
