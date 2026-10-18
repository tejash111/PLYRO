using System;
using Bowling.Contracts;
using UnityEngine;

namespace Bowling.App
{
    /// <summary>
    /// The <see cref="IGameLog"/> that writes to the Unity console as "[category] message". It lives in App until
    /// Platform provides the file logger.
    /// </summary>
    public sealed class UnityConsoleGameLog : IGameLog
    {
        public void Info(string category, string message)
        {
            Debug.Log(Format(category, message));
        }

        public void Warn(string category, string message)
        {
            Debug.LogWarning(Format(category, message));
        }

        public void Error(string category, string message, Exception exception = null)
        {
            Debug.LogError(Format(category, message));
            if (exception != null) Debug.LogException(exception);
        }

        static string Format(string category, string message)
        {
            return $"[{category}] {message}";
        }
    }
}
