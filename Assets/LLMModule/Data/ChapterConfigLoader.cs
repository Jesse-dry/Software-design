using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace LLMModule.Data
{
    /// <summary>
    /// 从 StreamingAssets/Data/ 加载章节策划配置。
    ///
    /// 使用方式：
    ///   var config = ChapterConfigLoader.Load("chapter_01");
    ///   // → 读取 StreamingAssets/Data/chapter_01.json
    /// </summary>
    public static class ChapterConfigLoader
    {
        private const string DATA_FOLDER = "Data";

        /// <summary>
        /// 加载指定章节配置。
        /// </summary>
        /// <param name="chapterFileName">文件名（不含 .json 后缀），如 "chapter_01"</param>
        /// <returns>反序列化后的 ChapterConfig</returns>
        public static ChapterConfig Load(string chapterFileName)
        {
            string filePath = Path.Combine(
                Application.streamingAssetsPath, DATA_FOLDER, $"{chapterFileName}.json");

            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    $"[ChapterConfigLoader] 配置文件不存在: {filePath}");

            string json = File.ReadAllText(filePath);
            var config = JsonConvert.DeserializeObject<ChapterConfig>(json);

            if (config == null)
                throw new JsonException($"[ChapterConfigLoader] 反序列化失败: {filePath}");

            Debug.Log($"[ChapterConfigLoader] 已加载章节配置: {config.chapter} " +
                      $"({config.evidence?.cardDefinitions?.Length ?? 0} 张牌, " +
                      $"{config.trial?.npcs?.Length ?? 0} 个NPC)");

            return config;
        }

        /// <summary>
        /// 加载并返回完整路径（用于非 StreamingAssets 场景，如编辑器测试）
        /// </summary>
        public static ChapterConfig LoadFromPath(string fullPath)
        {
            if (!File.Exists(fullPath))
                throw new FileNotFoundException(
                    $"[ChapterConfigLoader] 配置文件不存在: {fullPath}");

            string json = File.ReadAllText(fullPath);
            var config = JsonConvert.DeserializeObject<ChapterConfig>(json);

            if (config == null)
                throw new JsonException($"[ChapterConfigLoader] 反序列化失败: {fullPath}");

            return config;
        }
    }
}
