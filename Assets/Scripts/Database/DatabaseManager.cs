/// <summary>
/// 数据库管理器
/// 管理SQLite数据库连接、表结构初始化
/// 使用自定义SqliteConnection（P/Invoke封装sqlite3.dll）
/// 所有数据库操作在主线程执行
/// </summary>
using System;
using System.IO;
using GalaxyAgent.Core;
using UnityEngine;

namespace GalaxyAgent.Database
{
    public class DatabaseManager
    {
        // 数据库连接
        private SqliteConnection _connection;
        // 数据库文件路径
        private string _dbPath;
        // 是否已初始化
        private bool _isInitialized;

        /// <summary>数据库是否已连接</summary>
        public bool IsConnected => _connection != null && _connection.IsOpen;

        /// <summary>
        /// 初始化数据库连接并创建表结构
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                // 数据库文件存放在持久化数据目录
                string dbDir = Path.Combine(Application.persistentDataPath, "Saves");
                if (!Directory.Exists(dbDir))
                {
                    Directory.CreateDirectory(dbDir);
                }

                _dbPath = Path.Combine(dbDir, Constants.DATABASE_FILE_NAME);

                _connection = new SqliteConnection();
                _connection.Open(_dbPath);

                // 创建表结构
                DatabaseSchema.CreateTables(_connection);

                _isInitialized = true;
                Debug.Log($"[DatabaseManager] 数据库初始化成功: {_dbPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DatabaseManager] 数据库初始化失败: {e.Message}");
            }
        }

        /// <summary>
        /// 获取数据库连接（供SaveLoadManager等使用）
        /// </summary>
        public SqliteConnection GetConnection()
        {
            return _connection;
        }

        /// <summary>
        /// 执行非查询SQL
        /// </summary>
        public int ExecuteNonQuery(string sql)
        {
            if (!IsConnected) return -1;
            return _connection.ExecuteNonQuery(sql);
        }

        /// <summary>
        /// 执行查询SQL，逐行回调
        /// </summary>
        public void ExecuteQuery(string sql, Action<string[]> onRead)
        {
            if (!IsConnected) return;
            _connection.ExecuteQuery(sql, onRead);
        }

        /// <summary>
        /// 执行标量查询
        /// </summary>
        public string ExecuteScalar(string sql)
        {
            if (!IsConnected) return null;
            return _connection.ExecuteScalar(sql);
        }

        /// <summary>
        /// 关闭数据库连接
        /// </summary>
        public void Close()
        {
            if (_connection != null)
            {
                _connection.Close();
                _connection.Dispose();
                _connection = null;
                _isInitialized = false;
                Debug.Log("[DatabaseManager] 数据库连接已关闭");
            }
        }

        /// <summary>
        /// SQL字符串转义（防注入）
        /// </summary>
        public static string Escape(string value)
        {
            return SqliteConnection.Escape(value);
        }
    }
}
