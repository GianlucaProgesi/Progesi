# AxisVar golden fixtures

Golden files for no-regression tests of axis stationing across `Curve3d`, `PlanXY`, and `Profile` modes.

## station-table-3d-vs-projected.json

Worked example: 3D line from `(0,0,0)` to `(3,4,12)`.

- **Curve3d** abscissa length = 13 (true 3D arc length)
- **PlanXY / Profile** abscissa length = 5 (plan projection)

Real stations `[0, 2.5, 5.0]` normalize differently in Curve3d vs projected modes — the golden test asserts mapper output matches this table to tolerance.

Consumed by `StationTableGoldenTests` in `ProgesiRepositories.Rhino.Tests` (Rhino-native; requires hosted runner with Rhino 8 on PATH).
