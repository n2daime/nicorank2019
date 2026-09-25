using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.model;
using nicorankLib.api;
using nicorankLib.Util;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.api
{
    /// <summary>
    /// Issue #40: 動画情報の取得失敗・Status非ok・行なしの場合も除外（isDelete）しないことの検証。
    /// 順位・ポイントに影響させず、表示欠落として残す方針の回帰テスト。
    /// </summary>
    [TestClass]
    public class UnitTestNicoApiNoDelete
    {
        private const string OkXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<nicovideo_thumb_response status=\"ok\"><thumb>" +
            "<video_id>sm1</video_id><title>件名</title><description>d</description>" +
            "<thumbnail_url>u</thumbnail_url><first_retrieve>2010-03-05T05:04:59+09:00</first_retrieve>" +
            "<length>4:44</length><movie_type>mp4</movie_type><size_high>1</size_high><size_low>1</size_low>" +
            "<view_counter>10</view_counter><comment_num>1</comment_num><mylist_counter>2</mylist_counter>" +
            "<last_res_body>b</last_res_body><watch_url>w</watch_url><thumb_type>video</thumb_type>" +
            "<embeddable>1</embeddable><no_live_play>0</no_live_play>" +
            "<tags domain=\"jp\"><tag>タグA</tag></tags>" +
            "<genre>音楽</genre><user_id>1</user_id><user_nickname>名</user_nickname><user_icon_url>i</user_icon_url>" +
            "</thumb></nicovideo_thumb_response>";

        private const string FailXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<nicovideo_thumb_response status=\"fail\"><error><code>DELETED</code>" +
            "<description>deleted</description></error></nicovideo_thumb_response>";

        private class TestableNicoApi : NicoApi
        {
            public TestableNicoApi(ISQLiteCtrl ctrl)
            {
                this.dbCtrl = ctrl;
            }
        }

        private static List<Ranking> RankingList(params string[] ids)
        {
            var list = new List<Ranking>();
            foreach (var id in ids)
            {
                list.Add(new Ranking() { ID = id });
            }
            return list;
        }

        [TestMethod]
        public void GetUserInfo_Status非okでも除外されず中断しない()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateNicovideoThumbTable(db);
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm1", 0, FailXml);

                var api = new TestableNicoApi(db);
                var list = RankingList("sm1");

                Assert.IsTrue(api.GetUserInfo(list));
                Assert.IsFalse(list[0].isDelete);
                Assert.IsFalse(string.IsNullOrWhiteSpace(list[0].PlayTime));
            }
        }

        [TestMethod]
        public void GetUserInfo_行なしでも除外されず中断しない()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateNicovideoThumbTable(db);

                var api = new TestableNicoApi(db);
                var list = RankingList("sm9");

                Assert.IsTrue(api.GetUserInfo(list));
                Assert.IsFalse(list[0].isDelete);
            }
        }

        [TestMethod]
        public void GetUserInfo_正常取得で補完される()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateNicovideoThumbTable(db);
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm1", 1, OkXml);

                var api = new TestableNicoApi(db);
                var list = RankingList("sm1");

                Assert.IsTrue(api.GetUserInfo(list));
                Assert.IsFalse(list[0].isDelete);
                Assert.AreEqual("1", list[0].UserID);
                Assert.AreEqual("名", list[0].UserName);
                Assert.AreEqual("音楽", list[0].Category);
            }
        }

        [TestMethod]
        public void GetMovieInfo_最新取得日の行を読む()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateNicovideoThumbTable(db);
                //古い行はok・新しい行はfail。最新（fail）が読まれればタイトルは空のままになる
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm1", 1, OkXml);
                TestDbHelper.InsertNicovideoThumbData(db, 20200201, "sm1", 0, FailXml);

                var api = new TestableNicoApi(db);
                var list = RankingList("sm1");

                Assert.IsTrue(api.GetMovieInfo(list, false, true));
                Assert.IsFalse(list[0].isDelete);
                Assert.IsTrue(string.IsNullOrWhiteSpace(list[0].Title));
            }
        }

        [TestMethod]
        public void GetMovieInfo_新しい行がokなら補完される()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateNicovideoThumbTable(db);
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm1", 0, FailXml);
                TestDbHelper.InsertNicovideoThumbData(db, 20200201, "sm1", 1, OkXml);

                var api = new TestableNicoApi(db);
                var list = RankingList("sm1");

                Assert.IsTrue(api.GetMovieInfo(list, false, true));
                Assert.IsFalse(list[0].isDelete);
                Assert.AreEqual("件名", list[0].Title);
            }
        }

        [TestMethod]
        public void GetMovieInfo_行なしでも除外されず中断しない()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateNicovideoThumbTable(db);

                var api = new TestableNicoApi(db);
                var list = RankingList("sm9");

                Assert.IsTrue(api.GetMovieInfo(list, false, true));
                Assert.IsFalse(list[0].isDelete);
                Assert.IsTrue(string.IsNullOrWhiteSpace(list[0].Title));
            }
        }
    }
}
