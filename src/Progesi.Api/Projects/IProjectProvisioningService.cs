namespace Progesi.Api.Projects;

public interface IProjectProvisioningService
{
  ProjectEntry Provision(string name);
}
