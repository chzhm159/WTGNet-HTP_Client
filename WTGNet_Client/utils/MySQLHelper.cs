using Dapper;
using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WTGNet_Client.utils
{
    public class MySQLHelper
    {
        private readonly ILog log = LogManager.GetLogger(typeof(MySQLHelper));
        private static readonly MySQLHelper dbhelper = new MySQLHelper ();
        private static readonly object padlock = new object();
        /// <summary>
        /// 单例对应的数据库连接串
        /// </summary>
        private static string singleConnectString;

        /// <summary>
        /// 每个实例对应的连接串
        /// </summary>
        private string ConnectString;

        public MySQLHelper(string conntStr) {
            DefaultTypeMap.MatchNamesWithUnderscores = true;
            Interlocked.Exchange(ref model, 2);
            this.ConnectString = conntStr;
        }
        
        /// <summary>
        /// 2 为对象构造模式;
        /// 1 为单例模式
        /// </summary>
        private int model = 1;
        private MySQLHelper() {
            singleConnectString = AppCfg.GetStringVaue("mysql:url", singleConnectString) ;
            // 匹配字段中的下划线,并转换为驼峰
            DefaultTypeMap.MatchNamesWithUnderscores = true;
            Interlocked.Exchange(ref model, 1);
        }
        /// <summary>
        /// 获取一个用于数据操作的单例;
        /// 前提是数据链接串已在 App.config 配置文件中 ConnectString 指定;
        /// </summary>
        /// <returns></returns>
        public static MySQLHelper SingleInstance() {
            // Server=YOURSERVER;User ID=YOURUSERID;Password=YOURPASSWORD;Database=YOURDATABASE
            //if (singleInstance == null) {
            //    lock (padlock) {
            //        if (singleInstance == null) {
            //            string connectStr = ConfigurationManager.AppSettings.List("MySQL_ConnectString");
            //            if (String.IsNullOrEmpty(connectStr)) {
            //                throw new Exception("请在App.confg配置文件'appSettings'块中添加 'MySQL_ConnectString' 配置,用于链接数据库. 否则请使用 new DBHelper('ConnetctString...') ");
            //            }
            //            singleConnectString = connectStr;
            //            singleInstance = new MySQLHelper();
            //        }
            //    }
            //}
            return dbhelper;
        }
        private string GetConnectString() {
            // 单例模式返回 单例对应的数据库链接字符串
            if (model == 1) {
                return singleConnectString;
            } else {
                return ConnectString;
            }
        }
        public void Connect() {
            try {
                using (MySqlConnection conn = new MySqlConnection(GetConnectString())) {
                    
                    conn.Open();
                }
                // conn = new MySqlConnection(GetConnectString());    
            }catch (Exception ex) {
                log.ErrorFormat("数据库链接失败: {0},{1},{2}",ex.Message,ex.StackTrace,GetConnectString());
                AppEventBus.Inst().ShowMsg("数据库链接失败!,"+ex.Message);
            }
           
        }
        
        /// <summary>
        /// 执行一条SQL语句,返回影响的行数
        /// </summary>
        /// <param name="sql">sql语句</param>
        /// <param name="param">sql语句中的参数</param>
        /// <param name="transaction">事务</param>
        /// <param name="commandTimeout">执行超时</param>
        /// <param name="commandType">命令类型</param>
        /// <returns></returns>
        public int Execute(string sql, object param = null, bool withTransaction = false, int? commandTimeout = null, CommandType? commandType = null) {
            if (string.IsNullOrEmpty(sql)) {
                throw new Exception("SQL语句不可为空!");
            }
            //using (conn) {
                using (IDbConnection conn = new MySqlConnection(GetConnectString())) {
                // conn.Open();

                if (withTransaction) {
                    using (IDbTransaction transaction = conn.BeginTransaction()) {
                        try {
                            int rows = conn.Execute(sql, param, transaction: transaction, commandTimeout, commandType);
                            transaction.Commit();
                            return rows;
                        } catch (Exception e) {
                            Console.WriteLine(String.Format("MySQLHelper.QueryUseTransaction exception: {0},{1}", e.Message, e.StackTrace));
                            transaction.Rollback();
                            return 0;
                        }
                    }
                } else {
                    return conn.Execute(sql, param, commandTimeout: commandTimeout, commandType: commandType);
                }
            }
        }
        /// <summary>
        /// 执行一条插入语句
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="param"></param>
        /// <param name="commandTimeout"></param>
        /// <param name="commandType"></param>
        /// <returns></returns>
        public int Insert(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = null) {
            if (string.IsNullOrEmpty(sql) || !sql.StartsWith("insert into ", StringComparison.OrdinalIgnoreCase)) {
                throw new Exception("插入语句不可为空!,或者请修正为正确的 insert into 语句");
            }
            try {
                return this.Execute(sql, param, commandTimeout: commandTimeout, commandType: commandType);
            } catch (Exception e) {
                log.Error(String.Format("MySQLHelper.Insert exception: {0},{1}", e.Message, e.StackTrace));
                return 0;
            }
        }

        
        
        //}
        /// <summary>
        /// 删除操作
        /// </summary>
        /// <param name="sql">删除sql语句</param>
        /// <param name="param">SQL语句中的变量</param>
        /// <param name="withTransaction">是否事务删除</param>
        /// <param name="commandTimeout">sql执行超时</param>
        /// <param name="commandType"></param>
        /// <returns>返回删除的行数</returns>
        public int Delete(string sql, object param = null, bool withTransaction = false, int? commandTimeout = null, CommandType? commandType = null) {
            if (string.IsNullOrEmpty(sql) || !sql.StartsWith("delete from", StringComparison.OrdinalIgnoreCase)) {
                throw new Exception("删除语句不可为空!,或者请修正为正确的 delete from 语句");
            }
            try {
                return this.Execute(sql, param, withTransaction, commandTimeout: commandTimeout, commandType: commandType);
            } catch (Exception e) {
                log.Error(String.Format("MySQLHelper.Delete exception: {0},{1}", e.Message, e.StackTrace));
                return 0;
            }
        }
        public int Update(string sql, object param = null, bool withTransaction = false, int? commandTimeout = null, CommandType? commandType = null) {
            //if (string.IsNullOrEmpty(sql) || !sql.StartsWith("update ", StringComparison.OrdinalIgnoreCase)) {
            //    throw new Exception("更新语句不可为空!,或者请修正为正确的 update 语句");
            //}
            try {
                return this.Execute(sql,  param, withTransaction, commandTimeout: commandTimeout, commandType: commandType);
            } catch (Exception e) {
                log.Error(String.Format("MySQLHelper.Update exception: {0},{1}", e.Message, e.StackTrace));
                return 0;
            }
        }
        /// <summary>
        /// 执行一条查询语句,返回查询结果集.如果结果集为0,可能发生异常或者缺失没有结果
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="sql">查询语句</param>
        /// <param name="param">查询条件中的参数</param>
        /// <param name="commandTimeout">查询超时</param>
        /// <param name="commandType">查询类型</param>
        /// <returns></returns>
        public IEnumerable<T> Query<T>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = null) {

            using (IDbConnection conn = new MySqlConnection(GetConnectString())) {
                try {
                    IEnumerable<T> data = conn.Query<T>(sql, param, commandTimeout: commandTimeout, commandType: commandType);
                    if (data != null) {
                        return data;
                    } else {
                        return Array.Empty<T>();
                    }
                    //return ret;
                } catch (Exception e) {
                    Console.WriteLine(String.Format("MySQLHelper.Query exception: {0},{1}", e.Message, e.StackTrace));
                    return Array.Empty<T>();
                }
                
            }
        }
        
    }
}
