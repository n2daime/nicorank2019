using System;
using Microsoft.Data.Sqlite;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Util;

namespace UnitTest.nicorankLib.Util
{
    [TestClass]
    public class UnitTestSQLiteCtrl
    {
        [TestMethod]
        public void OpenInMemory_接続できる()
        {
            using (var dbCtrl = new SQLiteCtrl())
            {
                var result = dbCtrl.OpenInMemory();
                Assert.IsTrue(result);
                Assert.IsTrue(dbCtrl.IsOpen);
                Assert.IsNotNull(dbCtrl.Connection);
            }
        }

        [TestMethod]
        public void OpenInMemory_SQLが実行できる()
        {
            using (var dbCtrl = new SQLiteCtrl())
            {
                dbCtrl.OpenInMemory();
                using (var cmd = dbCtrl.Connection.CreateCommand())
                {
                    cmd.CommandText = "CREATE TABLE Test (ID TEXT PRIMARY KEY)";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "INSERT INTO Test VALUES('hello')";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "SELECT COUNT(*) FROM Test";
                    var count = (long)cmd.ExecuteScalar();
                    Assert.AreEqual(1L, count);
                }
            }
        }

        [TestMethod]
        public void Open_存在するDBファイルを開ける()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                System.IO.File.Create(tempFile).Dispose();
                using (var dbCtrl = new SQLiteCtrl())
                {
                    var result = dbCtrl.Open(tempFile);
                    Assert.IsTrue(result);
                    Assert.IsTrue(dbCtrl.IsOpen);
                }
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void Open_存在しないファイルはfalse()
        {
            using (var dbCtrl = new SQLiteCtrl())
            {
                var result = dbCtrl.Open("存在しないパス\\NonExistent.db");
                Assert.IsFalse(result);
                Assert.IsFalse(dbCtrl.IsOpen);
            }
        }

        [TestMethod]
        public void Open_同一パスで2回呼んでもエラーにならない()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                System.IO.File.Create(tempFile).Dispose();
                using (var dbCtrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(dbCtrl.Open(tempFile));
                    Assert.IsTrue(dbCtrl.Open(tempFile));
                }
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void Open_異なるパスで切り替えられる()
        {
            var tempFileA = Path.GetTempFileName();
            var tempFileB = Path.GetTempFileName();
            try
            {
                System.IO.File.Create(tempFileA).Dispose();
                System.IO.File.Create(tempFileB).Dispose();
                using (var dbCtrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(dbCtrl.Open(tempFileA));
                    Assert.IsTrue(dbCtrl.Open(tempFileB));
                }
            }
            finally
            {
                File.Delete(tempFileA);
                File.Delete(tempFileB);
            }
        }

        [TestMethod]
        public void Close_接続を切断できる()
        {
            using (var dbCtrl = new SQLiteCtrl())
            {
                dbCtrl.OpenInMemory();
                dbCtrl.Close();
                Assert.IsFalse(dbCtrl.IsOpen);
                Assert.IsNull(dbCtrl.Connection);
            }
        }

        [TestMethod]
        public void Close_未接続時でも安全()
        {
            using (var dbCtrl = new SQLiteCtrl())
            {
                var result = dbCtrl.Close();
                Assert.IsTrue(result);
            }
        }

        [TestMethod]
        public void Close_二回呼んでも安全()
        {
            using (var dbCtrl = new SQLiteCtrl())
            {
                dbCtrl.OpenInMemory();
                dbCtrl.Close();
                var result = dbCtrl.Close();
                Assert.IsTrue(result);
            }
        }

        [TestMethod]
        public void Dispose_usingブロックで自動解放される()
        {
            ISQLiteCtrl dbCtrl;
            using (dbCtrl = new SQLiteCtrl())
            {
                dbCtrl.OpenInMemory();
                Assert.IsTrue(dbCtrl.IsOpen);
            }
            Assert.IsFalse(dbCtrl.IsOpen);
        }

        [TestMethod]
        public void Dispose_二回呼んでも安全()
        {
            var dbCtrl = new SQLiteCtrl();
            dbCtrl.OpenInMemory();
            dbCtrl.Dispose();
            dbCtrl.Dispose();
        }

        [TestMethod]
        public void OpenInMemory_ISQLiteCtrlインターフェース経由で使用できる()
        {
            ISQLiteCtrl dbCtrl = new SQLiteCtrl();
            using (dbCtrl)
            {
                var result = dbCtrl.OpenInMemory();
                Assert.IsTrue(result);
            }
        }
    }
}
