using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
/* 
 Licensed under the Apache License, Version 2.0

 http://www.apache.org/licenses/LICENSE-2.0
 */
using System.Xml.Serialization;

namespace nicorankLib.Common
{

    [XmlRoot(ElementName = "nicorank")]
    public class NicoRankXml
    {
        [XmlElement(ElementName = "RANK")]
        public RANK RANK { get; set; }
        [XmlElement(ElementName = "RANKED")]
        public RANKED RANKED { get; set; }
        [XmlElement(ElementName = "UserInfo")]
        public UserInfoXml UserInfo { get; set; }
        [XmlElement(ElementName = "ICONDL_PATH")]
        public string ICONDL_PATH { get; set; }
        [XmlElement(ElementName = "POINT")]
        public POINT POINT { get; set; }
        [XmlElement(ElementName = "SP")]
        public SP SP { get; set; }
        [XmlElement(ElementName = "COMMENT_OFFSET")]
        public COMMENT_OFFSET COMMENT_OFFSET { get; set; }
        [XmlElement(ElementName = "MYLIST_OFFSET")]
        public MYLIST_OFFSET MYLIST_OFFSET { get; set; }
        [XmlElement(ElementName = "PLAY_OFFSET")]
        public PLAY_OFFSET PLAY_OFFSET { get; set; }
        [XmlElement(ElementName = "SYSTEM")]
        public SYSTEM SYSTEM { get; set; }
    }

    [XmlRoot(ElementName = "RANK")]
    public class RANK
    {
        [XmlAttribute(AttributeName = "Num")]
        public int Num { get; set; }
        [XmlAttribute(AttributeName = "Tyouki")]
        public bool Tyouki { get; set; }
    }

    [XmlRoot(ElementName = "RANKED")]
    public class RANKED
    {
        [XmlAttribute(AttributeName = "Num")]
        public int Num { get; set; }
    }

    [XmlRoot(ElementName = "UserInfo")]
    public class UserInfoXml
    {
        [XmlAttribute(AttributeName = "Num")]
        public int Num { get; set; }
    }

    [XmlRoot(ElementName = "POINT")]
    public class POINT
    {
        [XmlElement(ElementName = "CALC_MYLIST")]
        public double CALC_MYLIST { get; set; }
        [XmlElement(ElementName = "CALC_PLAY")]
        public double CALC_PLAY { get; set; }
        [XmlElement(ElementName = "CALC_COMMENT")]
        public double CALC_COMMENT { get; set; }
        [XmlElement(ElementName = "CALC_LIKE")]
        public double CALC_LIKE { get; set; }
    }

    [XmlRoot(ElementName = "SP")]
    public class SP
    {
        [XmlElement(ElementName = "POINT")]
        public POINT POINT { get; set; }
        [XmlElement(ElementName = "RANK")]
        public RANK RANK { get; set; }
        [XmlElement(ElementName = "RANKED")]
        public RANKED RANKED { get; set; }
        [XmlElement(ElementName = "UserInfo")]
        public UserInfoXml UserInfo { get; set; }
        [XmlElement(ElementName = "CheckDateOver")]
        public string CheckDateOver { get; set; }
    }

    [XmlRoot(ElementName = "COMMENT_OFFSET")]
    public class COMMENT_OFFSET
    {
        [XmlAttribute(AttributeName = "Mode")]
        public int Mode { get; set; }

        [XmlAttribute(AttributeName = "UnderLimit")]
        public double UnderLimit { get; set; }
    }

    [XmlRoot(ElementName = "MYLIST_OFFSET")]
    public class MYLIST_OFFSET
    {
        [XmlAttribute(AttributeName = "Mode")]
        public int Mode { get; set; }
    }

    [XmlRoot(ElementName = "PLAY_OFFSET")]
    public class PLAY_OFFSET
    {
        [XmlAttribute(AttributeName = "Mode")]
        public int Mode { get; set; }
    }

    [XmlRoot(ElementName = "ResultCsv")]
    public class ResultCsvXml
    {
        [XmlAttribute(AttributeName = "Code")]
        public bool IsUnicode { get; set; }
    }

    [XmlRoot(ElementName = "Thread")]
    public class Thread
    {
        [XmlAttribute(AttributeName = "Max")]
        public int Max { get; set; }
    }

    [XmlRoot(ElementName = "NicoChart")]
    public class NicoChart
    {
        [XmlAttribute(AttributeName = "Mode")]
        public bool Mode { get; set; }
    }

    [XmlRoot(ElementName = "NicoAPI")]
    public class NicoAPIXML
    {
        [XmlAttribute(AttributeName = "Retry")]
        public int Retry { get; set; }
    }

    [XmlRoot(ElementName = "UserIcon")]
    public class UserIcon
    {
        [XmlAttribute(AttributeName = "Retry")]
        public int Retry { get; set; }
    }

    [XmlRoot(ElementName = "Download")]
    public class Download
    {
        [XmlElement(ElementName = "NicoAPI")]
        public NicoAPIXML NicoAPI { get; set; }
        [XmlElement(ElementName = "UserIcon")]
        public UserIcon UserIcon { get; set; }
    }

    [XmlRoot(ElementName = "SYSTEM")]
    public class SYSTEM
    {
        [XmlElement(ElementName = "ResultCsv")]
        public ResultCsvXml ResultCsv { get; set; }
        [XmlElement(ElementName = "Thread")]
        public Thread Thread { get; set; }
        [XmlElement(ElementName = "NicoChart")]
        public NicoChart NicoChart { get; set; }
        [XmlElement(ElementName = "Download")]
        public Download Download { get; set; }
    }

}
