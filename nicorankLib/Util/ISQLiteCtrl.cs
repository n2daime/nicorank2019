using System;
using System.Data.SQLite;

namespace nicorankLib.Util
{
    public interface ISQLiteCtrl : IDisposable
    {
        SQLiteConnection Connection { get; }
        bool IsOpen { get; }
        bool Open(string sDataSource);
        bool OpenInMemory();
        bool Close();
    }
}
