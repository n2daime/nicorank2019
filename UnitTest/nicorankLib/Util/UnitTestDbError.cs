using Microsoft.Data.Sqlite;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Util;

namespace UnitTest.nicorankLib.Util
{
    [TestClass]
    public class UnitTestDbError
    {
        [TestMethod]
        public void ファイル不在時のOpen失敗後も安全にCloseできる()
        {
            using (var dbCtrl = new SQLiteCtrl())
            {
                bool opened = dbCtrl.Open("Z:\\存在しない\\NonExistent.db");
                Assert.IsFalse(opened);
                Assert.IsFalse(dbCtrl.IsOpen);

                bool closed = dbCtrl.Close();
                Assert.IsTrue(closed);
            }
        }

        [TestMethod]
        public void Close未接続時も複数回安全に呼べる()
        {
            using (var dbCtrl = new SQLiteCtrl())
            {
                Assert.IsTrue(dbCtrl.Close());
                Assert.IsTrue(dbCtrl.Close());
            }
        }

        [TestMethod]
        public void Dispose複数回呼んでも安全()
        {
            var dbCtrl = new SQLiteCtrl();
            dbCtrl.OpenInMemory();
            dbCtrl.Dispose();
            dbCtrl.Dispose();
        }

        [TestMethod]
        public void 複数インスタンスで同一DBファイルに同時接続できる()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                System.IO.File.Create(tempFile).Dispose();
                using (var db1 = new SQLiteCtrl())
                using (var db2 = new SQLiteCtrl())
                {
                    Assert.IsTrue(db1.Open(tempFile));
                    Assert.IsTrue(db2.Open(tempFile));

                    using (var cmd = db1.Connection.CreateCommand())
                    {
                        cmd.CommandText = "CREATE TABLE IF NOT EXISTS SharedTbl (ID TEXT PRIMARY KEY, 値 TEXT)";
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "INSERT INTO SharedTbl VALUES('k1', 'from_db1')";
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = db2.Connection.CreateCommand())
                    {
                        cmd.CommandText = "INSERT INTO SharedTbl VALUES('k2', 'from_db2')";
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = db1.Connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM SharedTbl";
                        Assert.AreEqual(2L, cmd.ExecuteScalar());
                    }

                    using (var cmd = db2.Connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT 値 FROM SharedTbl WHERE ID='k1'";
                        Assert.AreEqual("from_db1", cmd.ExecuteScalar().ToString());
                    }
                }
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
