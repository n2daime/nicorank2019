using System;
using System.Data.SQLite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Util;

namespace UnitTest.nicorankLib.Util
{
    [TestClass]
    public class UnitTestSQLiteCtrl
    {
        [TestMethod]
        public void TestMethodSQL()
        {
            var dbCtrl = new SQLiteCtrl();
            if(!dbCtrl.Open("ソース"))
            {
                return;
            }
            using (var aCmd = new SQLiteCommand(dbCtrl.Connection))
            {
                //パラメータは@XXXX"で指定
                aCmd.CommandText = "SQL";
                aCmd.Parameters.AddWithValue("パラメータ名", "値");

                //実行結果の取得
                using (var reader = aCmd.ExecuteReader())
                {
                    while(reader.Read())
                    {
                        var strResult =  reader["カラム名"].ToString();

//                        System.Convert.ToBoolean
                    }
                }
            }
        }
        [TestMethod]
        public void TestMethodWrite()
        {
            var dbCtrl = new SQLiteCtrl();
            if (!dbCtrl.Open("ソース"))
            {
                return;
            }
            using (var aCmd = new SQLiteCommand(dbCtrl.Connection))
            {
                try
                {

                    aCmd.Transaction = dbCtrl.Connection.BeginTransaction();


                    //パラメータは@XXXX"で指定
                    aCmd.CommandText = "SQL";

                    aCmd.Parameters.AddWithValue("パラメータ名", "値");

                    //For分で追加する場合
                    //aCmd.Parameters.Clear();

                    //更新の実行
                    aCmd.ExecuteNonQuery();

                    aCmd.Transaction.Commit();
                }
                catch
                {
                    aCmd.Transaction.Rollback();
                }

            }

        }

    }
}
