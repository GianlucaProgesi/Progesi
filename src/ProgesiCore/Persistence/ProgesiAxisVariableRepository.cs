using System;
using System.Data;
using System.Data.Common;
using ProgesiCore.Serialization;

namespace ProgesiCore.Persistence
{
    /// <summary>
    /// Repository minimale, ADO.NET-agnostico: funziona con qualunque DbConnection (SQLite, SQL Server, ecc.)
    /// Contratto: la connessione viene passata APERTA dall'esterno (gestisci tu il lifecycle).
    /// </summary>
    public sealed class ProgesiAxisVariableRepository
    {
        private readonly DbConnection _conn;

        public ProgesiAxisVariableRepository(DbConnection openConnection)
        {
            _conn = openConnection ?? throw new ArgumentNullException(nameof(openConnection));
            if (_conn.State != ConnectionState.Open)
                throw new InvalidOperationException("Connection must be opened.");
        }

        public void EnsureSchema()
        {
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = ProgesiAxisVariableSql.Ddl_Axis;
                cmd.ExecuteNonQuery();
            }
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = ProgesiAxisVariableSql.Ddl_AxisEntry;
                cmd.ExecuteNonQuery();
            }
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = ProgesiAxisVariableSql.Ddl_Indexes;
                cmd.ExecuteNonQuery();
            }
        }

        public ProgesiAxisVariable Load(int axisId)
        {
            // Header
            ProgesiAxisVariableDto dto = new ProgesiAxisVariableDto();
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = ProgesiAxisVariableSql.SelectAxisHeaderById;
                AddParam(cmd, "@AxisId", axisId);

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read())
                        throw new InvalidOperationException($"Axis {axisId} not found.");

                    dto.AxisId = r.GetInt32(0);
                    dto.AxisName = r.GetString(1);
                    dto.AxisLength = r.IsDBNull(2) ? (double?)null : r.GetDouble(2);
                    dto.RuleId = r.IsDBNull(3) ? (int?)null : r.GetInt32(3);
                }
            }

            // Entries
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = ProgesiAxisVariableSql.SelectAxisEntriesById;
                AddParam(cmd, "@AxisId", axisId);

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        dto.Entries.Add(new ProgesiAxisVariableDto.Entry
                        {
                            VariableName = r.GetString(1),
                            Position = r.GetDouble(2),
                            VariableId = r.GetInt32(3)
                        });
                    }
                }
            }

            return ProgesiAxisVariableDto.ToDomain(dto);
        }

        /// <summary>
        /// Strategia semplice e robusta: DELETE + INSERT, il tutto in transazione.
        /// </summary>
        public void Save(ProgesiAxisVariable axis)
        {
            var dto = ProgesiAxisVariableDto.FromDomain(axis);

            using (var tx = _conn.BeginTransaction())
            {
                // DELETE previous
                using (var del1 = _conn.CreateCommand())
                {
                    del1.Transaction = tx;
                    del1.CommandText = ProgesiAxisVariableSql.Upsert_DeleteAxisEntries;
                    AddParam(del1, "@AxisId", dto.AxisId);
                    del1.ExecuteNonQuery();
                }
                using (var del2 = _conn.CreateCommand())
                {
                    del2.Transaction = tx;
                    del2.CommandText = ProgesiAxisVariableSql.Upsert_DeleteAxis;
                    AddParam(del2, "@AxisId", dto.AxisId);
                    del2.ExecuteNonQuery();
                }

                // INSERT header
                using (var ins = _conn.CreateCommand())
                {
                    ins.Transaction = tx;
                    ins.CommandText = ProgesiAxisVariableSql.InsertAxis;
                    AddParam(ins, "@AxisId", dto.AxisId);
                    AddParam(ins, "@AxisName", dto.AxisName);
                    AddParam(ins, "@AxisLength", (object?)dto.AxisLength ?? DBNull.Value);
                    AddParam(ins, "@RuleId", (object?)dto.RuleId ?? DBNull.Value);
                    ins.ExecuteNonQuery();
                }

                // INSERT entries
                foreach (var e in dto.Entries)
                {
                    using (var insE = _conn.CreateCommand())
                    {
                        insE.Transaction = tx;
                        insE.CommandText = ProgesiAxisVariableSql.InsertAxisEntry;
                        AddParam(insE, "@AxisId", dto.AxisId);
                        AddParam(insE, "@VariableName", e.VariableName);
                        AddParam(insE, "@Position", e.Position);
                        AddParam(insE, "@VariableId", e.VariableId);
                        insE.ExecuteNonQuery();
                    }
                }

                tx.Commit();
            }
        }

        private static void AddParam(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }
}
