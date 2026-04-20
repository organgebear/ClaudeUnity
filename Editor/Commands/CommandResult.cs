namespace ClaudeUnity
{
    public class CommandResult
    {
        public bool Success { get; private set; }
        public string DataJson { get; private set; }
        public string ErrorMessage { get; private set; }

        public static CommandResult Ok(string dataJson = "{}")
        {
            return new CommandResult { Success = true, DataJson = dataJson };
        }

        public static CommandResult Ok(object data)
        {
            return new CommandResult { Success = true, DataJson = MiniJson.Serialize(data) };
        }

        public static CommandResult Fail(string error)
        {
            return new CommandResult { Success = false, ErrorMessage = error, DataJson = "{}" };
        }
    }
}
