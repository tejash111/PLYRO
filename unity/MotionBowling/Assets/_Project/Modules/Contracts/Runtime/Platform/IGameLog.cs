using System;

namespace Bowling.Contracts
{
    public interface IGameLog
    {
        void Info(string category, string message);
        void Warn(string category, string message);
        void Error(string category, string message, Exception exception = null);
    }
}
