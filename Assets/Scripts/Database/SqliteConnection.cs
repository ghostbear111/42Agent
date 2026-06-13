/// <summary>
/// SQLite原生库的P/Invoke封装
/// 通过直接调用sqlite3.dll的C接口操作数据库
/// 避免依赖Mono.Data.Sqlite（在Unity 6中可能不可用）
/// </summary>
using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace GalaxyAgent.Database
{
    /// <summary>
    /// sqlite3数据库连接句柄
    /// </summary>
    public class SqliteConnection : IDisposable
    {
        // sqlite3句柄指针
        private IntPtr _dbHandle;
        private bool _disposed;

        /// <summary>数据库是否已打开</summary>
        public bool IsOpen => _dbHandle != IntPtr.Zero;

        /// <summary>最后一条错误信息</summary>
        public string LastError
        {
            get
            {
                if (!IsOpen) return "数据库未打开";
                return Marshal.PtrToStringAnsi(NativeMethods.sqlite3_errmsg(_dbHandle)) ?? "未知错误";
            }
        }

        /// <summary>
        /// 打开指定路径的数据库文件
        /// </summary>
        public void Open(string databasePath)
        {
            int result = NativeMethods.sqlite3_open(databasePath, out _dbHandle);
            if (result != NativeMethods.SQLITE_OK)
            {
                string error = LastError;
                Close();
                throw new Exception($"无法打开数据库 {databasePath}: {error}");
            }
        }

        /// <summary>
        /// 执行非查询SQL语句（INSERT/UPDATE/DELETE/CREATE TABLE等）
        /// </summary>
        /// <returns>受影响的行数，出错返回-1</returns>
        public int ExecuteNonQuery(string sql)
        {
            if (!IsOpen) return -1;

            int result = NativeMethods.sqlite3_exec(_dbHandle, sql, IntPtr.Zero, IntPtr.Zero, out IntPtr errorMsg);
            if (result != NativeMethods.SQLITE_OK)
            {
                string errStr = errorMsg != IntPtr.Zero ? Marshal.PtrToStringAnsi(errorMsg) : LastError;
                Debug.LogError($"[SQLite] ExecuteNonQuery错误: {errStr}\nSQL: {sql}");
                if (errorMsg != IntPtr.Zero) NativeMethods.sqlite3_free(errorMsg);
                return -1;
            }
            return NativeMethods.sqlite3_changes(_dbHandle);
        }

        /// <summary>
        /// 执行查询SQL并逐行回调处理结果
        /// </summary>
        /// <param name="sql">SQL查询语句</param>
        /// <param name="rowCallback">每行回调，参数为列值数组</param>
        public void ExecuteQuery(string sql, Action<string[]> rowCallback)
        {
            if (!IsOpen || rowCallback == null) return;

            int result = NativeMethods.sqlite3_prepare_v2(_dbHandle, sql, -1, out IntPtr stmt, IntPtr.Zero);
            if (result != NativeMethods.SQLITE_OK)
            {
                Debug.LogError($"[SQLite] Prepare错误: {LastError}\nSQL: {sql}");
                return;
            }

            try
            {
                int columnCount = NativeMethods.sqlite3_column_count(stmt);

                while (true)
                {
                    result = NativeMethods.sqlite3_step(stmt);
                    if (result == NativeMethods.SQLITE_ROW)
                    {
                        // 读取当前行的所有列
                        var values = new string[columnCount];
                        for (int i = 0; i < columnCount; i++)
                        {
                            IntPtr textPtr = NativeMethods.sqlite3_column_text(stmt, i);
                            values[i] = textPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(textPtr) : null;
                        }
                        rowCallback(values);
                    }
                    else if (result == NativeMethods.SQLITE_DONE)
                    {
                        break; // 查询完成
                    }
                    else
                    {
                        Debug.LogError($"[SQLite] Step错误: {LastError}");
                        break;
                    }
                }
            }
            finally
            {
                NativeMethods.sqlite3_finalize(stmt);
            }
        }

        /// <summary>
        /// 执行标量查询，返回第一行第一列的值
        /// </summary>
        public string ExecuteScalar(string sql)
        {
            string resultValue = null;
            ExecuteQuery(sql, values =>
            {
                if (resultValue == null && values != null && values.Length > 0)
                {
                    resultValue = values[0];
                }
            });
            return resultValue;
        }

        /// <summary>
        /// 关闭数据库连接
        /// </summary>
        public void Close()
        {
            if (_dbHandle != IntPtr.Zero)
            {
                NativeMethods.sqlite3_close(_dbHandle);
                _dbHandle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Close();
                _disposed = true;
            }
        }

        /// <summary>
        /// SQL字符串转义（防注入）
        /// </summary>
        public static string Escape(string value)
        {
            return value?.Replace("'", "''") ?? "";
        }
    }

    /// <summary>
    /// sqlite3原生函数声明
    /// </summary>
    internal static class NativeMethods
    {
        public const int SQLITE_OK = 0;
        public const int SQLITE_ROW = 100;
        public const int SQLITE_DONE = 101;

        /// <summary>打开数据库文件</summary>
        [DllImport("sqlite3", EntryPoint = "sqlite3_open")]
        public static extern int sqlite3_open(string filename, out IntPtr db);

        /// <summary>关闭数据库</summary>
        [DllImport("sqlite3", EntryPoint = "sqlite3_close")]
        public static extern int sqlite3_close(IntPtr db);

        /// <summary>执行SQL语句（无需返回结果）</summary>
        [DllImport("sqlite3", EntryPoint = "sqlite3_exec")]
        public static extern int sqlite3_exec(IntPtr db, string sql, IntPtr callback, IntPtr callbackArg, out IntPtr errorMsg);

        /// <summary>准备SQL语句</summary>
        [DllImport("sqlite3", EntryPoint = "sqlite3_prepare_v2")]
        public static extern int sqlite3_prepare_v2(IntPtr db, string sql, int nByte, out IntPtr stmt, IntPtr tail);

        /// <summary>执行准备好的语句的下一步</summary>
        [DllImport("sqlite3", EntryPoint = "sqlite3_step")]
        public static extern int sqlite3_step(IntPtr stmt);

        /// <summary>释放准备好的语句</summary>
        [DllImport("sqlite3", EntryPoint = "sqlite3_finalize")]
        public static extern int sqlite3_finalize(IntPtr stmt);

        /// <summary>获取结果列数</summary>
        [DllImport("sqlite3", EntryPoint = "sqlite3_column_count")]
        public static extern int sqlite3_column_count(IntPtr stmt);

        /// <summary>获取指定列的文本值</summary>
        [DllImport("sqlite3", EntryPoint = "sqlite3_column_text")]
        public static extern IntPtr sqlite3_column_text(IntPtr stmt, int col);

        /// <summary>获取最后错误信息</summary>
        [DllImport("sqlite3", EntryPoint = "sqlite3_errmsg")]
        public static extern IntPtr sqlite3_errmsg(IntPtr db);

        /// <summary>获取受影响的行数</summary>
        [DllImport("sqlite3", EntryPoint = "sqlite3_changes")]
        public static extern int sqlite3_changes(IntPtr db);

        /// <summary>释放错误消息内存</summary>
        [DllImport("sqlite3", EntryPoint = "sqlite3_free")]
        public static extern void sqlite3_free(IntPtr ptr);
    }
}
