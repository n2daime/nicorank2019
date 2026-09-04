using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.api;
using nicorankLib.Util;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.api
{
    [TestClass]
    public class UnitTestNicoApiLockedTags
    {
        private const string LockedXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<nicovideo_thumb_response status=\"ok\"><thumb>" +
            "<video_id>sm9916382</video_id><title>t</title><description>d</description>" +
            "<thumbnail_url>u</thumbnail_url><first_retrieve>2010-03-05T05:04:59+09:00</first_retrieve>" +
            "<length>4:44</length><movie_type>mp4</movie_type><size_high>1</size_high><size_low>1</size_low>" +
            "<view_counter>10</view_counter><comment_num>1</comment_num><mylist_counter>2</mylist_counter>" +
            "<last_res_body>b</last_res_body><watch_url>w</watch_url><thumb_type>video</thumb_type>" +
            "<embeddable>1</embeddable><no_live_play>0</no_live_play>" +
            "<tags domain=\"jp\"><tag lock=\"1\">演奏してみた</tag><tag>未ロック</tag>" +
            "<tag lock=\"1\">流田P</tag><tag lock=\"1\">LEVEL5-judgelight-</tag></tags>" +
            "<genre>音楽</genre><user_id>1</user_id><user_nickname>n</user_nickname><user_icon_url>i</user_icon_url>" +
            "</thumb></nicovideo_thumb_response>";

        private const string NoLockXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<nicovideo_thumb_response status=\"ok\"><thumb>" +
            "<video_id>sm2</video_id><title>t</title><description>d</description>" +
            "<thumbnail_url>u</thumbnail_url><first_retrieve>2010-03-05T05:04:59+09:00</first_retrieve>" +
            "<length>4:44</length><movie_type>mp4</movie_type><size_high>1</size_high><size_low>1</size_low>" +
            "<view_counter>10</view_counter><comment_num>1</comment_num><mylist_counter>2</mylist_counter>" +
            "<last_res_body>b</last_res_body><watch_url>w</watch_url><thumb_type>video</thumb_type>" +
            "<embeddable>1</embeddable><no_live_play>0</no_live_play>" +
            "<tags domain=\"jp\"><tag>タグA</tag><tag>タグB</tag></tags>" +
            "<genre>音楽</genre><user_id>1</user_id><user_nickname>n</user_nickname><user_icon_url>i</user_icon_url>" +
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

        [TestMethod]
        public void lock混在XMLからlockのみ定義順に取得できる()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateNicovideoThumbTable(db);
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm1", 1, LockedXml);

                var api = new TestableNicoApi(db);
                var tags = api.GetLockedTags("sm1");

                CollectionAssert.AreEqual(
                    new List<string> { "演奏してみた", "流田P", "LEVEL5-judgelight-" }, tags);
            }
        }

        [TestMethod]
        public void タグロックなしは空リスト()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateNicovideoThumbTable(db);
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm2", 1, NoLockXml);

                var api = new TestableNicoApi(db);

                Assert.AreEqual(0, api.GetLockedTags("sm2").Count);
            }
        }

        [TestMethod]
        public void 行なしは空リスト()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateNicovideoThumbTable(db);

                var api = new TestableNicoApi(db);

                Assert.AreEqual(0, api.GetLockedTags("sm999").Count);
            }
        }

        [TestMethod]
        public void 複数取得日があれば最新の行を使う()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateNicovideoThumbTable(db);
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm1", 1, NoLockXml);
                TestDbHelper.InsertNicovideoThumbData(db, 20200103, "sm1", 1, LockedXml);

                var api = new TestableNicoApi(db);
                var tags = api.GetLockedTags("sm1");

                CollectionAssert.AreEqual(
                    new List<string> { "演奏してみた", "流田P", "LEVEL5-judgelight-" }, tags);
            }
        }

        [TestMethod]
        public void Status非okは空リスト()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateNicovideoThumbTable(db);
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm3", 0, FailXml);

                var api = new TestableNicoApi(db);

                Assert.AreEqual(0, api.GetLockedTags("sm3").Count);
            }
        }

        [TestMethod]
        public void 破損XMLは空リスト()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateNicovideoThumbTable(db);
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm4", 1, "<broken><xml>");

                var api = new TestableNicoApi(db);

                Assert.AreEqual(0, api.GetLockedTags("sm4").Count);
            }
        }
    }
}
