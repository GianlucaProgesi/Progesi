using System;
using System.Threading.Tasks;
using Progesi.DomainServices.Interfaces;
using Progesi.DomainServices.Models;

namespace Progesi.DomainServices.Services
{
  public static class VariableServiceCompatExtensions
  {
    public static Task<ProgesiVariable> UpsertAsync(this IProgesiVariableService svc, ProgesiVariable v)
        => Task.FromResult(svc.CreateOrUpdate(v));

    public static Task<ProgesiVariable> GetAsync(this IProgesiVariableService svc, Guid id)
        => Task.FromResult(svc.GetById(id));

    public static Task<ProgesiVariable> GetByNameAsync(this IProgesiVariableService svc, string name)
        => Task.FromResult(svc.GetByName(name));

    public static Task<bool> DeleteAsync(this IProgesiVariableService svc, Guid id)
        => Task.FromResult(svc.Delete(id));
  }
}
