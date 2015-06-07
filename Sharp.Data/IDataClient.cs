using System;
using Sharp.Data.Filters;
using Sharp.Data.Fluent;
using Sharp.Data.Schema;

namespace Sharp.Data {
    
    public interface IDataClient : IDisposable  {

        string Schema { get; set;}

        IDatabase Database { get; }
        Dialect Dialect { get; }

        bool ThrowException { get; set; }

		FluentAdd Add { get; }
		FluentRemove Remove { get; }
        FluentRename Rename { get; }

		IFluentInsert Insert { get; }
		IFluentSelect Select { get; }
		IFluentUpdate Update { get; }
		IFluentDelete Delete { get; }
        IFluentCount Count { get; }
        IFluentModify Modify { get; }

        void AddTable(string tableName, params FluentColumn[] columns);
        void AddColumn(string tableName, Column column);
        void AddForeignKey(string fkName, string table, string column, string referencingTable, string referencingColumn, OnDelete onDelete);
        void AddNamedPrimaryKey(string tableName, string pkName, params string[] columnNames);
        void AddPrimaryKey(string tableName, params string[] columnNames);
        void AddUniqueKey(string uniqueKeyName, string tableName, params string[] columnNames);
    	void AddIndex(string indexName, string tableName, params string[] columnNames);
        void AddColumnComment(string tableName, string columnName, string comment);
        void AddTableComment(string tableName, string comment);

        void RemoveColumn(string tableName, string columnName);
        void RemovePrimaryKey(string tableName, string primaryKeyName);
        void RemoveForeignKey(string foreigKeyName, string tableName);
        void RemoveTable(string tableName);
        void RemoveUniqueKey(string uniqueKeyName, string tableName);
		void RemoveIndex(string indexName, string table);
        void RemoveTableComment(string tableName);
        void RemoveColumnComment(string tableName, string columnName);

        void RenameTable(string tableName, string newTableName);
        void RenameColumn(string tableName, string columnName, string newColumnName);

        void ModifyColumn(string firstTableName, string columnName, Column columnDefinition);

        void Commit();
        void RollBack();
        void Close();

        ResultSet SelectSql(string[] tables, string[] columns, Filter filter, OrderBy[] orderBys, int skip, int take);

        int InsertSql(string table, string[] columns, object[] values);
        object InsertReturningSql(string table, string columnToReturn, string[] columns, object[] values);
        int UpdateSql(string table, string[] columns, object[] values, Filter filter);
		int DeleteSql(string table, Filter filter);
    	int CountSql(string table, Filter filter);

    	bool TableExists(string table);


    }
}
