using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using nicorankLib.Analyze.model;
using nicorankLib.Util;
using Newtonsoft.Json;

namespace nicorankLib.output
{
    public class ResultJsonRankDB : OutputBase
    {
        private string outFolder;
        private string jsonName;

        /// <summary>
        /// 出力の設定をする
        /// </summary>
        /// <param name="folder">出力フォルダ</param>
        /// <param name="fileName">出力ファイル名</param>
        public void SetOutput(string folder, string fileName)
        {
            this.outFolder = folder;
            this.jsonName = fileName;
        }

        /// <summary>
        /// ランキングリストをJsonで出力する
        /// </summary>
        /// <param name="rankingList"></param>
        /// <returns></returns>
        public override bool Execute(IReadOnlyList<Ranking> rankingList)
        {
            try
            {
                Directory.CreateDirectory(outFolder);
                string jsonPath = Path.Combine(outFolder, jsonName);

                // Jsonシリアライズ
                var json = JsonConvert.SerializeObject(rankingList, Formatting.None);

                File.WriteAllText(jsonPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }
            return true;
        }
    }
}