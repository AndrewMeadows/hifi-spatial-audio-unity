using System;
using System.Collections.Generic;
using Unity.WebRTC;

namespace Ravi {
public static class RaviUtil {
    static private bool s_initialized = false;

    public static bool WebRTCInitialized() { return s_initialized; }

    public static bool InitializeWebRTC() {
        if (!s_initialized) {
            s_initialized = true;
            // We're not using video tracks so we hard-code the video codec type
            // as 'Software'.  To send 'Hardware' when not available may cause
            // Unity to crash.
            // Also, we want 'directAudio' straight to the platform's default
            // audio device rather than through Unity's audio pipeline.
            // This last argument is not available in Unity's official plugin yet:
            // it relies on our own patch.
            WebRTC.Initialize(type:EncoderType.Software, directAudio:true);
            return true;
        }
        return false;
    }

    public static bool DisposeWebRTC() {
        if (s_initialized) {
            WebRTC.Dispose();
            s_initialized = false;
            return true;
        }
        return false;
    }
}

public static class Log {
    /// <summary>
    /// Each log event is has a Level.  The lower the Level the higher its priority
    /// and the fewer expected log events.  Level.Silent means no logs whatsoever.
    /// </summary>
    public enum Level {
        Silent = 0,
        Error,
        Warning,
        UncommonEvent,
        CommonEvent,
        Debug
    }

    static Dictionary<string, Level> LevelMap = new Dictionary<string, Level>();

    /// <summary>
    /// GlobalMaxLevel is an absolute limit for ALL Log events.
    /// Each log event must have equal or lower Level in order to be printed.
    /// By default it is set to Log.Level.Debug.
    /// </summary>
    static public Level GlobalMaxLevel = Level.Debug;

    /// <summary>
    /// SetMaxLevel() is used to explicitly set a max Level on a per-class basis.
    /// <code>
    /// Log.SetMaxLevel(classInstance, Log.Level.UncommonEvent);
    /// </code>
    /// All Log.Log() events for all instances of the class will not be printed unless
    /// their Level is lower than the MaxLevel for the class.  Any class whose MaxLevel
    /// has not been explicitly set will inherit GlobalMaxLevel by default.
    /// </summary>
    static public void SetMaxLevel(this object context, Level level) {
        string key = context.GetType().ToString();
        try {
            // add it
            LevelMap.Add(key, level);
        } catch (ArgumentException) {
            // already added --> update it
            LevelMap[key] = level;
        }
    }

    /// <summary>
    /// GetMaxLevel() returns the class-wide Level previously set via SetLevel().
    /// If the class-wide Level was not set then it will return GlobalMaxLevel.
    /// </summary>
    static public Level GetMaxLevel(object context) {
        Level level = GlobalMaxLevel;
        try {
            level = LevelMap[context.GetType().ToString()];
        } catch (Exception) {
        }
        return level;
    }

    /// <summary>
    /// Error(message) will print 'message' at Level.Error.
    /// </summary>
    public static void Error(this object context, string message) {
        if (GetMaxLevel(context) >= Level.Error) {
            try {
                string className = context.GetType().ToString();
                UnityEngine.Object obj = context as UnityEngine.Object;
                UnityEngine.Debug.Log(string.Format("ERROR {0}: {1}", className, message), obj);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"Log.Error failed err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// Error(template, args) will print the formatted message at Level.Error.
    /// </summary>
    public static void Error(this object context, string template, params object[] args) {
        if (GetMaxLevel(context) >= Level.Error) {
            try {
                string className = context.GetType().ToString();
                string message = string.Format(template, args);
                UnityEngine.Object obj = context as UnityEngine.Object;
                UnityEngine.Debug.Log(string.Format("ERROR {0}: {1}", className, message), obj);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"Log.Error failed template='{template}' err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// Warning(message) will print 'message' at Level.Warning.
    /// </summary>
    public static void Warning(this object context, string message) {
        if (GetMaxLevel(context) >= Level.Warning) {
            try {
                string className = context.GetType().ToString();
                UnityEngine.Object obj = context as UnityEngine.Object;
                UnityEngine.Debug.Log(string.Format("WARNING {0}: {1}", className, message), obj);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"Log.Warning failed err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// Warning(template, args) will print the formatted message at Level.Warning.
    /// </summary>
    public static void Warning(this object context, string template, params object[] args) {
        if (GetMaxLevel(context) >= Level.Warning) {
            try {
                string className = context.GetType().ToString();
                string message = string.Format(template, args);
                UnityEngine.Object obj = context as UnityEngine.Object;
                UnityEngine.Debug.Log(string.Format("WARNING {0}: {1}", className, message), obj);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"Log.Warning failed template='{template}' err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// UncommonEvent(message) will print 'message' at Level.UncommonEvent.
    /// </summary>
    public static void UncommonEvent(this object context, string message) {
        if (GetMaxLevel(context) >= Level.UncommonEvent) {
            try {
                string className = context.GetType().ToString();
                UnityEngine.Object obj = context as UnityEngine.Object;
                UnityEngine.Debug.Log(string.Format("{0}: {1}", className, message), obj);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"Log.UncommonEvent failed err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// UncommonEvent(template, args) will print the formatted message at Level.UncommonEvent.
    /// </summary>
    public static void UncommonEvent(this object context, string template, params object[] args) {
        if (GetMaxLevel(context) >= Level.UncommonEvent) {
            try {
                string className = context.GetType().ToString();
                string message = string.Format(template, args);
                UnityEngine.Object obj = context as UnityEngine.Object;
                UnityEngine.Debug.Log(string.Format("{0}: {1}", className, message), obj);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"Log.UncommonEvent failed template='{template}' err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// CommonEvent(message) will print 'message' at Level.CommonEvent.
    /// </summary>
    public static void CommonEvent(this object context, string message) {
        if (GetMaxLevel(context) >= Level.CommonEvent) {
            try {
                string className = context.GetType().ToString();
                UnityEngine.Object obj = context as UnityEngine.Object;
                UnityEngine.Debug.Log(string.Format("{0}: {1}", className, message), obj);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"Log.CommonEvent failed err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// CommonEvent(template, args) will print the formatted message at Level.CommonEvent.
    /// </summary>
    public static void CommonEvent(this object context, string template, params object[] args) {
        if (GetMaxLevel(context) >= Level.CommonEvent) {
            try {
                string className = context.GetType().ToString();
                string message = string.Format(template, args);
                UnityEngine.Object obj = context as UnityEngine.Object;
                UnityEngine.Debug.Log(string.Format("{0}: {1}", className, message), obj);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"Log.CommonEvent failed template='{template}' err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// Debug(message) will print 'message' at Level.Debug.
    /// This methods will not be included in compiled builds
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG"), System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void Debug(this object context, string message) {
        if (GetMaxLevel(context) >= Level.Debug) {
            try {
                string className = context.GetType().ToString();
                UnityEngine.Object obj = context as UnityEngine.Object;
                UnityEngine.Debug.Log(string.Format("DEBUG {0}: {1}", className, message), obj);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"Log.Debug failed err='{e.Message}'");
            }
        }
    }

    /// <summary>
    /// Debug(template, args) will print the formatted message at Level.Debug.
    /// This methods will not be included in compiled builds
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG"), System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void Debug(this object context, string template, params object[] args) {
        if (GetMaxLevel(context) >= Level.Debug) {
            try {
                string message = string.Format(template, args);
                string className = context.GetType().ToString();
                UnityEngine.Object obj = context as UnityEngine.Object;
                UnityEngine.Debug.Log(string.Format("DEBUG {0}: {1}", className, message), obj);
            } catch (Exception e) {
                UnityEngine.Debug.Log($"Log.Debug failed template='{template}' err='{e.Message}'");
            }
        }
    }
}
} // namespace
