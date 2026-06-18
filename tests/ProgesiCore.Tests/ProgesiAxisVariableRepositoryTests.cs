using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using Xunit;
using ProgesiCore;
using ProgesiCore.Persistence;
using ProgesiCore.Serialization;

namespace ProgesiCore.Tests
{
  public class ProgesiAxisVariableRepositoryTests
  {
    [Fact]
    public void Load_Mapping_FromDataReader_Works()
    {
      // Simulo header (SelectAxisHeaderById)
      var header = new DataTable();
      header.Columns.Add("AxisId", typeof(int));
      header.Columns.Add("AxisName", typeof(string));
      header.Columns.Add("AxisLength", typeof(double));
      header.Columns.Add("Name", typeof(string));
      header.Columns.Add("ValueTypeKey", typeof(string));
      header.Columns.Add("RuleId", typeof(int));
      header.Rows.Add(5, "AX", 10.0, "Thickness", "System.Double", 7);

      // Simulo entries (SelectAxisEntriesById)
      var entries = new DataTable();
      entries.Columns.Add("AxisId", typeof(int));
      entries.Columns.Add("Position", typeof(double));
      entries.Columns.Add("VariableId", typeof(int));
      entries.Rows.Add(5, 0.0, 1);
      entries.Rows.Add(5, 0.1, 2);
      entries.Rows.Add(5, 0.2, 3);

      var dto = new ProgesiAxisVariableDto();

      using (IDataReader r = header.CreateDataReader())
      {
        Assert.True(r.Read());
        dto.AxisId = r.GetInt32(0);
        dto.AxisName = r.GetString(1);
        dto.AxisLength = r.IsDBNull(2) ? (double?)null : r.GetDouble(2);
        dto.Name = r.GetString(3);
        dto.ValueTypeKey = r.GetString(4);
        dto.RuleId = r.IsDBNull(5) ? (int?)null : r.GetInt32(5);
      }

      using (IDataReader r = entries.CreateDataReader())
      {
        while (r.Read())
        {
          dto.Entries.Add(new ProgesiAxisVariableDto.Entry
          {
            Position = r.GetDouble(1),
            VariableId = r.GetInt32(2)
          });
        }
      }

      var axis = ProgesiAxisVariableDto.ToDomain(dto);
      Assert.Equal(5, axis.Id);
      Assert.Equal("AX", axis.AxisName);
      Assert.Equal(10.0, axis.AxisLength);
      Assert.Equal("Thickness", axis.Name);
      Assert.Equal("System.Double", axis.ValueTypeKey);
      Assert.Equal(7, axis.RuleId);

      var all = axis.EnumerateAll().ToList();
      Assert.Equal(3, all.Count);
    }

    [Fact]
    public void Save_BuildsExpectedSql_AndParameters()
    {
      var axis = new ProgesiAxisVariable(9, "AX-Save", "Thickness", "System.Double", axisLength: 12.5, ruleId: 77);

      var s1 = new ProgesiAxisVariable.ProgesiVariableSignature(1, "Thickness", "System.Double");
      var s2 = new ProgesiAxisVariable.ProgesiVariableSignature(2, "Thickness", "System.Double");
      var s3 = new ProgesiAxisVariable.ProgesiVariableSignature(3, "Thickness", "System.Double");

      axis.Add(s1, 0.0);
      axis.Add(s2, 0.1);
      axis.Add(s3, 0.2);

      var conn = new FakeDbConnection();
      conn.Open();

      var repo = new ProgesiAxisVariableRepository(conn);
      repo.Save(axis);

      Assert.Contains(conn.Commands, c => c.CommandText == ProgesiAxisVariableSql.Upsert_DeleteAxisEntries);
      Assert.Contains(conn.Commands, c => c.CommandText == ProgesiAxisVariableSql.Upsert_DeleteAxis);
      Assert.Contains(conn.Commands, c => c.CommandText == ProgesiAxisVariableSql.InsertAxis);
      Assert.True(conn.Commands.Count(x => x.CommandText == ProgesiAxisVariableSql.InsertAxisEntry) >= 3);

      var headerCmd = conn.Commands.First(c => c.CommandText == ProgesiAxisVariableSql.InsertAxis);
      var headerParams = headerCmd.Params;
      Assert.Contains(headerParams, p => p.ParameterName == "@AxisId" && (int)p.Value == 9);
      Assert.Contains(headerParams, p => p.ParameterName == "@AxisName" && (string)p.Value == "AX-Save");
      Assert.Contains(headerParams, p => p.ParameterName == "@AxisLength" && (double)p.Value == 12.5);
      Assert.Contains(headerParams, p => p.ParameterName == "@Name" && (string)p.Value == "Thickness");
      Assert.Contains(headerParams, p => p.ParameterName == "@ValueTypeKey" && (string)p.Value == "System.Double");
      Assert.Contains(headerParams, p => p.ParameterName == "@RuleId" && (int)p.Value == 77);

      var anyEntry = conn.Commands.First(c => c.CommandText == ProgesiAxisVariableSql.InsertAxisEntry);
      var entryParams = anyEntry.Params;
      Assert.Contains(entryParams, p => p.ParameterName == "@AxisId" && (int)p.Value == 9);
      Assert.Contains(entryParams, p => p.ParameterName == "@Position");
      Assert.Contains(entryParams, p => p.ParameterName == "@VariableId");
    }

    // -------------------------
    // Fakes ADO.NET minimi
    // -------------------------
    private sealed class FakeDbConnection : DbConnection
    {
      private ConnectionState _state = ConnectionState.Closed;
      public readonly List<FakeDbCommand> Commands = new List<FakeDbCommand>();

      public override string ConnectionString { get; set; } = string.Empty;
      public override string Database => "Fake";
      public override string DataSource => "Fake";
      public override string ServerVersion => "1.0";
      public override ConnectionState State => _state;

      public override void ChangeDatabase(string databaseName) { }
      public override void Close() { _state = ConnectionState.Closed; }
      public override void Open() { _state = ConnectionState.Open; }

      protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new FakeDbTransaction(this);
      protected override DbCommand CreateDbCommand()
      {
        var cmd = new FakeDbCommand(this);
        Commands.Add(cmd);
        return cmd;
      }
    }

    private sealed class FakeDbTransaction : DbTransaction
    {
      private readonly FakeDbConnection _conn;
      public FakeDbTransaction(FakeDbConnection conn) { _conn = conn; }
      public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
      protected override DbConnection DbConnection => _conn;
      public override void Commit() { }
      public override void Rollback() { }
    }

    private sealed class FakeDbCommand : DbCommand
    {
      private readonly FakeDbConnection _conn;
      public readonly List<FakeDbParameter> Params = new List<FakeDbParameter>();
      public FakeDbCommand(FakeDbConnection conn) { _conn = conn; }

      public override string CommandText { get; set; } = string.Empty;
      public override int CommandTimeout { get; set; }
      public override CommandType CommandType { get; set; } = CommandType.Text;
      public override UpdateRowSource UpdatedRowSource { get; set; } = UpdateRowSource.None;
      protected override DbConnection DbConnection { get => _conn; set { } }
      protected override DbParameterCollection DbParameterCollection => new FakeDbParameterCollection(Params);
      protected override DbTransaction DbTransaction { get; set; } = null!;
      public override bool DesignTimeVisible { get; set; }

      public override void Cancel() { }
      public override int ExecuteNonQuery() => 1;
      public override object ExecuteScalar() => 0;
      public override void Prepare() { }
      protected override DbParameter CreateDbParameter() => new FakeDbParameter();
      protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotImplementedException();
    }

    private sealed class FakeDbParameter : DbParameter
    {
      public override DbType DbType { get; set; }
      public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
      public override bool IsNullable { get; set; }
      public override string ParameterName { get; set; } = string.Empty;
      public override string SourceColumn { get; set; } = string.Empty;
      public override object Value { get; set; } = DBNull.Value;
      public override bool SourceColumnNullMapping { get; set; }
      public override int Size { get; set; }
      public override void ResetDbType() { }
    }

    private sealed class FakeDbParameterCollection : DbParameterCollection
    {
      private readonly List<FakeDbParameter> _list;
      public FakeDbParameterCollection(List<FakeDbParameter> list) { _list = list; }

      public override int Count => _list.Count;
      public override object SyncRoot => this;
      public override int Add(object value) { _list.Add((FakeDbParameter)value); return _list.Count - 1; }
      public override void AddRange(Array values) { foreach (var v in values) Add(v); }
      public override void Clear() { _list.Clear(); }
      public override bool Contains(object value) => _list.Contains((FakeDbParameter)value);
      public override bool Contains(string value) => _list.Any(p => p.ParameterName == value);
      public override void CopyTo(Array array, int index) => _list.ToArray().CopyTo(array, index);
      public override System.Collections.IEnumerator GetEnumerator() => _list.GetEnumerator();
      public override int IndexOf(object value) => _list.IndexOf((FakeDbParameter)value);
      public override int IndexOf(string parameterName) => _list.FindIndex(p => p.ParameterName == parameterName);
      public override void Insert(int index, object value) => _list.Insert(index, (FakeDbParameter)value);
      public override void Remove(object value) => _list.Remove((FakeDbParameter)value);
      public override void RemoveAt(int index) => _list.RemoveAt(index);
      public override void RemoveAt(string parameterName)
      {
        var i = IndexOf(parameterName);
        if (i >= 0) _list.RemoveAt(i);
      }
      protected override DbParameter GetParameter(int index) => _list[index];
      protected override DbParameter GetParameter(string parameterName) => _list.First(p => p.ParameterName == parameterName);
      protected override void SetParameter(int index, DbParameter value) => _list[index] = (FakeDbParameter)value;
      protected override void SetParameter(string parameterName, DbParameter value)
      {
        var i = IndexOf(parameterName);
        if (i >= 0) _list[i] = (FakeDbParameter)value;
      }
    }
  }
}
