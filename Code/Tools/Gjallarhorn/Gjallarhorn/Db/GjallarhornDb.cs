﻿using Eir.Common.IO;
using System;
using System.Collections.Generic;
using Eir.Common.Logging;

namespace Gjallarhorn.Db
{
    public class GjallarhornDb
    {
        private readonly DynaSql _dynaSql;
        private const string MONTHLY_STATS_TABLE_NAME = "MonthlyStats";
        

        public GjallarhornDb(IFileSystem fileSystem)
        {
            var dbLocation = fileSystem.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Gjallarhorn.sqllite");
            _dynaSql = new DynaSql($"Data Source={dbLocation};Version=3;");//will autocreate db if missing.
        }

        public void EnsureMonitorTableExists(string tableName)
        {
            string cmd = null;
            try
            {
                if (!_dynaSql.DbTableExists(tableName))
                {
                    cmd = $"create table if not exists {tableName} (id text PRIMARY KEY, created text not null, exportedDate text, data text);";
                    _dynaSql.SqlExecuteNonQuery(cmd);
                }
            }
            catch (Exception e)
            {
                Log.To.Main.AddException($"Failed ensuring monitor table. {cmd} on path {_dynaSql.ConnString}", e);
                throw;
            }
         
        }

        public void SaveMonitorData(string monitorName, string text)
        {
            string cmd = null;
            try
            {
                cmd = $"insert into {monitorName} (id,created,data) values(@id,@created,@data)";
                _dynaSql.SqlExecuteNonQuery(cmd, new List<DynaSql.DynaParameter>
                {
                    new DynaSql.DynaParameter{Name = "id",Value = Guid.NewGuid().ToString()},
                    new DynaSql.DynaParameter{Name = "created",Value = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.sss")},
                    new DynaSql.DynaParameter{Name = "data",Value = text}
                });
            }
            catch (Exception e)
            {
                Log.To.Main.AddException($"Failed saving monitor data. {cmd}",e);
                throw;
            }
         
        }

        private bool _montlyTableChecked;
        private void EnsureMontlyStatsTableExists()
        {
            if (_montlyTableChecked) return;

            string cmd = null;
            try
            {
                if (!_dynaSql.DbTableExists(MONTHLY_STATS_TABLE_NAME))
                {
                    cmd = $"create table if not exists {MONTHLY_STATS_TABLE_NAME} (id text PRIMARY KEY, year integer, month integer, idType integer, exportedDate text);";
                    _dynaSql.SqlExecuteNonQuery(cmd);
                }
            }
            catch (Exception e)
            {
                Log.To.Main.AddException($"Failed ensuring montly monitor data. {cmd} on path {_dynaSql.ConnString}", e);
                throw;
            }
           

            _montlyTableChecked = true;
        }

        public void AddToMontlyStats(Dictionary<string, int> values, int year, int month, MontlyStatsType idType)
        {
            string cmd = null;
            try
            {
                EnsureMontlyStatsTableExists();

                using (var conn = _dynaSql.ConnectionGet())
                {
                    conn.Open();
                    _dynaSql.SqlExecuteNonQuery(conn, "BEGIN TRANSACTION;");
                    cmd = $"insert or ignore into {MONTHLY_STATS_TABLE_NAME} (id, year, month, idType) values(@id, @year, @month, @idType)";
                    foreach (var val in values)
                    {
                        _dynaSql.SqlExecuteNonQuery(conn, cmd, new List<DynaSql.DynaParameter>
                        {
                            new DynaSql.DynaParameter{Name = "id", Value = val.Key},
                            new DynaSql.DynaParameter{Name = "year", Value = year.ToString()},
                            new DynaSql.DynaParameter{Name = "month", Value = month.ToString()},
                            new DynaSql.DynaParameter{Name = "idType",Value = ((int)idType).ToString()}
                        });
                    }
                    _dynaSql.SqlExecuteNonQuery(conn, "COMMIT TRANSACTION;");
                    conn.Close();
                }
            }
            catch (Exception e)
            {
                Log.To.Main.AddException($"Failed running monthly stats. {cmd} on path {_dynaSql.ConnString}", e);
                throw;
            }
           
           
        }

        public int CurrentMontlyRunInDb()
        {
            EnsureMontlyStatsTableExists();

            var notExportetYet = _dynaSql.SqlExecuteScalar($"Select month from {MONTHLY_STATS_TABLE_NAME} limit 1");
            if (!int.TryParse(notExportetYet, out int ret))
            {
                ret = -1;
            }
            return ret;
        }

        public void ResetMontlyDbTable()
        {
            EnsureMontlyStatsTableExists();

            _dynaSql.SqlExecuteNonQuery($"drop table {MONTHLY_STATS_TABLE_NAME} ");
            EnsureMontlyStatsTableExists();
        }

        public int GetToMontlyStats(MontlyStatsType type)
        {
            EnsureMontlyStatsTableExists();

            var val = _dynaSql.SqlExecuteScalar($"Select count(id) from {MONTHLY_STATS_TABLE_NAME} where idType = {(int)type}");
            int.TryParse(val, out int ret);
            return ret;
        }
    }
}
