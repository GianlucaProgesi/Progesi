namespace Progesi.DomainServices.Interfaces
{
  public interface IAxisStationingService
  {
    // AxisLength è la lunghezza reale (m) dell'asse
    double ToNormalized(double axisLength, double stationReal);
    double ToReal(double axisLength, double stationNormalized);
  }
}
