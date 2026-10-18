namespace Bowling.Contracts
{
    public interface ISettingsStore { OperatorSettings Load(); void Save(OperatorSettings settings); }
}
