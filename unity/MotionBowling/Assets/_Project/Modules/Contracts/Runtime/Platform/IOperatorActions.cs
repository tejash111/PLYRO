using System;

namespace Bowling.Contracts
{
    public interface IOperatorActions                    // implemented by Platform; called by the UI operator panel
    {
        OperatorSettings Settings { get; }
        void ApplySettings(OperatorSettings settings);   // saves, then App re-applies camera/volume/recording
        void ResetLeaderboard();
        void Quit();                                     // clean exit (launchd does NOT relaunch on clean exit)
        event Action<OperatorSettings> SettingsApplied;
    }
}
