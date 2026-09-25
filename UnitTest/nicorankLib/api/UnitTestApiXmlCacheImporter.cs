using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.api;
using nicorankLib.Util;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.api
{
    /// <summary>
    /// Issue #40: 運搬ファイルから本地への取込（新しい取得日だけ置き換え）の検証。
    /// 本地にしかない貯金を消さないこと・上書きコピーしないことが要点。
    /// </summary>
    [TestClass]
    public class UnitTestApiXmlCacheImporter
    {
        private static string CreateThumbDbFile()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db");
            File.Create(path).Dispose();
            var dbCtrl = new SQLiteCtrl();
            Assert.IsTrue(dbCtrl.Open(path), "temp db open");
            ApiXmlCacheImporter.EnsureNicovideoThumbTable(dbCtrl);
            dbCtrl.Close();
            return path;
        }

        private static void InsertRow(string path, int getDate, string id, string xml)
        {
            var dbCtrl = new SQLiteCtrl();
            Assert.IsTrue(dbCtrl.Open(path), "temp db open");
            TestDbHelper.InsertNicovideoThumbData(dbCtrl, getDate, id, 1, xml);
            dbCtrl.Close();
        }

        private static string ReadXml(string path, string id)
        {
            var dbCtrl = new SQLiteCtrl();
            Assert.IsTrue(dbCtrl.Open(path), "temp db open");
            string xml = null;
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = "SELECT XML FROM NicovideoThumb WHERE ID = @ID";
                cmd.Parameters.AddWithValue("@ID", id);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        xml = reader["XML"].ToString();
                    }
                }
            }
            dbCtrl.Close();
            return xml;
        }

        [TestMethod]
        public void 運搬行が新規IDなら取り込む()
        {
            string source = CreateThumbDbFile();
            string local = CreateThumbDbFile();
            try
            {
                InsertRow(source, 20200101, "sm1", "XML_A");
                InsertRow(source, 20200101, "sm2", "XML_B");

                var importer = new ApiXmlCacheImporter();
                Assert.AreEqual(2, importer.MergeCacheFile(source, local));
                Assert.AreEqual("XML_A", ReadXml(local, "sm1"));
                Assert.AreEqual("XML_B", ReadXml(local, "sm2"));
            }
            finally
            {
                File.Delete(source);
                File.Delete(local);
            }
        }

        [TestMethod]
        public void 本地が新しければ置き換えない()
        {
            string source = CreateThumbDbFile();
            string local = CreateThumbDbFile();
            try
            {
                InsertRow(source, 20200101, "sm1", "XML_OLD");
                InsertRow(source, 20200101, "sm2", "XML_NEW_ID");
                InsertRow(local, 20200201, "sm1", "XML_LOCAL");

                var importer = new ApiXmlCacheImporter();
                Assert.AreEqual(1, importer.MergeCacheFile(source, local));
                Assert.AreEqual("XML_LOCAL", ReadXml(local, "sm1"));
                Assert.AreEqual("XML_NEW_ID", ReadXml(local, "sm2"));
            }
            finally
            {
                File.Delete(source);
                File.Delete(local);
            }
        }

        [TestMethod]
        public void 運搬側が新しければ置き換える()
        {
            string source = CreateThumbDbFile();
            string local = CreateThumbDbFile();
            try
            {
                InsertRow(source, 20200201, "sm1", "XML_NEW");
                InsertRow(local, 20200101, "sm1", "XML_OLD");

                var importer = new ApiXmlCacheImporter();
                Assert.AreEqual(1, importer.MergeCacheFile(source, local));
                Assert.AreEqual("XML_NEW", ReadXml(local, "sm1"));
            }
            finally
            {
                File.Delete(source);
                File.Delete(local);
            }
        }
    }
}
