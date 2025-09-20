using System;
using Progesi.DomainServices.Interfaces;
using Progesi.DomainServices.Models;
using Progesi.DomainServices.Services;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  internal static class ServiceHub
  {
    private static readonly IProgesiVariableService _service = new InMemoryVariableService();
    public static IProgesiVariableService VariableService => _service;

    public static ProgesiVariable GetVariable(Guid id) => _service.GetById(id);
    public static ProgesiVariable CreateOrUpdate(ProgesiVariable v) => _service.CreateOrUpdate(v);
    public static bool Delete(Guid id) => _service.Delete(id);
  }
}
