using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace IPBanManager.Services
{
    /// <summary>
    /// 基于 .NET Framework 内置 JavaScriptSerializer 的轻量 JSON 持久化。
    /// 写入采用同目录临时文件 + 替换，尽量降低断电或异常造成的半截文件风险。
    /// </summary>
    public static class JsonHelper
    {
        private static readonly JavaScriptSerializer Serializer = CreateSerializer();

        private static JavaScriptSerializer CreateSerializer()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            serializer.RecursionLimit = 256;
            return serializer;
        }

        public static string Serialize(object value)
        {
            return Serializer.Serialize(value);
        }

        public static T Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            return Serializer.Deserialize<T>(json);
        }

        public static T Load<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path, Encoding.UTF8);
            return Deserialize<T>(json);
        }

        public static void Save<T>(string path, T value)
        {
            string json = Serialize(value);
            SaveText(path, json);
        }

        public static void SaveText(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, text, new UTF8Encoding(false));

            try
            {
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temp, path, null);
                    }
                    catch (Exception)
                    {
                        File.Copy(temp, path, true);
                        TryDelete(temp);
                    }
                }
                else
                {
                    File.Move(temp, path);
                }
            }
            finally
            {
                TryDelete(temp);
            }
        }

        public static string ReadTextAuto(string path)
        {
            if (!File.Exists(path)) return "";
            byte[] bytes = File.ReadAllBytes(path);
            return DecodeText(bytes);
        }

        public static string DecodeText(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

            try
            {
                UTF8Encoding strict = new UTF8Encoding(false, true);
                return strict.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default.GetString(bytes);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // 清理临时文件失败不影响主流程。
            }
        }
    }
}
