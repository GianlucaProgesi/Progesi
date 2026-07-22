// src/ProgesiGrasshopperAssembly/ProgesiAssemblyPriority.cs
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Components;
using System;

namespace ProgesiGrasshopperAssembly
{
  /// <summary>
  /// Viene eseguito da GH all'avvio del plugin, prima della creazione dei componenti.
  /// Qui installiamo il binder EF/SQLite in modo che il CLR carichi le DLL corrette
  /// dal folder della .gha o da GH\Libraries PRIMA che EF si inizializzi.
  /// </summary>
  public sealed class ProgesiAssemblyPriority : GH_AssemblyPriority
  {
    public override GH_LoadingInstruction PriorityLoad()
    {
      // installa il binder una sola volta
      return GH_LoadingInstruction.Proceed;
    }
  }
}
