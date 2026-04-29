using System;
using System.Globalization;
using UnityEngine;
using VrcScope;

namespace SCOPE
{
    [AddComponentMenu("SCOPE/Unity Logger")]
    public sealed class UnityLogger : MonoBehaviour
    {
        [SerializeField] LogType emitAs = LogType.Log;
        private Parser _parser = new Parser();

        private void OnEnable()
        {
            Application.logMessageReceived += HandleLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private void OnDestroy()
        {
            _parser?.Dispose();
        }

        private void HandleLog(string condition, string _, LogType type)
        {
            if (string.IsNullOrEmpty(condition)) return;

            var line = BuildSyntheticPrefixLine(condition, type);
            try
            {
                var jsonl = _parser.ParseLineAsJson(line);
                if (string.IsNullOrEmpty(jsonl)) return;
                Emit(jsonl);
            }
            catch (Exception e)
            {
                Debug.LogError("[UnityLogger] parser error: " + e.Message);
            }
        }

        private static string BuildSyntheticPrefixLine(string body, LogType type)
        {
            var timestamp = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture);
            return $"{timestamp} {UnityLevelToken(type)} -  {body}";
        }

        private void Emit(string line)
        {
            switch (emitAs)
            {
                case LogType.Warning:
                    Debug.LogWarning(line);
                    break;
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    Debug.LogError(line);
                    break;
                default:
                    Debug.Log(line);
                    break;
            }
        }

        private static string UnityLevelToken(LogType type) => type switch
        {
            LogType.Warning => "Warning",
            LogType.Error => "Error",
            LogType.Exception => "Exception",
            LogType.Assert => "Error",
            _ => "Log",
        };
    }
}
