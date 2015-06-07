using System;
using System.Collections.Generic;
using System.Linq;
using Sharp.Data.Databases;
using Sharp.Data.Filters;
using Sharp.Data.Fluent;
using Sharp.Data.Schema;

namespace Sharp.Data {

    public class DataClient : IDataClient {
        public static string DefaultSchema { get; set; }

    	public IDatabase Database { get; set; }
		public Dialect Dialect { get; set; }
        public string Schema { get; set; }
        public bool ThrowException { get; set; }

    	public DataClient(IDatabase database, Dialect dialect, string schema = null) {
            Database = database;
			Dialect = dialect;
            ThrowException = true;
            if (schema == null) {
                DefaultSchema = Schema;            
            }
    	}

		public FluentAdd Add {
			get { return new FluentAdd(this); }
		}

		public FluentRemove Remove {
			get { return new FluentRemove(this); }
		}

        public FluentRename Rename {
            get { return new FluentRename(this);}
        }

        public IFluentModify Modify {
            get { return new FluentModify(this); }
        }

        public IFluentInsert Insert {
			get { return new FluentInsert(this); }
		}

		public IFluentUpdate Update {
			get { return new FluentUpdate(this); }
		}

		public IFluentSelect Select {
			get { return new FluentSelect(this); }
		}

		public IFluentDelete Delete {
			get { return new FluentDelete(this); }
		}

    	public IFluentCount Count {
			get { return new FluentCount(this); }
    	}

    	public virtual void AddTable(string tableName, params FluentColumn[] columns) {
            var table = new Table(tableName);
            foreach (FluentColumn fcol in columns) {
                table.Columns.Add(fcol.Object);
            }
            string[] sqls = Dialect.GetCreateTableSqls(table);
            ExecuteSqls(sqls);
        }

		private void ExecuteSqls(string[] sqls) {
    		foreach (string sql in sqls) {
    			Database.ExecuteSql(sql);
    		}
    	}

    	public virtual void RemoveTable(string tableName) {
            string[] sqls = Dialect.GetDropTableSqls(tableName);
			ExecuteSqls(sqls);
        }

        public virtual void AddPrimaryKey(string tableName, params string[] columnNames) {
            string sql = Dialect.GetPrimaryKeySql(tableName, "pk_" + tableName, columnNames);
            Database.ExecuteSql(sql);
        }

        public virtual void AddNamedPrimaryKey(string tableName, string pkName, params string[] columnNames) {
            string sql = Dialect.GetPrimaryKeySql(tableName, pkName, columnNames);
            Database.ExecuteSql(sql);
        }

        public virtual void AddForeignKey(string fkName, string table, string column, string referencingTable,
                                  string referencingColumn, OnDelete onDelete) {
            string sql = Dialect.GetForeignKeySql(fkName, table, column, referencingTable, referencingColumn, onDelete);
            Database.ExecuteSql(sql);
        }

        public void RemovePrimaryKey(string tableName, string primaryKeyName) {
            string sql = Dialect.GetDropPrimaryKeySql(tableName, primaryKeyName);
            Database.ExecuteSql(sql);
        }

        public virtual void RemoveForeignKey(string foreigKeyName, string tableName) {
            string sql = Dialect.GetDropForeignKeySql(foreigKeyName, tableName);
            Database.ExecuteSql(sql);
        }

        public virtual void AddUniqueKey(string uniqueKeyName, string tableName, params string[] columnNames) {
            string sql = Dialect.GetUniqueKeySql(uniqueKeyName, tableName, columnNames);
            Database.ExecuteSql(sql);
        }

    	public virtual void RemoveUniqueKey(string uniqueKeyName, string tableName) {
            string sql = Dialect.GetDropUniqueKeySql(uniqueKeyName, tableName);
            Database.ExecuteSql(sql);
        }

        public virtual void AddIndex(string indexName, string tableName, params string[] columnNames) {
    		string sql = Dialect.GetCreateIndexSql(indexName, tableName, columnNames);
    		Database.ExecuteSql(sql);
    	}

        public virtual void RemoveIndex(string indexName, string table) {
    		string sql = Dialect.GetDropIndexSql(indexName, table);
			Database.ExecuteSql(sql);
    	}

        public virtual void AddColumn(string tableName, Column column) {
            string sql = Dialect.GetAddColumnSql(tableName, column);
            Database.ExecuteSql(sql);
        }

        public virtual void RemoveColumn(string tableName, string columnName) {
            string[] sqls = Dialect.GetDropColumnSql(tableName, columnName);
            for (int i = 0; i < sqls.Length; i++) {
                Database.ExecuteSql(sqls[i]);                        
            }
        }

        public void AddTableComment(string tableName, string comment) {
            string sql = Dialect.GetAddCommentToTableSql(tableName, comment);
            Database.ExecuteSql(sql);
        }

        public void AddColumnComment(string tableName, string columnName, string comment) {
            string sql = Dialect.GetAddCommentToColumnSql(tableName, columnName, comment);
            Database.ExecuteSql(sql);
        }

        public virtual void RemoveTableComment(string tableName) {
            string sql = Dialect.GetRemoveCommentFromTableSql(tableName);
            Database.ExecuteSql(sql);
        }

        public virtual void RemoveColumnComment(string tableName, string columnName) {
            string sql = Dialect.GetRemoveCommentFromColumnSql(tableName, columnName);
            Database.ExecuteSql(sql);
        }

        public void RenameTable(string tableName, string newTableName) {
            string sql = Dialect.GetRenameTableSql(tableName, newTableName);
            Database.ExecuteSql(sql);
        }

        public void RenameColumn(string tableName, string columnName, string newColumnName) {
            string sql = Dialect.GetRenameColumnSql(tableName, columnName, newColumnName);
            Database.ExecuteSql(sql);
        }

        public void ModifyColumn(string tableName, string columnName, Column columnDefinition) {
            Database.ExecuteSql(Dialect.GetModifyColumnSql(tableName, columnName, columnDefinition));
        }

        public virtual ResultSet SelectSql(string[] tables, string[] columns, Filter filter, OrderBy[] orderBys, int skip, int take) {
            var selectBuilder = new SelectBuilder(Dialect, tables, columns);
            selectBuilder.Filter = filter;
            selectBuilder.OrderBys = orderBys;
            selectBuilder.Skip = skip;
            selectBuilder.Take = take;

            string sql = selectBuilder.Build();
            if (selectBuilder.HasFilter) {
                return Database.Query(sql, selectBuilder.Parameters);
            }
            return Database.Query(sql);
        }

        public virtual int InsertSql(string table, string[] columns, object[] values) {
            if (values == null) {
                values = new object[columns.Length];
            }
            string sql = Dialect.GetInsertSql(table, columns, values);
            return Database.ExecuteSql(sql, Dialect.ConvertToNamedParameters(values));
        }

        public virtual object InsertReturningSql(string table, string columnToReturn, string[] columns, object[] values) {
			var returningPar = new Out {Name = "returning_" + columnToReturn, Size = 4000};
            string retSql = Dialect.GetInsertReturningColumnSql(table, columns, values, columnToReturn, returningPar.Name);
            object[] pars = Dialect.ConvertToNamedParameters(values);
            List<object> listPars = pars.ToList();
            listPars.Add(returningPar);
            Database.ExecuteSql(retSql, listPars.ToArray());
            return returningPar.Value;
        }

        public virtual int UpdateSql(string table, string[] columns, object[] values, Filter filter) {
            if (values == null) {
                values = new object[columns.Length];
            }
            string sql = Dialect.GetUpdateSql(table, columns, values);

            In[] parameters = Dialect.ConvertToNamedParameters(values);
            if (filter != null) {
                string whereSql = Dialect.GetWhereSql(filter, parameters.Count());
            	object[] pars = filter.GetAllValueParameters();
                In[] filterParameters = Dialect.ConvertToNamedParameters(parameters.Count(), pars);
                filterParameters = filterParameters.Where(x => x.Value != null && x.Value != DBNull.Value).ToArray();
                parameters = parameters.Concat(filterParameters).ToArray();
                sql = sql + " " + whereSql;
            }

            return Database.ExecuteSql(sql, parameters);
        }

        public virtual int DeleteSql(string table, Filter filter) {
            string sql = Dialect.GetDeleteSql(table);

            if (filter != null) {
                string whereSql = Dialect.GetWhereSql(filter, 0);
				object[] pars = filter.GetAllValueParameters();
                In[] parameters = Dialect.ConvertToNamedParameters(0, pars);
                return Database.ExecuteSql(sql + " " + whereSql, parameters);
            }

            return Database.ExecuteSql(sql);
        }

		public virtual int CountSql(string table, Filter filter) {
			string sql = Dialect.GetCountSql(table);
			object obj;

			if (filter != null) {
				string whereSql = Dialect.GetWhereSql(filter, 0);
				object[] pars = filter.GetAllValueParameters();
				In[] parameters = Dialect.ConvertToNamedParameters(0, pars);
				obj = Database.QueryScalar(sql + " " + whereSql, parameters);
				return Convert.ToInt32(obj);
			}

			obj = Database.QueryScalar(sql);
			return Convert.ToInt32(obj);
		}

    	public bool TableExists(string table) {
    		string sql = Dialect.GetTableExistsSql(table);
    		return Convert.ToInt32(Database.QueryScalar(sql)) > 0;
    	}

    	public void Commit() {
            if (Database != null) {
                Database.Commit();
            }
        }

        public void RollBack() {
            if (Database != null) {
                Database.RollBack();
            }
        }

        public void Close() {
            if (Database != null) {
                Database.Close();
            }
        }

    	public void Dispose() {
    		Close();
		}
    }
}