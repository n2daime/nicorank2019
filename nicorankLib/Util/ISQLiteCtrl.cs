using System;
using Microsoft.Data.Sqlite;

namespace nicorankLib.Util
{
    public interface ISQLiteCtrl : IDisposable
    {
        SqliteConnection Connection { get; }
        bool IsOpen { get; }
        bool Open(string sDataSource);
        bool OpenInMemory();
        bool Close();
    }
}
