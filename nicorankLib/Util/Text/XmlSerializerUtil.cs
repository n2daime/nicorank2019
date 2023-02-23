using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace nicorankLib.Util.Text
{
    public class XmlSerializerUtil
    {
        public static ClassName Deserialize<ClassName>(string xml)
        {
            //XmlSerializerオブジェクトを作成
            var serializer = new XmlSerializer(typeof(ClassName));

            var encoding = Encoding.GetEncoding("UTF-8");
            var stream = new MemoryStream(encoding.GetBytes(xml));

            //XMLファイルから読み込み、逆シリアル化する
            ClassName obj = (ClassName)serializer.Deserialize(stream);
            return obj;
        }

        public static string Serialize<ClassName>(ClassName xmlObject)
        {
            //XmlSerializerオブジェクトを作成
            var serializer = new XmlSerializer(typeof(ClassName));

            var encoding = Encoding.GetEncoding("UTF-8");
            var stream = new MemoryStream();

            //XMLファイルから読み込み、逆シリアル化する
            serializer.Serialize(XmlWriter.Create(stream), xmlObject);
            stream.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            return encoding.GetString(stream.GetBuffer());
        }
    }
}
