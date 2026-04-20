namespace ClaudeUnity
{
    public interface ICommandHandler
    {
        CommandResult Execute(string commandType, JsonObject parameters);
    }
}
