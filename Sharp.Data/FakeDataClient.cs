using System;
using Sharp.Data.Filters;
using Sharp.Data.Fluent;
using Sharp.Data.Schema;

namespace Sharp.Data {
    public class FakeDataClient : IDataClient {
        public void Dispose() {}
        public string Schema { get; set; }
        public IDatabase Database { get; private set; }
        public Dialect Dialect { get; private set; }
        public bool ThrowException { get; set; }
        public FluentAdd Add { get; private set; }
        public FluentRemove Remove { get; private set; }
        public FluentRename Rename { get; private set; }
        public IFluentInsert Insert { get; private set; }
        public IFluentSelect Select { get; private set; }
        public IFluentUpdate Update { get; private set; }
        public IFluentDelete Delete { get; private set; }
        public IFluentCount Count { get; private set; }
        public IFluentModify Modify { get; private set; }

        public void AddTable(string tableName, params FluentColumn[] columns) {}
        public void AddColumn(string tableName, Column column) {}
        public void AddForeignKey(string fkName, string table, string column, string referencingTable, string referencingColumn, OnDelete onDelete) {}
        public void AddNamedPrimaryKey(string tableName, string pkName, params string[] columnNames) {}
        public void AddPrimaryKey(string tableName, params string[] columnNames) {}
        public void AddUniqueKey(string uniqueKeyName, string tableName, params string[] columnNames) {}
        public void AddIndex(string indexName, string tableName, params string[] columnNames) {}
        public void AddColumnComment(string tableName, string columnName, string comment) {}
        public void AddTableComment(string tableName, string comment) {}

        public void RemoveTable(string tableName) { }
        public void RemoveColumn(string tableName, string columnName) { }
        public void RemovePrimaryKey(string tableName, string primaryKeyName) { }
        public void RemoveForeignKey(string foreigKeyName, string tableName) {}
        public void RemoveUniqueKey(string uniqueKeyName, string tableName) {}
        public void RemoveIndex(string indexName, string table) {}
        public void RemoveTableComment(string tableName) {}
        public void RemoveColumnComment(string tableName, string columnName) {}
        
        public void RenameTable(string tableName, string newTableName) {}
        public void RenameColumn(string tableName, string columnName, string newColumnName) {}
        public void ModifyColumn(string firstTableName, string columnName, Column columnDefinition) {}

        public void Commit() {}
        public void RollBack() {}
        public void Close() {}
        public ResultSet SelectSql(string[] tables, string[] columns, Filter filter, OrderBy[] orderBys, int skip, int take) {
            return new ResultSet();
        }
        public int InsertSql(string table, string[] columns, object[] values) {
            return 0;
        }
        public object InsertReturningSql(string table, string columnToReturn, string[] columns, object[] values) {
            return 0;
        }
        public int UpdateSql(string table, string[] columns, object[] values, Filter filter) {
            return 0;
        }

        public int DeleteSql(string table, Filter filter) {
            return 0;
        }

        public int CountSql(string table, Filter filter) {
            return 0;
        }

        public bool TableExists(string table) {
            return false;
        }
    }
}