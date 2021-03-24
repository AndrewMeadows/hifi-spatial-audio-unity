using System;
using System.Collections.Generic;
//using UnityEngine;

namespace HiFi {
public static class LogUtil {
    /// <summary>
    /// Each log event is has a LogLevel.  The lower the LogLevel the higher its priority
    /// and the fewer expected log events.  LogLevel.Silent means no logs whatsoever.
    /// </summary>
    public enum LogLevel {
        Silent = 0,
        Error,
        Warning,
        UncommonEvent,
        CommonEvent,
        Debug
    }

    static Dictionary<string, LogLevel> LogLevelMap = new Dictionary<string, LogLevel>();

    /// <summary>
    /// GlobalMaxLogLevel is an absolute limit for ALL LogUil log events.
    /// Each log event must have equal or lower LogLevel in order to be printed.
    /// By default it is set to HiFi.LogUtil.LogLevel.Debug.
    /// </summary>
    static public LogLevel GlobalMaxLogLevel = LogLevel.Debug;

    /// <summary>
    /// SetMaxLogLevel() is used to explicitly set a max LogLevel on a per-class basis.
    /// <code>
    /// HiFi.LogUtil.SetMaxLogLevel(classInstance, HiFi.LogUtil.LogLevel.UncommonEvent);
    /// </code>
    /// All LogUtil.Log() events for all instances of the class will not be printed unless
    /// their LogLevel is lower than the MaxLogLevel for the class.  Any class whose MaxLogLevel
    /// has not been explicitly set will inherit GlobalMaxLogLevel by default.
    /// </summary>
    static public void SetMaxLogLevel(this UnityEngine.Object context, LogLevel level) {
        string key = context.GetType().ToString();
        try {
            // add it
            LogLevelMap.Add(key, level);
        } catch (ArgumentException) {
            // already added --> update it
            LogLevelMap[key] = level;
        }
    }

    /// <summary>
    /// GetLogLevel() will return the class-wide LogLevel set via SetLogLevel().
    /// If the class-wide LogLevel was not set then it will return GlobalMaxLogLevel.
    /// </summary>
    static public LogLevel GetMaxLogLevel(UnityEngine.Object context) {
        LogLevel level = GlobalMaxLogLevel;
        try {
            level = LogLevelMap[context.GetType().ToString()];
        } catch (Exception) {
        }
        return level;
    }

    /// <summary>
    /// LogError(message) will print 'message' at LogLevel.Error.
    /// </summary>
    public static void LogError(this UnityEngine.Object context, string message) {
        if (GetMaxLogLevel(context) >= LogLevel.Error) {
            try {
                string className = context.GetType().ToString();
                UnityEngine.Debug.Log(string.Format("ERROR {0}: {1}", className, message), context);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"LogUtil.LogError failed err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// LogError(template, args) will print the formatted message at LogLevel.Error.
    /// </summary>
    public static void LogError(this UnityEngine.Object context, string template, params object[] args) {
        if (GetMaxLogLevel(context) >= LogLevel.Error) {
            try {
                string className = context.GetType().ToString();
                string message = string.Format(template, args);
                UnityEngine.Debug.Log(string.Format("ERROR {0}: {1}", className, message), context);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"LogUtil.LogError failed template='{template}' err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// LogWarning(message) will print 'message' at LogLevel.Warning.
    /// </summary>
    public static void LogWarning(this UnityEngine.Object context, string message) {
        if (GetMaxLogLevel(context) >= LogLevel.Warning) {
            try {
                string className = context.GetType().ToString();
                UnityEngine.Debug.Log(string.Format("WARNING {0}: {1}", className, message), context);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"LogUtil.LogWarning failed err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// LogWarning(template, args) will print the formatted message at LogLevel.Warning.
    /// </summary>
    public static void LogWarning(this UnityEngine.Object context, string template, params object[] args) {
        if (GetMaxLogLevel(context) >= LogLevel.Warning) {
            try {
                string className = context.GetType().ToString();
                string message = string.Format(template, args);
                UnityEngine.Debug.Log(string.Format("WARNING {0}: {1}", className, message), context);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"LogUtil.LogWarning failed template='{template}' err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// LogUncommonEvent(message) will print 'message' at LogLevel.UncommonEvent.
    /// </summary>
    public static void LogUncommonEvent(this UnityEngine.Object context, string message) {
        if (GetMaxLogLevel(context) >= LogLevel.UncommonEvent) {
            try {
                string className = context.GetType().ToString();
                UnityEngine.Debug.Log(string.Format("{0}: {1}", className, message), context);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"LogUtil.LogUncommonEvent failed err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// LogUncommonEvent(template, args) will print the formatted message at LogLevel.UncommonEvent.
    /// </summary>
    public static void LogUncommonEvent(this UnityEngine.Object context, string template, params object[] args) {
        if (GetMaxLogLevel(context) >= LogLevel.UncommonEvent) {
            try {
                string className = context.GetType().ToString();
                string message = string.Format(template, args);
                UnityEngine.Debug.Log(string.Format("{0}: {1}", className, message), context);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"LogUtil.LogUncommonEvent failed template='{template}' err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// LogCommonEvent(message) will print 'message' at LogLevel.CommonEvent.
    /// </summary>
    public static void LogCommonEvent(this UnityEngine.Object context, string message) {
        if (GetMaxLogLevel(context) >= LogLevel.CommonEvent) {
            try {
                string className = context.GetType().ToString();
                UnityEngine.Debug.Log(string.Format("{0}: {1}", className, message), context);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"LogUtil.LogCommonEvent failed err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// LogCommonEvent(template, args) will print the formatted message at LogLevel.CommonEvent.
    /// </summary>
    public static void LogCommonEvent(this UnityEngine.Object context, string template, params object[] args) {
        if (GetMaxLogLevel(context) >= LogLevel.CommonEvent) {
            try {
                string className = context.GetType().ToString();
                string message = string.Format(template, args);
                UnityEngine.Debug.Log(string.Format("{0}: {1}", className, message), context);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"LogUtil.LogCommonEvent failed template='{template}' err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// LogDebug(message) will print 'message' at LogLevel.Debug.
    /// This methods will not be included in compiled builds
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG"), System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void LogDebug(this UnityEngine.Object context, string message) {
        if (GetMaxLogLevel(context) >= LogLevel.Debug) {
            try {
                string className = context.GetType().ToString();
                UnityEngine.Debug.Log(string.Format("DEBUG {0}: {1}", className, message), context);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"LogUtil.LogDebug failed err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// LogDebug(template, args) will print the formatted message at LogLevel.Debug.
    /// This methods will not be included in compiled builds
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG"), System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void LogDebug(this UnityEngine.Object context, string template, params object[] args) {
        if (GetMaxLogLevel(context) >= LogLevel.Debug) {
            try {
                string message = string.Format(template, args);
                string className = context.GetType().ToString();
                UnityEngine.Debug.Log(string.Format("DEBUG {0}: {1}", className, message), context);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"LogUtil.LogDebug failed template='{template}' err='{e.Message}'");
            }
        }
    }
}
} // namespace
