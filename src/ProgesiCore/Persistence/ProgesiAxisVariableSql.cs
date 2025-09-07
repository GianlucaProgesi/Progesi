using System;

namespace ProgesiCore.Persistence
{
    /// <summary>
    /// DDL e statement SQL (provider-agnostici) per ProgesiAxisVariable.
    /// </summary>
    public static class ProgesiAxisVariableSql
    {
        // ---- DDL (SQLite/Postgre/MySQL friendly; per SQL Server sostituisci REAL->FLOAT)
        public const string Ddl_Axis = @"
CREATE TABLE IF NOT EXISTS Axis (
  AxisId     INTEGER      PRIMARY KEY,
  AxisName   TEXT         NOT NULL,
  AxisLength REAL         NULL,
  RuleId     INTEGER      NULL
);";

        public const string Ddl_AxisEntry = @"
CREATE TABLE IF NOT EXISTS AxisEntry (
  AxisId       INTEGER      NOT NULL,
  VariableName TEXT         NOT NULL,
  Position     REAL         NOT NULL,
  VariableId   INTEGER      NOT NULL,
  PRIMARY KEY (AxisId, VariableName, Position, VariableId),
  FOREIGN KEY (AxisId) REFERENCES Axis(AxisId) ON DELETE CASCADE
);";

        public const string Ddl_Indexes = @"
CREATE INDEX IF NOT EXISTS IX_AxisEntry_Axis ON AxisEntry(AxisId);
CREATE INDEX IF NOT EXISTS IX_AxisEntry_AxisName ON AxisEntry(AxisId, VariableName);
";

        // ---- SELECT
        public const string SelectAxisHeaderById = @"
SELECT AxisId, AxisName, AxisLength, RuleId
FROM Axis
WHERE AxisId = @AxisId;";

        public const string SelectAxisEntriesById = @"
SELECT AxisId, VariableName, Position, VariableId
FROM AxisEntry
WHERE AxisId = @AxisId
ORDER BY VariableName, Position, VariableId;";

        // ---- INSERT/DELETE (approccio semplice: delete+insert atomicamente)
        public const string Upsert_DeleteAxis = @"DELETE FROM Axis WHERE AxisId = @AxisId;";
        public const string Upsert_DeleteAxisEntries = @"DELETE FROM AxisEntry WHERE AxisId = @AxisId;";
        public const string InsertAxis = @"
INSERT INTO Axis (AxisId, AxisName, AxisLength, RuleId)
VALUES (@AxisId, @AxisName, @AxisLength, @RuleId);";

        public const string InsertAxisEntry = @"
INSERT INTO AxisEntry (AxisId, VariableName, Position, VariableId)
VALUES (@AxisId, @VariableName, @Position, @VariableId);";
    }
}
