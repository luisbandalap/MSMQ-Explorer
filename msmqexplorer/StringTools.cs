#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

#endregion

namespace MSMQExplorer
{
    internal static class StringTools
    {
        /// <summary>
        ///     Extracts string from XMLDocument
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public static string GetXmlAsString(XmlDocument xml)
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter tx = new XmlTextWriter(sw) {Formatting = Formatting.Indented};
            xml.WriteTo(tx);

            string str = sw.ToString();

            return str;
        }

        public static string GetStringFromByteArray(byte[] bytes)
        {
            char[] chars = new char[bytes.Length/sizeof (char)];
            Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new string(chars);
        }

        public static string GetStringFromByteList(List<byte> byteList)
        {
            if (byteList == null) return "";
            byte[] byteArray = byteList.ToArray();
            string text = Encoding.Unicode.GetString(byteArray);
            return text;
        }

        public static List<byte> GetByteListFromString(String str)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(str);
            List<byte> byteList = new List<byte>(bytes);
            return byteList;
        }

        public static byte[] ReadAllBytes(this BinaryReader reader)
        {
            const int bufferSize = 4096;
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] buffer = new byte[bufferSize];
                int count;
                while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                    ms.Write(buffer, 0, count);
                return ms.ToArray();
            }
        }

        public static byte[] ReadAllBytes(this Stream reader)
        {
            const int bufferSize = 4096;
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] buffer = new byte[bufferSize];
                int count;
                while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                    ms.Write(buffer, 0, count);
                return ms.ToArray();
            }
        }

        public static bool ContainsUnicodeCharacter(string input)
        {
            const int maxAnsiCode = 255;
            return input.Any(c => c > maxAnsiCode);
        }

        public static string PrettyXml(string xml)
        {
            StringBuilder stringBuilder = new StringBuilder();

            XElement element = XElement.Parse(xml);

            XmlWriterSettings settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
                NewLineOnAttributes = true
            };

            using (XmlWriter xmlWriter = XmlWriter.Create(stringBuilder, settings))
            {
                element.Save(xmlWriter);
            }

            return stringBuilder.ToString();
        }
    }
}