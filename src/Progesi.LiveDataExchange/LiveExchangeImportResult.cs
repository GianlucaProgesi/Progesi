using System.Collections.Generic;

namespace Progesi.LiveDataExchange
{
  public sealed class LiveExchangeMessage
  {
    public int Branch { get; set; }
    public string Text { get; set; } = "";
  }

  public sealed class LiveExchangeErrorCoordinate
  {
    public int Branch { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
  }

  public sealed class LiveExchangeCountSummary
  {
    public int Branch { get; set; }
    public string Text { get; set; } = "";
  }

  public sealed class LiveExchangeImportResult
  {
    public string SourcePath { get; set; } = "";
    public string LogPath { get; set; } = "";
    public string Info { get; set; } = "";
    public List<LiveExchangeMessage> Warnings { get; } = new List<LiveExchangeMessage>();
    public List<LiveExchangeMessage> Errors { get; } = new List<LiveExchangeMessage>();
    public List<LiveExchangeErrorCoordinate> ErrorCoordinates { get; } = new List<LiveExchangeErrorCoordinate>();
    public List<LiveExchangeCountSummary> Counts { get; } = new List<LiveExchangeCountSummary>();
    public List<string> LogLines { get; } = new List<string>();
  }
}
