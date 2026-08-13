#nullable disable
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Progesi.LiveDataExchange.Cloud;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.Cloud;
using ProgesiRepositories.Rhino;
using Rhino;

namespace ProgesiGrasshopperAssembly.Components
{
  /// <summary>
  /// Push/Pull Rhino store ↔ Progesi cloud API.
  /// The optional <c>TestRole</c> input is dev-only: it sets the <c>X-Test-Roles</c> header
  /// for local Development APIs with <c>UseTestAuthentication=true</c> (A3.2/A3.3 hardening).
  /// Leave empty in production; use <c>Token</c> (Bearer) for real Entra auth instead.
  /// </summary>
  public sealed class ProgesiCloudSyncComponent : GH_Component
  {
    public ProgesiCloudSyncComponent()
      : base("ProgesiData-CloudSync", "CloudSync",
             "Push/Pull Rhino store ↔ Progesi cloud API with ContentHash conflict detection.",
             "Progesi", "DataEx — Cloud")
    {
    }

    public override Guid ComponentGuid => new Guid("A4F31C2E-6D0B-4E91-9C2A-7E5D4B839016");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Execute sync when true.", GH_ParamAccess.item, false);
      p.AddTextParameter("Direction", "Dir", "Push or Pull.", GH_ParamAccess.item, "Push");
      p.AddTextParameter("ApiBaseUrl", "Url", "Base URL of Progesi.Api (e.g. https://localhost:5001).", GH_ParamAccess.item, "https://localhost:5001");
      p.AddTextParameter("ProjectId", "Proj", "Cloud project id (X-Project-Id).", GH_ParamAccess.item, "default");
      p.AddTextParameter("Token", "Token", "Bearer token for cloud auth.", GH_ParamAccess.item, "");
      p.AddTextParameter("TestRole", "TestRole", "Dev-only: X-Test-Roles value, e.g. writer. Leave empty in production.", GH_ParamAccess.item, "");
      for (int i = 1; i < Params.Input.Count; i++)
        Params.Input[i].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("Summary", "Sum", "Applied/skipped/conflict counts.", GH_ParamAccess.item);
      p.AddTextParameter("Conflicts", "Conf", "Reported conflicts (type,id,local,cloud).", GH_ParamAccess.item);
      p.AddTextParameter("Log", "Log", "Sync log lines.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      bool run = false;
      string directionText = "Push";
      string apiBaseUrl = "https://localhost:5001";
      string projectId = "default";
      string token = string.Empty;
      string testRole = string.Empty;

      da.GetData(0, ref run);
      da.GetData(1, ref directionText);
      da.GetData(2, ref apiBaseUrl);
      da.GetData(3, ref projectId);
      da.GetData(4, ref token);
      da.GetData(5, ref testRole);

      if (!run)
      {
        da.SetData(0, "Idle");
        da.SetData(1, string.Empty);
        da.SetData(2, string.Empty);
        return;
      }

      try
      {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null)
        {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "RhinoDoc.ActiveDoc is null.");
          da.SetData(0, "Error");
          da.SetData(1, string.Empty);
          da.SetData(2, "RhinoDoc.ActiveDoc is null.");
          return;
        }

        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "ApiBaseUrl is required.");
          da.SetData(0, "Error");
          return;
        }

        if (string.IsNullOrWhiteSpace(projectId))
        {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "ProjectId is required.");
          da.SetData(0, "Error");
          return;
        }

        var direction = ParseDirection(directionText);
        var local = ReadLocalSnapshot(doc);
        var syncState = new RhinoSyncStateStore(doc);
        var localApplier = new RhinoCloudSyncLocalApplier(doc);
        var engine = new CloudSyncEngine();

        using (var client = new HttpProgesiCloudClient(new ProgesiCloudClientOptions
        {
          BaseUrl = apiBaseUrl.Trim(),
          ProjectId = projectId.Trim(),
          BearerToken = token ?? string.Empty,
          TestRoles = testRole ?? string.Empty
        }))
        {
          var cloud = client.GetCloudSnapshotAsync(CancellationToken.None).GetAwaiter().GetResult();
          var result = engine.ExecuteAsync(
              direction,
              local,
              cloud,
              syncState,
              client,
              localApplier,
              CancellationToken.None).GetAwaiter().GetResult();

          if (result.Conflicts.Count > 0)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, result.Conflicts.Count + " conflict(s) detected.");

          da.SetData(0, FormatSummary(result));
          da.SetData(1, FormatConflicts(result));
          da.SetData(2, string.Join(Environment.NewLine, result.Log ?? Array.Empty<string>()));
        }
      }
      catch (Exception ex)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
        da.SetData(0, "Error");
        da.SetData(1, string.Empty);
        da.SetData(2, ex.ToString());
      }
    }

    private static CloudSyncDirection ParseDirection(string directionText)
    {
      if (string.Equals(directionText?.Trim(), "Pull", StringComparison.OrdinalIgnoreCase))
        return CloudSyncDirection.Pull;

      return CloudSyncDirection.Push;
    }

    private static CloudSnapshot ReadLocalSnapshot(RhinoDoc doc)
    {
      var varRepo = new RhinoVariableRepository(doc);
      var metaRepo = new RhinoMetadataRepository(doc);
      var clusterRepo = new RhinoVariableClusterRepository(doc);

      var variables = varRepo.GetAllAsync().GetAwaiter().GetResult();
      var metadata = metaRepo.ListAsync(0, 100000).GetAwaiter().GetResult();
      var clusters = clusterRepo.GetAllAsync().GetAwaiter().GetResult();

      return CloudSnapshotMapper.FromDomain(variables, metadata, clusters);
    }

    private static string FormatSummary(CloudSyncResult result)
    {
      return "vars=" + result.VariablesApplied
          + ", meta=" + result.MetadataApplied
          + ", clusters=" + result.ClustersApplied
          + ", skipped=" + result.Skipped
          + ", conflicts=" + result.Conflicts.Count;
    }

    private static string FormatConflicts(CloudSyncResult result)
    {
      if (result.Conflicts == null || result.Conflicts.Count == 0)
        return string.Empty;

      var sb = new StringBuilder();
      foreach (var conflict in result.Conflicts)
      {
        sb.Append(conflict.ObjectType)
          .Append(' ')
          .Append(conflict.Id)
          .Append(": local=")
          .Append(conflict.LocalHash)
          .Append(" cloud=")
          .Append(conflict.CloudHash)
          .AppendLine();
      }

      return sb.ToString().TrimEnd();
    }
  }
}
