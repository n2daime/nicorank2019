using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using nicorankLib.Util;

namespace UnitTest.Helpers
{
    public static class TestDbHelper
    {
        public static ISQLiteCtrl CreateInMemoryDb()
        {
            var dbCtrl = new SQLiteCtrl();
            dbCtrl.OpenInMemory();
            return dbCtrl;
        }

        public static void CreateRankingTable(ISQLiteCtrl dbCtrl)
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Ranking (
                        ID TEXT,
                        集計日 INTEGER,
                        再生数 INTEGER,
                        コメント数 INTEGER,
                        マイリスト数 INTEGER,
                        いいね数 INTEGER,
                        人気のタグ TEXT
                    )";
                cmd.ExecuteNonQuery();
            }
        }

        public static void CreateMovieTable(ISQLiteCtrl dbCtrl)
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Movie (
                        ID TEXT PRIMARY KEY,
                        投稿日 INTEGER,
                        タイトル TEXT
                    )";
                cmd.ExecuteNonQuery();
            }
        }

        public static void CreateRankingDateTable(ISQLiteCtrl dbCtrl)
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS RankingDate (
                        集計日 INTEGER PRIMARY KEY,
                        メンテナンス INTEGER DEFAULT 0
                    )";
                cmd.ExecuteNonQuery();
            }
        }

        public static void CreateNicovideoThumbTable(ISQLiteCtrl dbCtrl)
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS NicovideoThumb (
                        取得日 INTEGER,
                        ID TEXT,
                        Status INTEGER,
                        XML TEXT
                    )";
                cmd.ExecuteNonQuery();
            }
        }

        public static void CreateLastResultTable(ISQLiteCtrl dbCtrl)
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS LastResult (
                        種別 TEXT,
                        集計日 INTEGER,
                        ID TEXT,
                        タイトル TEXT,
                        総合ランク INTEGER,
                        ポイント INTEGER,
                        再生数 INTEGER,
                        コメント数 INTEGER,
                        マイリスト数 INTEGER,
                        累計再生数 INTEGER,
                        累計コメント数 INTEGER,
                        累計マイリスト数 INTEGER,
                        JSON TEXT,
                        いいね数 INTEGER DEFAULT 0,
                        累計いいね数 INTEGER DEFAULT 0
                    )";
                cmd.ExecuteNonQuery();
            }
        }

        public static void CreateHistoryTable(ISQLiteCtrl dbCtrl)
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS History (
                        集計日 INTEGER,
                        ID TEXT,
                        総合ランク INTEGER,
                        ポイント INTEGER,
                        再生数 INTEGER,
                        コメント数 INTEGER,
                        マイリスト数 INTEGER,
                        いいね数 INTEGER DEFAULT 0
                    )";
                cmd.ExecuteNonQuery();
            }
        }

        public static void CreateLastResultInfoTable(ISQLiteCtrl dbCtrl)
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS LastResultInfo (
                        種別 TEXT,
                        集計日 INTEGER,
                        XML TEXT
                    )";
                cmd.ExecuteNonQuery();
            }
        }

        public static void CreateDailylogTable(ISQLiteCtrl dbCtrl)
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Dailylog (
                        集計日 INTEGER,
                        ID TEXT,
                        タイトル TEXT,
                        投稿日 INTEGER,
                        再生時間 TEXT,
                        総合順位 INTEGER,
                        ポイント数 INTEGER,
                        カテゴリランク INTEGER,
                        カテゴリ TEXT,
                        人気のタグ TEXT,
                        再生ランク INTEGER,
                        再生数 INTEGER,
                        再生補正 REAL,
                        再生ポイント INTEGER,
                        コメントランク INTEGER,
                        コメント数 INTEGER,
                        コメント補正 REAL,
                        コメントポイント INTEGER,
                        マイリストランク INTEGER,
                        マイリスト数 INTEGER,
                        マイリスト補正 REAL,
                        マイリストポイント INTEGER,
                        いいねランク INTEGER DEFAULT 0,
                        いいね数 INTEGER DEFAULT 0,
                        いいね補正 INTEGER DEFAULT 1,
                        いいねポイント INTEGER DEFAULT 0,
                        イメージパス TEXT
                    )";
                cmd.ExecuteNonQuery();
            }
        }

        public static void CreateSnapshotRankingTable(ISQLiteCtrl dbCtrl)
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Ranking (
                        ID TEXT PRIMARY KEY,
                        再生数 INTEGER,
                        コメント数 INTEGER,
                        マイリスト数 INTEGER,
                        いいね数 INTEGER
                    )";
                cmd.ExecuteNonQuery();
            }
        }

        public static void CreateDBVersionTable(ISQLiteCtrl dbCtrl)
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS DBVersion (
                        集計日 INTEGER,
                        Ver TEXT
                    )";
                cmd.ExecuteNonQuery();
            }
        }

        public static void InsertRankingData(ISQLiteCtrl dbCtrl, string id, int syuukeiBi, int play, int comment, int mylist, int like, string tags = "[]")
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = @"INSERT INTO Ranking(ID, 集計日, 再生数, コメント数, マイリスト数, いいね数, 人気のタグ)
                                    VALUES(@ID, @集計日, @再生数, @コメント数, @マイリスト数, @いいね数, @人気のタグ)";
                cmd.Parameters.AddWithValue("@ID", id);
                cmd.Parameters.AddWithValue("@集計日", syuukeiBi);
                cmd.Parameters.AddWithValue("@再生数", play);
                cmd.Parameters.AddWithValue("@コメント数", comment);
                cmd.Parameters.AddWithValue("@マイリスト数", mylist);
                cmd.Parameters.AddWithValue("@いいね数", like);
                cmd.Parameters.AddWithValue("@人気のタグ", tags);
                cmd.ExecuteNonQuery();
            }
        }

        public static void InsertMovieData(ISQLiteCtrl dbCtrl, string id, long date, string title)
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = @"INSERT INTO Movie(ID, 投稿日, タイトル) VALUES(@ID, @投稿日, @タイトル)";
                cmd.Parameters.AddWithValue("@ID", id);
                cmd.Parameters.AddWithValue("@投稿日", date);
                cmd.Parameters.AddWithValue("@タイトル", title);
                cmd.ExecuteNonQuery();
            }
        }

        public static void InsertNicovideoThumbData(ISQLiteCtrl dbCtrl, int getDate, string id, int status, string xml)
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = @"INSERT INTO NicovideoThumb(取得日, ID, Status, XML) VALUES(@取得日, @ID, @Status, @XML)";
                cmd.Parameters.AddWithValue("@取得日", getDate);
                cmd.Parameters.AddWithValue("@ID", id);
                cmd.Parameters.AddWithValue("@Status", status);
                cmd.Parameters.AddWithValue("@XML", xml);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
